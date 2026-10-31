// tests/SlowQueryRadar.Tests.Unit/Core/SharedConfigurationExtensionsTests.cs
using Microsoft.Extensions.Configuration;
using SlowQueryRadar.Core.Configuration;

namespace SlowQueryRadar.Tests.Unit.Core;

/// <summary>Tests shared configuration discovery and source precedence.</summary>
public sealed class SharedConfigurationExtensionsTests
{
    [Fact]
    public void AddSharedSettings_FindsSharedFileFromNestedDirectory()
    {
        var root = CreateTemporaryDirectory();
        var nested = Directory.CreateDirectory(Path.Combine(root.FullName, "a", "b", "c"));
        File.WriteAllText(Path.Combine(root.FullName, "SlowQueryRadar.slnx"), string.Empty);
        File.WriteAllText(
            Path.Combine(root.FullName, "appsettings.Shared.json"),
            "{\"QueryStore\":{\"ConnectionString\":\"shared-value\"}}");
        var previousCurrentDirectory = Directory.GetCurrentDirectory();

        try
        {
            Directory.SetCurrentDirectory(nested.FullName);
            var builder = new ConfigurationBuilder();
            SharedConfiguration.AddSharedSettings(builder);
            var configuration = builder.Build();

            Assert.Equal("shared-value", configuration["QueryStore:ConnectionString"]);
        }
        finally
        {
            Directory.SetCurrentDirectory(previousCurrentDirectory);
            root.Delete(recursive: true);
        }
    }

    [Fact]
    public void AddSharedSettings_WhenFileIsMissing_IsNoOp()
    {
        var root = CreateTemporaryDirectory();
        var nested = Directory.CreateDirectory(Path.Combine(root.FullName, "nested"));
        File.WriteAllText(Path.Combine(root.FullName, "SlowQueryRadar.slnx"), string.Empty);
        var previousCurrentDirectory = Directory.GetCurrentDirectory();

        try
        {
            Directory.SetCurrentDirectory(nested.FullName);
            var builder = new ConfigurationBuilder();
            SharedConfiguration.AddSharedSettings(builder);
            var configuration = builder.Build();

            Assert.Null(configuration["QueryStore:ConnectionString"]);
        }
        finally
        {
            Directory.SetCurrentDirectory(previousCurrentDirectory);
            root.Delete(recursive: true);
        }
    }

    [Fact]
    public void EnvironmentVariablesOverrideSharedConfiguration()
    {
        var root = CreateTemporaryDirectory();
        var nested = Directory.CreateDirectory(Path.Combine(root.FullName, "nested", "leaf"));
        File.WriteAllText(Path.Combine(root.FullName, "SlowQueryRadar.slnx"), string.Empty);
        var variableName = "QueryStore__ConnectionString";
        var previousValue = Environment.GetEnvironmentVariable(variableName);
        var previousCurrentDirectory = Directory.GetCurrentDirectory();
        File.WriteAllText(
            Path.Combine(root.FullName, "appsettings.Shared.json"),
            "{\"QueryStore\":{\"ConnectionString\":\"shared-value\"}}");

        try
        {
            Directory.SetCurrentDirectory(nested.FullName);
            Environment.SetEnvironmentVariable(variableName, "environment-value");
            var builder = new ConfigurationBuilder();
            SharedConfiguration.AddSharedSettings(builder);
            var configuration = builder
                .AddEnvironmentVariables()
                .Build();

            Assert.Equal("environment-value", configuration["QueryStore:ConnectionString"]);
        }
        finally
        {
            Directory.SetCurrentDirectory(previousCurrentDirectory);
            Environment.SetEnvironmentVariable(variableName, previousValue);
            root.Delete(recursive: true);
        }
    }

    private static DirectoryInfo CreateTemporaryDirectory()
    {
        return Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"SlowQueryRadar-{Guid.NewGuid():N}"));
    }
}
