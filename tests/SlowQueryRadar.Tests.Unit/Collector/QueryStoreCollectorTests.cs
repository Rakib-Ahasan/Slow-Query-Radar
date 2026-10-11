//tests/SlowQueryRadar.Tests.Unit/Collector/QueryStoreCollectorTests.cs
#pragma warning disable CS8765
using System.Collections;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Options;
using SlowQueryRadar.Collector;
using SlowQueryRadar.Core;
using Xunit;

namespace SlowQueryRadar.Tests.Unit.Collector;

public sealed class QueryStoreCollectorTests
{
    [Fact]
    public async Task CollectTopSlowQueriesAsync_WhenQueryStoreIsOff_ThrowsQueryStoreDisabledException()
    {
        var connection = new FakeQueryStoreConnection("OFF");
        var collector = CreateCollector(connection);

        var exception = await Assert.ThrowsAsync<QueryStoreDisabledException>(() =>
            collector.CollectTopSlowQueriesAsync(10, TimeSpan.FromHours(24), CancellationToken.None));

        Assert.Contains("ALTER DATABASE", exception.Message);
        Assert.Contains("QUERY_STORE = ON", exception.Message);
    }

    [Fact]
    public async Task CollectTopSlowQueriesAsync_ImpactScoreMapsCorrectly()
    {
        var connection = new FakeQueryStoreConnection("READ_WRITE");
        connection.AddQuery(1, "SELECT * FROM Users WHERE Id = @Id", 100, 5, 200, 5000, 200000);
        var collector = CreateCollector(connection);

        var snapshots = await collector.CollectTopSlowQueriesAsync(
            10, TimeSpan.FromHours(24), CancellationToken.None);

        var snapshot = Assert.Single(snapshots);
        Assert.Equal(1L, snapshot.QueryId);
        Assert.Equal("SELECT * FROM Users WHERE Id = @Id", snapshot.SqlText);
        Assert.Equal(100L, snapshot.ExecCount);
        Assert.Equal(5.0, snapshot.AvgDurationMs, 2);
        Assert.Equal(200.0, snapshot.TotalCpuMs, 2);
        Assert.Equal(200000.0, snapshot.ImpactScore, 2);
        Assert.Equal(5000L, snapshot.TotalLogicalReads);
        Assert.NotEmpty(snapshot.NormalizedSqlHash);
    }

    private static QueryStoreCollector CreateCollector(FakeQueryStoreConnection connection) =>
        new(Options.Create(new QueryStoreOptions()), () => connection);

    private sealed class FakeQueryStoreConnection : DbConnection
    {
        private readonly string _state;
        private readonly DataTable _rows = new();

        public FakeQueryStoreConnection(string state)
        {
            _state = state;
            _rows.Columns.Add("QueryId", typeof(long));
            _rows.Columns.Add("SqlText", typeof(string));
            _rows.Columns.Add("ExecCount", typeof(long));
            _rows.Columns.Add("AvgDurationMs", typeof(double));
            _rows.Columns.Add("TotalCpuMs", typeof(double));
            _rows.Columns.Add("TotalLogicalReads", typeof(long));
            _rows.Columns.Add("ImpactScore", typeof(double));
            _rows.Columns.Add("ExecutionPlanXml", typeof(string));
            _rows.Columns.Add("LastExecutionUtc", typeof(DateTime));
        }

        public void AddQuery(long queryId, string sqlText, long execCount, double avgDurationMs,
            double totalCpuMs, long logicalReads, double impactScore) =>
            _rows.Rows.Add(queryId, sqlText, execCount, avgDurationMs, totalCpuMs,
                logicalReads, impactScore, DBNull.Value, DateTime.UtcNow);

        private string _connectionString = string.Empty;
        public override string ConnectionString { get => _connectionString; [param: AllowNull] set => _connectionString = value ?? string.Empty; }
        public override string Database => "FakeRadarDb";
        public override string DataSource => "FakeQueryStore";
        public override string ServerVersion => "1.0";
        public override ConnectionState State { get; } = ConnectionState.Open;
        public override void ChangeDatabase(string databaseName) { }
        public override void Close() { }
        public override void Open() { }
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => throw new NotSupportedException();
        protected override DbCommand CreateDbCommand() => new FakeQueryStoreCommand(this, _state, _rows);
    }

    private sealed class FakeQueryStoreCommand : DbCommand
    {
        private readonly string _state;
        private readonly DataTable _rows;
        private readonly FakeParameterCollection _parameters = new();

        public FakeQueryStoreCommand(DbConnection connection, string state, DataTable rows)
        {
            DbConnection = connection;
            _state = state;
            _rows = rows;
        }

        private string _commandText = string.Empty;
        public override string CommandText { get => _commandText; [param: AllowNull] set => _commandText = value ?? string.Empty; }
        public override int CommandTimeout { get; set; }
        public override CommandType CommandType { get; set; } = CommandType.Text;
        public override bool DesignTimeVisible { get; set; }
        public override UpdateRowSource UpdatedRowSource { get; set; }
        protected override DbConnection? DbConnection { get; set; }
        protected override DbParameterCollection DbParameterCollection => _parameters;
        protected override DbTransaction? DbTransaction { get; set; }
        public override void Cancel() { }
        public override int ExecuteNonQuery() => 0;
        public override object? ExecuteScalar() => _state;
        public override void Prepare() { }
        protected override DbParameter CreateDbParameter() => new FakeParameter();
        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
        {
            if (_commandText.Contains("actual_state_desc", StringComparison.OrdinalIgnoreCase))
            {
                var state = new DataTable();
                state.Columns.Add("actual_state_desc", typeof(string));
                state.Rows.Add(_state);
                return state.CreateDataReader();
            }

            return _rows.CreateDataReader();
        }
    }

    private sealed class FakeParameter : DbParameter
    {
        public override DbType DbType { get; set; }
        public override ParameterDirection Direction { get; set; } = ParameterDirection.Input;
        public override bool IsNullable { get; set; }
        private string _parameterName = string.Empty;
        private string _sourceColumn = string.Empty;
        public override string ParameterName { get => _parameterName; [param: AllowNull] set => _parameterName = value ?? string.Empty; }
        public override string SourceColumn { get => _sourceColumn; [param: AllowNull] set => _sourceColumn = value ?? string.Empty; }
        public override object? Value { get; set; }
        public override bool SourceColumnNullMapping { get; set; }
        public override int Size { get; set; }
        public override void ResetDbType() { }
    }

    private sealed class FakeParameterCollection : DbParameterCollection
    {
        private readonly List<DbParameter> _items = new();
        public override int Count => _items.Count;
        public override object SyncRoot => ((ICollection)_items).SyncRoot!;
        public override int Add(object value) { _items.Add((DbParameter)value); return _items.Count - 1; }
        public override void AddRange(Array values) { foreach (var value in values) Add(value!); }
        public override void Clear() => _items.Clear();
        public override bool Contains(object value) => _items.Contains((DbParameter)value);
        public override bool Contains(string value) => IndexOf(value) >= 0;
        public override void CopyTo(Array array, int index) => ((ICollection)_items).CopyTo(array, index);
        public override IEnumerator GetEnumerator() => _items.GetEnumerator();
        public override int IndexOf(object value) => _items.IndexOf((DbParameter)value);
        public override int IndexOf(string parameterName) => _items.FindIndex(p => p.ParameterName == parameterName);
        public override void Insert(int index, object value) => _items.Insert(index, (DbParameter)value);
        public override void Remove(object value) => _items.Remove((DbParameter)value);
        public override void RemoveAt(int index) => _items.RemoveAt(index);
        public override void RemoveAt(string parameterName) => RemoveAt(IndexOf(parameterName));
        protected override DbParameter GetParameter(int index) => _items[index];
        protected override DbParameter GetParameter(string parameterName) => _items[IndexOf(parameterName)];
        protected override void SetParameter(int index, DbParameter value) => _items[index] = value;
        protected override void SetParameter(string parameterName, DbParameter value) => _items[IndexOf(parameterName)] = value;
    }
}