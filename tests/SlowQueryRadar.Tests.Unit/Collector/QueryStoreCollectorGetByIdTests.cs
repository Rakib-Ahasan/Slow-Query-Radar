//tests/SlowQueryRadar.Tests.Unit/Collector/QueryStoreCollectorGetByIdTests.cs
using Xunit;
using SlowQueryRadar.Core;

namespace SlowQueryRadar.Tests.Unit.Collector;

public sealed class QueryStoreCollectorGetByIdTests
{
    [Fact]
    public async Task GetByQueryIdAsync_WhenQueryExists_ReturnsSnapshot()
    {
        var connection = new QueryStoreCollectorTests.FakeQueryStoreConnection("READ_WRITE");
        connection.AddQuery(23, "SELECT * FROM Orders WHERE Id = @Id", 10, 5, 20, 100, 200);
        var collector = QueryStoreCollectorTests.CreateCollector(connection);

        var snapshot = await collector.GetByQueryIdAsync(23, CancellationToken.None);

        Assert.NotNull(snapshot);
        Assert.Equal(23L, snapshot.QueryId);
        Assert.Equal("SELECT * FROM Orders WHERE Id = @Id", snapshot.SqlText);
    }

    [Fact]
    public async Task GetByQueryIdAsync_WhenQueryDoesNotExist_ReturnsNull()
    {
        var connection = new QueryStoreCollectorTests.FakeQueryStoreConnection("READ_WRITE");
        connection.AddQuery(23, "SELECT * FROM Orders WHERE Id = @Id", 10, 5, 20, 100, 200);
        var collector = QueryStoreCollectorTests.CreateCollector(connection);

        var snapshot = await collector.GetByQueryIdAsync(999999, CancellationToken.None);

        Assert.Null(snapshot);
    }

    [Fact]
    public async Task GetByQueryIdAsync_WhenQueryStoreIsOff_ThrowsQueryStoreDisabledException()
    {
        var connection = new QueryStoreCollectorTests.FakeQueryStoreConnection("OFF");
        var collector = QueryStoreCollectorTests.CreateCollector(connection);

        var exception = await Assert.ThrowsAsync<QueryStoreDisabledException>(() =>
            collector.GetByQueryIdAsync(23, CancellationToken.None));

        Assert.Contains("QUERY_STORE = ON", exception.Message);
    }
}
