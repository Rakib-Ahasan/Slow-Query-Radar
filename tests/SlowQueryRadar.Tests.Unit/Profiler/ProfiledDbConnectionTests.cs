//tests/SlowQueryRadar.Tests.Unit/Profiler/ProfiledDbConnectionTests.cs
using System.Data;
using Microsoft.Data.Sqlite;
using SlowQueryRadar.Profiler;
using SlowQueryRadar.Profiler.Sinks;
using Xunit;

namespace SlowQueryRadar.Tests.Unit.Profiler;

public sealed class ProfiledDbConnectionTests
{
    [Fact]
    public void OpenDelegatesToInnerConnection()
    {
        using var inner = new SqliteConnection("Data Source=:memory:");
        using var profiled = new ProfiledDbConnection(inner, new InMemoryPerformanceSink());

        profiled.Open();

        Assert.Equal(ConnectionState.Open, inner.State);
        Assert.Equal(ConnectionState.Open, profiled.State);
    }

    [Fact]
    public void BeginTransactionDelegatesToInnerConnection()
    {
        using var inner = new SqliteConnection("Data Source=:memory:");
        using var profiled = new ProfiledDbConnection(inner, new InMemoryPerformanceSink());
        profiled.Open();

        using var transaction = profiled.BeginTransaction();

        Assert.NotNull(transaction);
        Assert.Equal(inner, transaction.Connection);
    }

    [Fact]
    public void CreateDbCommandReturnsProfiledDbCommand()
    {
        using var inner = new SqliteConnection("Data Source=:memory:");
        using var profiled = new ProfiledDbConnection(inner, new InMemoryPerformanceSink());

        using var command = profiled.CreateCommand();

        Assert.IsType<ProfiledDbCommand>(command);
    }
}
