using System;
using System.IO;
using System.Text.Json;

// Config
const string engineToken = "$(EngineDir)/";

// Entry point

if (args.Length < 1 || string.IsNullOrEmpty(args[0]) || args[0].Length < 25 || !Directory.Exists(args[0].Trim()))
    ExitWithLogMsg("Invalid/Non-existent platform directory specified!\nSupply valid path e.g. D:\\UESources\\Engine\\Binaries\\Win64\\", 3);

string targetDir = args[0].Trim();
string[] targetFiles = Directory.GetFiles(targetDir, "*.target", SearchOption.TopDirectoryOnly);

if (targetFiles.Length < 1)
    ExitWithLogMsg($"No target-files found in {targetDir}", 2);

string sourceRoot = Path.GetFullPath(Path.Combine(targetDir, @"..\..\..\"));
string engineDir = Path.GetFullPath(Path.Combine(sourceRoot, @"Engine\"));

if (!Directory.Exists(Path.Combine(sourceRoot, @"Engine\"))) //|| !Directory.Exists(Path.Combine(sourceRoot, @"Samples\")) || !Directory.Exists(Path.Combine(sourceRoot, @"FeaturePacks\"))) //|| !File.Exists(Path.Combine(sourceRoot, @"Setup.bat")))
    ExitWithLogMsg($"This does not appear to be UE source/installation root dir: {sourceRoot}", 3);

if (!File.Exists(Path.Combine(sourceRoot, @".ue4dependencies")))
     ExitWithLogMsg($"Dependencies manifest not found. Please run Setup.bat/GitDependencies.exe first.", 2);

Console.WriteLine($"Processing {targetFiles.Length} target files...");

int total = 0;
int errors = 0;
foreach (var filePath in targetFiles)
{
    Console.WriteLine($"Checking {Path.GetFileName(filePath)}");
    try
    {
        using (JsonDocument document = JsonDocument.Parse(File.ReadAllText(filePath)))
        {
            JsonElement root = document.RootElement;
            ProcessTargetEntry(root, "BuildProducts", ref total, ref errors);
            ProcessTargetEntry(root, "RuntimeDependencies", ref total, ref errors);
        }
    }
    catch (JsonException)
    {
        PrintColoredOneShot($"   Inavlid JSON format for {Path.GetFileName(filePath)}", ConsoleColor.Yellow);
    }
}

ExitWithLogMsg($"Total entries: {total}, Missing files: {errors}", 0);


// ==================================================================
// Workers

void PrintColoredOneShot(string text, ConsoleColor color)
{
    var originalColor = Console.ForegroundColor;
    Console.ForegroundColor = color;
    Console.WriteLine(text);
    Console.ForegroundColor = originalColor;
}

void ProcessTargetEntry(JsonElement root, string propName, ref int total, ref int errors)
{
    if (root.TryGetProperty(propName, out JsonElement foundProp))
    {
        foreach (JsonElement entry in foundProp.EnumerateArray())
        {
            if (entry.TryGetProperty("Path", out JsonElement pathData))
            {
                total++;
                var rawPath = pathData.GetString();
                if (rawPath?.IndexOf(engineToken) == -1)
                {
                    PrintColoredOneShot($"   Invalid path prop: {rawPath}", ConsoleColor.Gray);
                    continue;
                }
                var finalPath = Path.GetFullPath(Path.Combine(engineDir, rawPath!.Replace("$(EngineDir)/", "")));
                //Console.WriteLine(finalPath);
                if (!File.Exists(finalPath))
                {
                    var color = finalPath.EndsWith(".pdb") ? ConsoleColor.Yellow : ConsoleColor.Red;
                    PrintColoredOneShot($"   missing: {finalPath}", color);
                    errors++;
                }
            }
        }
    }
    else { Console.WriteLine($"{propName} entries not found, skipping..."); }
}

void ExitWithLogMsg(string message, int exitCode)
{
    Console.WriteLine(message);
    Environment.Exit(exitCode);
}