// tests/SlowQueryRadar.Tests.Unit/Core/SharedConfigurationExtensionsTests.cs
using Microsoft.Extensions.Configuration;
using SlowQueryRadar.Core.Configuration;

namespace SlowQueryRadar.Tests.Unit.Core;

/// <summary>Tests shared configuration discovery and source precedence.</summary>
public sealed class SharedConfigurationExtensionsTests
{
    [Fact]
    public void AddSharedAppSettings_LoadsFileFromAncestorDirectory()
    {
        var root = CreateTemporaryDirectory();
        var parent = Directory.CreateDirectory(Path.Combine(root.FullName, "parent"));
        var leaf = Directory.CreateDirectory(Path.Combine(parent.FullName, "leaf"));
        File.WriteAllText(
            Path.Combine(root.FullName, "appsettings.Shared.json"),
            "{\"QueryStore\":{\"ConnectionString\":\"shared-value\"}}");

        try
        {
            var configuration = new ConfigurationBuilder()
                .AddSharedAppSettings(leaf.FullName)
                .Build();

            Assert.Equal("shared-value", configuration["QueryStore:ConnectionString"]);
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Fact]
    public void AddSharedAppSettings_WhenFileIsMissing_IsNoOp()
    {
        var root = CreateTemporaryDirectory();

        try
        {
            var configuration = new ConfigurationBuilder()
                .AddSharedAppSettings(root.FullName)
                .Build();

            Assert.Null(configuration["QueryStore:ConnectionString"]);
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Fact]
    public void EnvironmentVariablesOverrideSharedConfiguration()
    {
        var root = CreateTemporaryDirectory();
        var variableName = "QueryStore__ConnectionString";
        var previousValue = Environment.GetEnvironmentVariable(variableName);
        File.WriteAllText(
            Path.Combine(root.FullName, "appsettings.Shared.json"),
            "{\"QueryStore\":{\"ConnectionString\":\"shared-value\"}}");

        try
        {
            Environment.SetEnvironmentVariable(variableName, "environment-value");
            var configuration = new ConfigurationBuilder()
                .AddSharedAppSettings(root.FullName)
                .AddEnvironmentVariables()
                .Build();

            Assert.Equal("environment-value", configuration["QueryStore:ConnectionString"]);
        }
        finally
        {
            Environment.SetEnvironmentVariable(variableName, previousValue);
            root.Delete(recursive: true);
        }
    }

    private static DirectoryInfo CreateTemporaryDirectory()
    {
        return Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"SlowQueryRadar-{Guid.NewGuid():N}"));
    }
}
