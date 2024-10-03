using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

class TerraformModuleDownloader
{
    // Structure to represent each module
    public class ModuleInfo
    {
        public string Key { get; set; }
        public string Source { get; set; }
        public string Dir { get; set; }
    }

    // Main method to kick off the download process
    public static void Main(string[] args)
    {
        string mainModulePath = args.Length > 0 ? args[0] : ".";
        string moduleFile = Path.Combine(mainModulePath, "modules.json");

        // Parse the main module
        List<ModuleInfo> modules = new List<ModuleInfo>();

        // Start with the main module
        modules.Add(new ModuleInfo { Key = "", Source = "", Dir = mainModulePath });

        // Download the modules recursively
        DownloadModules(mainModulePath, modules);

        // Write the modules to modules.json
        WriteModulesJson(moduleFile, modules);

        Console.WriteLine("All modules downloaded and written to modules.json.");
    }

    // Download modules recursively by parsing the source of each module
    static void DownloadModules(string moduleDir, List<ModuleInfo> modules)
    {
        // Find all module sources in the current directory
        var moduleSources = ParseModuleSources(moduleDir);

        foreach (var module in moduleSources)
        {
            string moduleKey = module.Key;
            string moduleSource = module.Source;
            string moduleDestination = Path.Combine(moduleDir, ".terraform/modules", moduleKey);

            // Skip if already downloaded
            if (!Directory.Exists(moduleDestination))
            {
                // Clone the module repository
                CloneModule(moduleSource, moduleDestination);
            }

            // Add to the list of modules
            modules.Add(new ModuleInfo { Key = moduleKey, Source = moduleSource, Dir = moduleDestination });

            // Recursively download submodules
            DownloadModules(moduleDestination, modules);
        }
    }

    // Parse the current module directory for other module sources
    static List<ModuleInfo> ParseModuleSources(string moduleDir)
    {
        var modules = new List<ModuleInfo>();
        string moduleRegexPattern = @"module\s+""(\w+)""\s+\{[^}]*source\s*=\s*""([^""]*)""[^}]*\}";

        foreach (string file in Directory.GetFiles(moduleDir, "*.tf", SearchOption.AllDirectories))
        {
            string content = File.ReadAllText(file);

            foreach (Match match in Regex.Matches(content, moduleRegexPattern, RegexOptions.Singleline))
            {
                string moduleKey = match.Groups[1].Value;
                string moduleSource = match.Groups[2].Value;

                if (moduleSource.StartsWith("git::"))
                {
                    modules.Add(new ModuleInfo
                    {
                        Key = moduleKey,
                        Source = moduleSource,
                        Dir = $".terraform/modules/{moduleKey}"
                    });
                }
            }
        }

        return modules;
    }

    // Clone a module using git
    static void CloneModule(string moduleSource, string destination)
    {
        Console.WriteLine($"Cloning {moduleSource} into {destination}...");

        try
        {
            // Execute the git clone command
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = $"clone {moduleSource.Replace("git::", "")} {destination}",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (Process process = Process.Start(startInfo))
            {
                process.WaitForExit();
                if (process.ExitCode != 0)
                {
                    Console.WriteLine($"Failed to clone {moduleSource}. Exit Code: {process.ExitCode}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error cloning module: {ex.Message}");
        }
    }

    // Write the module list to modules.json
    static void WriteModulesJson(string outputFile, List<ModuleInfo> modules)
    {
        var jsonObject = new { Modules = modules };

        var options = new JsonSerializerOptions { WriteIndented = true };
        string json = JsonSerializer.Serialize(jsonObject, options);

        File.WriteAllText(outputFile, json);
    }
}
