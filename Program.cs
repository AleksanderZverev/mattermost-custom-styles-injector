// See https://aka.ms/new-console-template for more information

using System.Text;
using craftersmine.Asar.Net;
using NUglify;
using NUglify.JavaScript;

var cssCode = """
              .sidebar--left__icons {display:none}

              #sidebar-left .nav-pills__unread-indicator {display:none}

              .post.same--root.same--user time.post__time {opacity:0.3}

              .SidebarChannelGroup.isCollapsed .SidebarChannelGroupHeader.muted + .SidebarChannelGroup_content {display:none}
              """;

var uglifiedCss = Uglify.Css(cssCode).Code;

var jsCode = $$"""
               const injectInterval = setInterval((() => {
                   const inject_root = document.getElementById('root');
                   console.log('INJECTING!!!', inject_root);
                   if (!inject_root) {
                       console.log('INJECTING NULL', inject_root);
                       return;
                   }
               
                   console.log('INJECTING CSSS', inject_root);
                   var inject_styles = '{{uglifiedCss}}';
                   var inject_styleSheet = document.createElement('style');
                   inject_styleSheet.innerText = inject_styles;
                   document.head.appendChild(inject_styleSheet);
               
                   console.log('INJECTING CLEAR', inject_root);
                   clearInterval(injectInterval);
               }), 1e3);
               """;

/* //Для ручной распаковки и обратно
npm install -g asar
asar extract app.asar app
mv app.asar original-app.asar

asar pack app app.asar
 */

var uglifiedJs = Uglify.Js(
    jsCode,
    new CodeSettings()
    {
        LocalRenaming = LocalRenaming.KeepAll,
    }).Code;

//TODO: add recovery button

var oldAppFileName = "app-old.asar";
var appFileName = "app.asar";

if (!TryFindMattermostAppDirectory(out var mattermostDirectory))
{
    Console.WriteLine("Не удалось найти приложение Mattermost");
    Thread.Sleep(1000);
    return;
}

if (File.Exists(Path.Combine(mattermostDirectory, oldAppFileName)))
{
    Console.WriteLine("Стили Mattermost уже заменены");
    Thread.Sleep(1000);
    return;
}

var appFilePath = Path.Combine(mattermostDirectory, appFileName);

if (!File.Exists(appFilePath))
{
    Console.WriteLine("Не удалось найти файл приложения Mattermost");
    Thread.Sleep(1000);
    return;
}

Console.WriteLine($"Mattermost найден по пути: {mattermostDirectory}");

#if !DEBUG
Console.WriteLine($"Continue: y/n");
var answer = Console.ReadLine()?.Trim().ToLowerInvariant();

if (answer != "y")
{
    Console.WriteLine("Closing...");
    Thread.Sleep(1000);
    return;
}
#endif

var unpackedDirectoryName = "app";
var unpackedDirectoryPath = Path.Combine(mattermostDirectory, unpackedDirectoryName);

RemoveUnpackedDirectory();

if (!TryUnpackAppFile(appFilePath, unpackedDirectoryPath))
{
    Console.WriteLine("Не удалось разархивировать приложение");
    OnReturn();
    return;
}

if (!TryFindInjectingFile(unpackedDirectoryPath, out var injectingFilePath))
{
    Console.WriteLine("Не удалось найти файл стилей");
    OnReturn();
    return;
}

string fileData;

try
{
    fileData = File.ReadAllText(injectingFilePath, Encoding.UTF8);
}
catch (Exception e)
{
    Console.WriteLine("Не удалось прочитать файл стилей");
    OnReturn();
    return;
}


var lineEndIndex = fileData.LastIndexOf(
    "}",
    StringComparison.InvariantCultureIgnoreCase);

if (lineEndIndex < 0)
{
    Console.WriteLine("Не удалось найти позицию вставки новых стилей");
    OnReturn();
    return;
}

var newJsCode = fileData.Insert(lineEndIndex, ";" + uglifiedJs);

try
{
    File.WriteAllText(injectingFilePath, newJsCode, Encoding.UTF8);
}
catch (Exception e)
{
    Console.WriteLine("Не удалось обновить файл приложения");
    OnReturn();
    return;
}

if (!RenameFileIfExists(appFilePath, oldAppFileName))
{
    Console.WriteLine("Не удалось переиноменовать файл приложения");
    OnReturn();
    return;
}

Console.WriteLine("Стили применены!");
Console.ReadKey(true);
return;

void OnReturn()
{
    RemoveUnpackedDirectory();
    Console.ReadKey(true);
}

void RemoveUnpackedDirectory()
{
    RemoveDirIfExists(unpackedDirectoryPath);
}

static void RemoveDirIfExists(string ditPath)
{
    if (Directory.Exists(ditPath))
    {
        Directory.Delete(ditPath, true);
    }
}

bool TryFindMattermostAppDirectory(out string path)
{
    var localAppPath =  Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);;
    var directoryPath = Path.Combine(localAppPath, "Programs", "mattermost-desktop", "resources");

    if (Directory.Exists(directoryPath))
    {
        path = directoryPath;
        return true;
    }
    
    path = string.Empty;
    return false;
}

static bool TryUnpackAppFile(string filePath, string outputDirPath)
{
    try
    {
        var appArchive = new AsarArchive(filePath);
        var unpacker = new AsarArchiveUnpacker(appArchive);

        unpacker.UnpackAsync(outputDirPath).GetAwaiter().GetResult();
        appArchive.Dispose();
        return true;
    }
    catch (Exception e)
    {
        return false;
    }
}

static bool TryFindInjectingFile(string directoryPath, out string path)
{
    var injectingFilePath = Path.Combine(directoryPath, "externalAPI.js");

    if (!File.Exists(injectingFilePath))
    {
        path = string.Empty;
        return false;
    }

    path = injectingFilePath;
    return true;
}

static bool TryArchiveDirectory(string archivingDirectoryPath, string outputDirectory, string archiveName)
{
    try
    {
        var packingArchiveData = AsarArchivePackerDataBuilder.CreateBuilder(outputDirectory, archiveName);

        var archivingDir = new DirectoryInfo(archivingDirectoryPath);

        foreach (var file in archivingDir.GetFiles())
        {
            packingArchiveData.AddFile(file);
        }

        foreach (var dir in archivingDir.GetDirectories())
        {
            packingArchiveData.AddDirectory(dir);
        }

        packingArchiveData.PerformFileSort(true);

        var packer = new AsarArchivePacker(packingArchiveData.CreateArchiveData());
        packer.PackAsync().GetAwaiter().GetResult();
        return true;
    }
    catch (Exception e)
    {
        return false;
    }
}

static bool ReplaceAppFileWithNew(string outputDirPath, string replacingFilePath, string newFilePath)
{
    try
    {
        var oldFileNameWitDate = Path.Combine(outputDirPath, $"app_{DateTime.Now:dd-MM-yyyy_HH-mm-sss}.asar");
        File.Move(replacingFilePath, oldFileNameWitDate);
        File.Move(newFilePath, replacingFilePath);
        return true;
    }
    catch (Exception e)
    {
        return false;
    }
}

static bool RenameFileIfExists(string appFilePath, string newFileName)
{
    try
    {
        if (!File.Exists(appFilePath))
        {
            //If app file doesnt exists electron wont load it
            return true;
        }

        var appFile = new FileInfo(appFilePath);
        var oldFileNameWitDate = Path.Combine(appFile.DirectoryName, newFileName);
        File.Move(appFilePath, oldFileNameWitDate);
        return true;
    }
    catch (Exception e)
    {
        return false;
    }
}