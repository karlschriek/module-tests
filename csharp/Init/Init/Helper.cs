using System.Text.RegularExpressions;

namespace Init;

public enum ModuleSource
{
    LocalPath,
    GitRepository,
    TerraformRegistry,
    Unknown
}

public static class SourceHelper
{
    public static ModuleSource CategorizeSource(string source, string workingDir)
    {

        // Check if it's a git repo
        if (IsGitRepo(source))
        {
            return ModuleSource.GitRepository;
        }
        
        // Check for a local path
        if (IsLocalPath(workingDir, source))
        {
            return ModuleSource.LocalPath;
        }

        // Check if it's a terraform registry
        if (IsTerraformRegistry(source))
        {
            return ModuleSource.TerraformRegistry;
        }

        return ModuleSource.Unknown;
    }

    static bool IsLocalPath(string workingDir, string source)
    {
        // Check if the source is a local file or directory path
        var path = Path.Combine(workingDir, source);
        return Directory.Exists(path) || File.Exists(path);
    }

    static bool IsGitRepo(string source)
    {
        // Check if the source matches the common git URL formats
        string gitPattern = @"^(https:\/\/|git@|ssh:\/\/|github\.com|git:\/\/|bitbucket\.org)";
        return Regex.IsMatch(source, gitPattern, RegexOptions.IgnoreCase);
    }

    static bool IsTerraformRegistry(string source)
    {
        // Check if the source matches the Terraform Registry module format
        // Format: <namespace>/<name>/<provider>
        string terraformRegistryPattern = @"^[\w\-]+\/[\w\-]+\/[\w\-]+$";
        return Regex.IsMatch(source, terraformRegistryPattern);
    }
}