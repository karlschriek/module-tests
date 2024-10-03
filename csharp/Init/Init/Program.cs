using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.NetworkInformation;
using System.Text.Json;
using Init;

class TerraformModuleDownloader
{
    // Structure to represent each module

    public class Module
    {
        public string DownloadToDir { get; set; } = string.Empty;

        public string RepoSource { get; set; } = string.Empty;

        public string RepoRevision { get; set; } = string.Empty;
        
        public ModuleSource SourceType { get; set; }
        
        public ModuleInfo ModuleInfo { get; set; }
    }
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
        string moduleFile = Path.Combine(".terraform/modules", "modules.json");
        string downloadDir = Path.Combine(".terraform/modules");
        
        CleanUp(downloadDir);

        // Parse the main module
        List<Module> modules = new List<Module>();

        // Start with the main module
        var initModule = new Module()
        {
            ModuleInfo = new ModuleInfo(){ Key = "", Source = "", Dir = mainModulePath }
        };
        modules.Add(initModule);

        // Download the modules recursively
        DownloadModules(mainModulePath, modules);

        // Write the modules to modules.json
        WriteModulesJson(moduleFile, modules);

        Console.WriteLine("All modules downloaded and written to modules.json.");
    }

    // Download modules recursively by parsing the source of each module
    static void DownloadModules(string parseDir, List<Module> modules, string prefix="")
    {
        // Find all module sources in the current directory
        var moduleSources = ParseModuleSources(parseDir, prefix);

        foreach (var module in moduleSources)
        {
            // Skip if already downloaded (break recursion)
            if (!Directory.Exists(module.DownloadToDir))
            {
                var sourceType = SourceHelper.CategorizeSource(module.ModuleInfo.Source, parseDir);
                // Clone the module repository
                if (sourceType == ModuleSource.GitRepository)
                    CloneModule(GetRepoPath(module.ModuleInfo.Source), module.DownloadToDir);
                else if (sourceType == ModuleSource.TerraformRegistry)
                {
                    // TODO implement this
                }
                else if (sourceType == ModuleSource.LocalPath)
                {
                    // do nothing
                }
                else
                {
                    throw new Exception("Unknown source: " + module.ModuleInfo.Source);
                }
                
                // Add to the list of modules
                modules.Add(module);

                // Recursively download submodules
                DownloadModules(module.ModuleInfo.Dir, modules, $"{module.ModuleInfo.Key}.");
            }
        }
    }

    // Parse the current module directory for other module sources by converting HCL to JSON
    static List<Module> ParseModuleSources(string moduleDir, string prefix)
    {
        var modules = new List<Module>();

        foreach (string file in Directory.GetFiles(moduleDir, "*.tf", SearchOption.AllDirectories))
        {
            string jsonFile = ConvertHCLToJson(file);
            if (jsonFile == null)
                continue;

            var jsonDoc = JsonDocument.Parse(jsonFile);

            if (jsonDoc.RootElement.TryGetProperty("module", out JsonElement modulesElement))
            {
                foreach (JsonProperty moduleProp in modulesElement.EnumerateObject())
                {
                    string moduleKey = moduleProp.Name;

                    foreach (var arrayItem in moduleProp.Value.EnumerateArray())
                    {
                        if (arrayItem.TryGetProperty("source", out JsonElement sourceElement))
                        {
                            string moduleSource = sourceElement.GetString();
                            
                            var sourceType = SourceHelper.CategorizeSource(moduleSource, moduleDir);

                            switch (sourceType)
                            {
                                case ModuleSource.GitRepository:
                                    modules.Add(CreateRepoModule(moduleSource, sourceType, prefix, moduleKey));
                                    break;
                                case ModuleSource.TerraformRegistry:
                                    break; //TODO
                                case ModuleSource.LocalPath:
                                    modules.Add(CreateLocalPathModule(moduleSource, sourceType, prefix, moduleKey));
                                    break; //TODO
                            }

                            
                        }
                    }
                    
                }
            }
        }

        return modules;
    }

    private static Module CreateRepoModule(string moduleSource, ModuleSource sourceType, string prefix, string moduleKey)
    {
        var stringParts = moduleSource.Split(new string[] { "//" }, StringSplitOptions.None); //TODO, need to also do this for local files. Split on "./"?
        string modulePath = stringParts.Length > 1 ? $"/{stringParts[1]}" : "";

        return new Module()
        {
            RepoSource = stringParts[0],
            RepoRevision = "",
            SourceType = sourceType,
            DownloadToDir = $".terraform/modules/{prefix}{moduleKey}",
            ModuleInfo = new ModuleInfo
            {
                Key = $"{prefix}{moduleKey}",
                Source = moduleSource,
                Dir = $".terraform/modules/{prefix}{moduleKey}{modulePath}"
            }
        };
    }


    private static Module CreateLocalPathModule(string moduleSource, ModuleSource sourceType, string prefix,
        string moduleKey)
    {
        return new Module()
        {
            RepoSource = "",
            RepoRevision = "",
            SourceType = sourceType,
            DownloadToDir = "",
            ModuleInfo = new ModuleInfo
            {
                Key = $"{prefix}{moduleKey}",
                Source = moduleSource,
                Dir = $".terraform/modules/{prefix}{moduleKey}{moduleSource}"
            }
        };
    }

    // Converts an HCL file to JSON using the external `hcl2json` tool
    static string ConvertHCLToJson(string hclFile)
    {

        // Run the external hcl2json command to convert HCL to JSON
        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = "hcl2json",  // Make sure hcl2json is installed and available in the PATH
            Arguments = $"\"{hclFile}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using (Process process = Process.Start(startInfo))
        {
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();

            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                Console.WriteLine($"Error converting HCL to JSON: {error}");
                return null;
            }

            return output;
        }
        

    }

    // Clone a module using git
    static void CloneModule(string moduleSource, string destination)
    {
        Console.WriteLine($"Cloning {moduleSource} into {destination}...");


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

    // Write the module list to modules.json
    static void WriteModulesJson(string outputFile, List<Module> modules)
    {
        var jsonObject = new { Modules = modules };

        var options = new JsonSerializerOptions { WriteIndented = true };
        string json = JsonSerializer.Serialize(jsonObject, options);

        File.WriteAllText(outputFile, json);
    }

    private static void CleanUp(string dir)
    {
        try
        {
            if (Directory.Exists(dir))
            {
                // Delete the directory and all its contents
                Directory.Delete(dir, recursive: true);
                Console.WriteLine($"Directory '{dir}' deleted successfully.");
            }
            else
            {
                Console.WriteLine($"Directory '{dir}' does not exist.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }
    }

    private static string GetRepoPath(string moduleSource)
    {
        return moduleSource.Split(new string[] { "//" }, StringSplitOptions.None)[0];
    }
}
