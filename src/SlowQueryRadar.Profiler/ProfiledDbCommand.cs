//src/SlowQueryRadar.Profiler/ProfiledDbCommand.cs
#pragma warning disable CS8765
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using SlowQueryRadar.Core.Models;
using SlowQueryRadar.Profiler.Sinks;

namespace SlowQueryRadar.Profiler;

/// <summary>Delegates a database command while recording execution performance.</summary>
public sealed class ProfiledDbCommand : DbCommand
{
    private readonly DbCommand _inner;
    private readonly ProfiledDbConnection _connectionOwner;
    private readonly IPerformanceSink _sink;
    private readonly ProfilerOptions _options;

    /// <summary>Initializes a profiled command wrapper.</summary>
    /// <param name="inner">The provider command to wrap.</param>
    /// <param name="connection">The owning profiled connection.</param>
    /// <param name="sink">The performance sink.</param>
    /// <param name="options">Profiler options.</param>
    public ProfiledDbCommand(
        DbCommand inner,
        ProfiledDbConnection connection,
        IPerformanceSink sink,
        ProfilerOptions? options = null)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _connectionOwner = connection ?? throw new ArgumentNullException(nameof(connection));
        _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        _options = options ?? new ProfilerOptions();
    }

    /// <inheritdoc />
    public override string CommandText
    {
        get => _inner.CommandText;
        [param: AllowNull] set => _inner.CommandText = value ?? string.Empty;
    }

    /// <inheritdoc />
    public override int CommandTimeout
    {
        get => _inner.CommandTimeout;
        set => _inner.CommandTimeout = value;
    }

    /// <inheritdoc />
    public override CommandType CommandType
    {
        get => _inner.CommandType;
        set => _inner.CommandType = value;
    }

    /// <inheritdoc />
    public override bool DesignTimeVisible
    {
        get => _inner.DesignTimeVisible;
        set => _inner.DesignTimeVisible = value;
    }

    /// <inheritdoc />
    public override UpdateRowSource UpdatedRowSource
    {
        get => _inner.UpdatedRowSource;
        set => _inner.UpdatedRowSource = value;
    }

    /// <inheritdoc />
    protected override DbConnection? DbConnection
    {
        get => _connectionOwner;
        set => _inner.Connection = value is ProfiledDbConnection profiled ? profiled.InnerConnection : value;
    }

    /// <inheritdoc />
    protected override DbTransaction? DbTransaction
    {
        get => _inner.Transaction;
        set => _inner.Transaction = value;
    }

    /// <inheritdoc />
    protected override DbParameterCollection DbParameterCollection => _inner.Parameters;

    /// <inheritdoc />
    public override void Cancel() => _inner.Cancel();

    /// <inheritdoc />
    public override void Prepare() => _inner.Prepare();

    /// <inheritdoc />
    protected override DbParameter CreateDbParameter() => _inner.CreateParameter();

    /// <inheritdoc />
    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) =>
        ExecuteMeasured(() => _inner.ExecuteReader(behavior));

    /// <inheritdoc />
    protected override async Task<DbDataReader> ExecuteDbDataReaderAsync(
        CommandBehavior behavior, CancellationToken cancellationToken)
    {
        return await ExecuteMeasuredAsync(
            () => _inner.ExecuteReaderAsync(behavior, cancellationToken), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override int ExecuteNonQuery() => ExecuteMeasured(_inner.ExecuteNonQuery);

    /// <inheritdoc />
    public override async Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken)
    {
        return await ExecuteMeasuredAsync(
            () => _inner.ExecuteNonQueryAsync(cancellationToken), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override object? ExecuteScalar() => ExecuteMeasured(_inner.ExecuteScalar);

    /// <inheritdoc />
    public override async Task<object?> ExecuteScalarAsync(CancellationToken cancellationToken)
    {
        return await ExecuteMeasuredAsync(
            () => _inner.ExecuteScalarAsync(cancellationToken), cancellationToken).ConfigureAwait(false);
    }

    private T ExecuteMeasured<T>(Func<T> execute)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = execute();
            stopwatch.Stop();
            WriteEntry(stopwatch.Elapsed.TotalMilliseconds, true, null);
            return result;
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            WriteEntry(stopwatch.Elapsed.TotalMilliseconds, false, exception.Message);
            throw;
        }
    }

    private async Task<T> ExecuteMeasuredAsync<T>(Func<Task<T>> execute, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = await execute().ConfigureAwait(false);
            stopwatch.Stop();
            WriteEntry(stopwatch.Elapsed.TotalMilliseconds, true, null);
            return result;
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            WriteEntry(stopwatch.Elapsed.TotalMilliseconds, false, exception.Message);
            throw;
        }
    }

    private void WriteEntry(double durationMs, bool success, string? exceptionMessage)
    {
        var caller = CallerContext.Current.Value ?? FindCaller();
        var sqlText = CommandText ?? string.Empty;
        var entry = new DapperLogEntry
        {
            SqlHash = SqlNormalizer.ComputeHashFromSql(sqlText),
            SqlText = sqlText,
            ParametersJson = SerializeParameters(),
            DurationMs = durationMs,
            CallerMemberName = caller?.MemberName,
            CallerFilePath = caller?.FilePath,
            CallerLineNumber = caller?.LineNumber,
            Success = success,
            ExceptionMessage = exceptionMessage,
            TimestampUtc = DateTime.UtcNow
        };

        _sink.Write(entry);
    }

    private string? SerializeParameters()
    {
        var parameters = _inner.Parameters.Cast<DbParameter>().ToList();
        if (parameters.Count == 0)
        {
            return null;
        }

        if (!_options.LogParameterValues)
        {
            return JsonSerializer.Serialize(parameters.Select(parameter => parameter.ParameterName));
        }

        return JsonSerializer.Serialize(parameters.ToDictionary(
            parameter => parameter.ParameterName,
            parameter => parameter.Value is DBNull ? null : parameter.Value));
    }

    private static CallerInfo? FindCaller()
    {
        foreach (var frame in new StackTrace().GetFrames() ?? Array.Empty<StackFrame>())
        {
            var method = frame.GetMethod();
            var declaringType = method?.DeclaringType;
            var namespaceName = declaringType?.Namespace ?? string.Empty;
            if (declaringType is null || namespaceName.StartsWith("System.Data", StringComparison.Ordinal)
                || namespaceName.StartsWith("Dapper", StringComparison.Ordinal)
                || namespaceName.StartsWith("SlowQueryRadar.Profiler", StringComparison.Ordinal))
            {
                continue;
            }

            return new CallerInfo(
                method!.Name,
                frame.GetFileName() ?? declaringType.Assembly.Location,
                frame.GetFileLineNumber());
        }

        return null;
    }

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
