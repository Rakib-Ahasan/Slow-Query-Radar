// src/SlowQueryRadar.Core/Configuration/SharedConfiguration.cs
using Microsoft.Extensions.Configuration;

namespace SlowQueryRadar.Core.Configuration;

/// <summary>Provides shared configuration loading for local development.</summary>
public static class SharedConfiguration
{
    private const int MaxDirectoryLevels = 10;
    private const string SolutionFileName = "SlowQueryRadar.slnx";
    private const string SharedSettingsFileName = "appsettings.Shared.json";
    private const string SharedTemplateFileName = "appsettings.Shared.template.json";

    /// <summary>
    /// Adds the repository-root <c>appsettings.Shared.json</c> source when discoverable.
    /// </summary>
    /// <param name="builder">The configuration builder.</param>
    /// <returns>The same configuration builder.</returns>
    public static IConfigurationBuilder AddSharedSettings(IConfigurationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (!TryFindSolutionRoot(out var solutionRoot))
        {
            WarnMissingSharedSettings("Could not locate repository root containing SlowQueryRadar.slnx.");
            return builder;
        }

        var sharedSettingsPath = Path.Combine(solutionRoot, SharedSettingsFileName);
        builder.AddJsonFile(sharedSettingsPath, optional: true, reloadOnChange: false);
        if (!File.Exists(sharedSettingsPath))
        {
            WarnMissingSharedSettings($"Expected '{SharedSettingsFileName}' at '{solutionRoot}'.");
        }

        return builder;
    }

    /// <summary>Attempts to locate the shared settings file path.</summary>
    /// <param name="path">Resolved path when found.</param>
    /// <returns><see langword="true"/> when found; otherwise <see langword="false"/>.</returns>
    public static bool TryFindSharedSettingsPath(out string? path)
    {
        if (TryFindSolutionRoot(out var solutionRoot))
        {
            var candidate = Path.Combine(solutionRoot, SharedSettingsFileName);
            if (File.Exists(candidate))
            {
                path = candidate;
                return true;
            }
        }

        path = null;
        return false;
    }

    private static bool TryFindSolutionRoot(out string solutionRoot)
    {
        foreach (var startDirectory in EnumerateStartDirectories())
        {
            var current = new DirectoryInfo(startDirectory);
            for (var depth = 0; depth <= MaxDirectoryLevels && current is not null; depth++)
            {
                if (File.Exists(Path.Combine(current.FullName, SolutionFileName)))
                {
                    solutionRoot = current.FullName;
                    return true;
                }

                current = current.Parent;
            }
        }

        solutionRoot = string.Empty;
        return false;
    }

    private static IEnumerable<string> EnumerateStartDirectories()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var currentDirectory = Directory.GetCurrentDirectory();
        if (!string.IsNullOrWhiteSpace(currentDirectory) && seen.Add(currentDirectory))
        {
            yield return currentDirectory;
        }

        if (!string.IsNullOrWhiteSpace(AppContext.BaseDirectory) && seen.Add(AppContext.BaseDirectory))
        {
            yield return AppContext.BaseDirectory;
        }
    }

    private static void WarnMissingSharedSettings(string reason)
    {
        Console.Error.WriteLine(
            $"Warning: {reason} Copy '{SharedTemplateFileName}' to '{SharedSettingsFileName}' in the repository root.");
    }
}