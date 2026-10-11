//src/SlowQueryRadar.Profiler/ProfiledDbConnection.cs
#pragma warning disable CS8765
using System.Data;
using System.Data.Common;
using SlowQueryRadar.Profiler.Sinks;

namespace SlowQueryRadar.Profiler;

/// <summary>Delegates database connection operations while profiling created commands.</summary>
public sealed class ProfiledDbConnection : DbConnection
{
    private readonly DbConnection _inner;
    private readonly IPerformanceSink _sink;
    private readonly ProfilerOptions _options;

    /// <summary>Initializes a profiled connection wrapper.</summary>
    /// <param name="inner">The provider connection to wrap.</param>
    /// <param name="sink">The sink receiving command performance entries.</param>
    /// <param name="options">Optional profiler options.</param>
    public ProfiledDbConnection(DbConnection inner, IPerformanceSink sink, ProfilerOptions? options = null)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        _options = options ?? new ProfilerOptions();
    }

    internal DbConnection InnerConnection => _inner;
    internal IPerformanceSink Sink => _sink;
    internal ProfilerOptions Options => _options;

    /// <inheritdoc />
    public override string ConnectionString
    {
        get => _inner.ConnectionString;
        set => _inner.ConnectionString = value;
    }

    /// <inheritdoc />
    public override string Database => _inner.Database;

    /// <inheritdoc />
    public override string DataSource => _inner.DataSource;

    /// <inheritdoc />
    public override string ServerVersion => _inner.ServerVersion;

    /// <inheritdoc />
    public override ConnectionState State => _inner.State;

    /// <inheritdoc />
    public override void ChangeDatabase(string databaseName) => _inner.ChangeDatabase(databaseName);

    /// <inheritdoc />
    public override void Open()
    {
        _inner.Open();
    }

    /// <inheritdoc />
    public override async Task OpenAsync(CancellationToken cancellationToken)
    {
        await _inner.OpenAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override void Close()
    {
        _inner.Close();
    }

    /// <inheritdoc />
    public override async Task CloseAsync()
    {
        await _inner.CloseAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) =>
        _inner.BeginTransaction(isolationLevel);

    /// <inheritdoc />
    protected override async ValueTask<DbTransaction> BeginDbTransactionAsync(
        IsolationLevel isolationLevel, CancellationToken cancellationToken)
    {
        return await _inner.BeginTransactionAsync(isolationLevel, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The provider returned no transaction.");
    }

    /// <inheritdoc />
    protected override DbCommand CreateDbCommand() =>
        new ProfiledDbCommand(_inner.CreateCommand(), this, _sink, _options);

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _inner.Dispose();
        }

        base.Dispose(disposing);
    }
}
