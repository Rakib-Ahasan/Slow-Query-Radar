// tests/SlowQueryRadar.Tests.Integration/IntegrationTestFixture.cs
using Microsoft.Extensions.Configuration;
using SlowQueryRadar.Core.Configuration;

namespace SlowQueryRadar.Tests.Integration;

/// <summary>Provides shared configuration for integration tests.</summary>
public sealed class IntegrationTestFixture
{
    /// <summary>Gets the configuration assembled for integration tests.</summary>
    public IConfiguration Configuration { get; } = BuildConfiguration();

    private static IConfiguration BuildConfiguration()
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory);
        SharedConfiguration.AddSharedSettings(builder);
        return builder
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();
    }
}
