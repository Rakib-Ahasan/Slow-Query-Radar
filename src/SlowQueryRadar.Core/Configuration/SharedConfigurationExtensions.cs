// src/SlowQueryRadar.Core/Configuration/SharedConfigurationExtensions.cs
using Microsoft.Extensions.Configuration;

namespace SlowQueryRadar.Core.Configuration;

/// <summary>Provides configuration sources shared by local development projects.</summary>
public static class SharedConfigurationExtensions
{
    /// <summary>
    /// Walks up the directory tree from <paramref name="startDirectory"/>
    /// looking for appsettings.Shared.json. Loads the first one found
    /// (optional). Silently no-ops if nothing is found.
    /// </summary>
    /// <param name="builder">The configuration builder.</param>
    /// <param name="startDirectory">The directory from which to start searching.</param>
    /// <returns>The same configuration builder.</returns>
    public static IConfigurationBuilder AddSharedAppSettings(
        this IConfigurationBuilder builder,
        string? startDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        startDirectory ??= AppContext.BaseDirectory;

        var directory = new DirectoryInfo(startDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "appsettings.Shared.json");
            if (File.Exists(candidate))
            {
                builder.AddJsonFile(candidate, optional: true, reloadOnChange: false);
                return builder;
            }

            directory = directory.Parent;
        }

        return builder;
    }
}
