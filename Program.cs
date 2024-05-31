// See https://aka.ms/new-console-template for more information

using System.Text;
using System.Text.RegularExpressions;
using craftersmine.Asar.Net;
using NUglify;
using NUglify.Css;
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

var uglifiedJs = Uglify.Js(jsCode, new CodeSettings()
{
    LocalRenaming = LocalRenaming.KeepAll,
}).Code;

//TODO: write already completed, if works is done
//TODO: add recovery button
var currentDirPath = Directory.GetCurrentDirectory();

var tempDirName = "mattermost_temp"; //"app";
var tempDirPath = Path.Combine(currentDirPath, tempDirName);

if (!TryFindMattermostAppFilePath(out var appFilePath))
{
    Console.WriteLine("Mattermost is not found");
    Thread.Sleep(1000);
    return;
}

Console.WriteLine($"Mattermost found by path: {appFilePath}");

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


// RemoveTempDir();
//
// if (!TryUnpackAppFile(appFilePath, tempDirPath))
// {
//     Console.WriteLine("Error on unpacking");
//     OnReturn();
//     return;
// }

// if (!TryFindInjectingFile(tempDirPath, out var injectingFilePath))
// {
//     Console.WriteLine("script file is not found");
//     OnReturn();
//     return;
// }


string fileData;

try
{
    // fileData = File.ReadAllText(injectingFilePath, Encoding.UTF8);
    fileData = File.ReadAllText(appFilePath, Encoding.ASCII);
}
catch (Exception e)
{
    Console.WriteLine("Failure on reading code file");
    OnReturn();
    return;
}

var injectingAfterLineCode = Regex.Match(
    fileData,
    @"addEventListener\(""storage"",\w+?\),window.addEventListener\(""load"",\w+?\)");
// var lineEndIndex = fileData.LastIndexOf(
//     "}",
//     StringComparison.InvariantCultureIgnoreCase);

var lineEndIndex = fileData.IndexOf(
    "}",
    injectingAfterLineCode.Index,
    StringComparison.InvariantCultureIgnoreCase);
    
if (lineEndIndex < 0)
{
    Console.WriteLine("Ending line index is not found");
    OnReturn();
    return;
}

var insertingString = Encoding.ASCII.GetString(
    Encoding.Convert(Encoding.UTF8, Encoding.ASCII, Encoding.UTF8.GetBytes(";" + uglifiedJs + ";")));
var newJsCode = fileData.Insert(lineEndIndex, insertingString);

try
{
    // var uglifiedNewJsCode = Uglify.Js(newJsCode).Code;
    // File.WriteAllText(injectingFilePath, newJsCode, Encoding.UTF8);
    File.WriteAllText(appFilePath, newJsCode, Encoding.ASCII);
}
catch (Exception e)
{
    Console.WriteLine("Failure on updating code file");
    OnReturn();
    return;
}

// if (!TryArchiveDirectory(tempDirPath, currentDirPath, tempDirName))
// {
//     Console.WriteLine("Failure on creating new archive");
//     OnReturn();
//     return;
// }

// var updatedMattermostFilePath = Path.Combine(currentDirPath, tempDirName + ".asar");
//
// if (!File.Exists(updatedMattermostFilePath))
// {
//     Console.WriteLine("Updated mattermost file is not found");
//     OnReturn();
//     return;
// }


// if (!ReplaceAppFileWithNew(currentDirPath, appFilePath, updatedMattermostFilePath))
// {
//     Console.WriteLine("Failure on replacing the file with the new one");
//     OnReturn();
//     return;
// }


//JUST RENAMING
// if (!RenameAppFile(appFilePath))
// {
//     Console.WriteLine("Failure on renaming app file");
//     OnReturn();
//     return;
// }

Console.WriteLine("Completed!");
Thread.Sleep(5000);
return;

void OnReturn()
{
    RemoveTempDir(); 
    Thread.Sleep(1000);
}

void RemoveTempDir()
{
    if (Directory.Exists(tempDirPath))
    {
        Directory.Delete(tempDirPath, true);
    }
}

static void RemoveDirIfExists(string ditPath)
{
    if (Directory.Exists(ditPath))
    {
        Directory.Delete(ditPath, true);
    }
}

bool TryFindMattermostAppFilePath(out string path)
{
    var appFileName = "app.asar";
    var appFilePath = Path.Combine(currentDirPath, "app.asar");

    if (!File.Exists(appFilePath))
    {
        path = string.Empty;
        return false;
    }

    path = appFilePath;
    return true;
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

static bool RenameAppFile(string appFilePath)
{
    try
    {
        if (!File.Exists(appFilePath))
        {
            //If app file doesnt exists electron wont load it
            return true;
        }

        var appFile = new FileInfo(appFilePath);
        var oldFileNameWitDate = Path.Combine(appFile.DirectoryName, $"app_{DateTime.Now:dd-MM-yyyy_HH-mm-sss}.asar");
        File.Move(appFilePath, oldFileNameWitDate);
        return true;
    }
    catch (Exception e)
    {
        return false;
    }
}