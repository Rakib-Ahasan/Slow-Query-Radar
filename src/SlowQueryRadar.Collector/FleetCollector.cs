//src/SlowQueryRadar.Collector/FleetCollector.cs
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SlowQueryRadar.Core;
using SlowQueryRadar.Core.Abstractions;
using SlowQueryRadar.Core.Models;

namespace SlowQueryRadar.Collector;

/// <summary>Collects query metrics across multiple monitoring targets.</summary>
public sealed class FleetCollector : IFleetCollector
{
    private readonly ITargetProvider _targetProvider;
    private readonly MonitoringOptions _monitoring;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<FleetCollector> _logger;
    private readonly Func<MonitoringTarget, IQueryStoreCollector> _collectorFactory;

    /// <summary>Initializes a fleet collector.</summary>
    public FleetCollector(
        ITargetProvider targetProvider,
        IOptions<MonitoringOptions> monitoring,
        ILoggerFactory? loggerFactory,
        ILogger<FleetCollector> logger)
    {
        ArgumentNullException.ThrowIfNull(targetProvider);
        ArgumentNullException.ThrowIfNull(monitoring);
        ArgumentNullException.ThrowIfNull(logger);

        _targetProvider = targetProvider;
        _monitoring = monitoring.Value;
        _loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
        _logger = logger;
        _collectorFactory = target => CreateCollector(target.ConnectionString);
    }

    /// <summary>Initializes a fleet collector with a custom per-target collector factory.</summary>
    public FleetCollector(
        ITargetProvider targetProvider,
        IOptions<MonitoringOptions> monitoring,
        ILoggerFactory loggerFactory,
        ILogger<FleetCollector> logger,
        Func<MonitoringTarget, IQueryStoreCollector> collectorFactory)
        : this(targetProvider, monitoring, loggerFactory, logger)
    {
        ArgumentNullException.ThrowIfNull(collectorFactory);
        _collectorFactory = collectorFactory;
    }

    /// <inheritdoc />
    public async Task<FleetCollectResult> CollectAsync(CollectOptions options, string? group, string? tag, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        var targets = await _targetProvider.GetTargetsAsync(ct).ConfigureAwait(false);
        var filteredTargets = targets
            .Where(target => target.Enabled)
            .Where(target => string.IsNullOrWhiteSpace(group) || string.Equals(target.Group, group, StringComparison.OrdinalIgnoreCase))
            .Where(target => string.IsNullOrWhiteSpace(tag) || target.Tags.Any(value => string.Equals(value, tag, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        var maxParallel = _monitoring.MaxParallelTargets > 0 ? _monitoring.MaxParallelTargets : 4;
        var timeoutSeconds = _monitoring.PerTargetTimeoutSeconds > 0 ? _monitoring.PerTargetTimeoutSeconds : 60;
        using var gate = new SemaphoreSlim(maxParallel, maxParallel);

        var tasks = filteredTargets.Select(async target =>
        {
            await gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                return await CollectTargetAsync(target, options, timeoutSeconds, ct).ConfigureAwait(false);
            }
            finally
            {
                gate.Release();
            }
        }).ToArray();

        var results = await Task.WhenAll(tasks).ConfigureAwait(false);
        return new FleetCollectResult(results, DateTimeOffset.UtcNow);
    }

    private async Task<TargetResult> CollectTargetAsync(
        MonitoringTarget target,
        CollectOptions options,
        int timeoutSeconds,
        CancellationToken ct)
    {
        var started = DateTimeOffset.UtcNow;
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        try
        {
            var collector = _collectorFactory(target);
            var result = await collector.CollectAsync(options, timeoutCts.Token).ConfigureAwait(false);
            var queries = result.Queries.Select(query =>
            {
                query.TargetName = target.Name;
                query.Group = target.Group;
                return query;
            }).ToArray();

            return new TargetResult(
                target.Name,
                target.Group,
                TargetResultStatus.Ok,
                null,
                queries,
                DateTimeOffset.UtcNow - started);
        }
        catch (QueryStoreDisabledException exception)
        {
            _logger.LogWarning(exception, "Query Store disabled for target {TargetName}", target.Name);
            return new TargetResult(
                target.Name,
                target.Group,
                TargetResultStatus.QueryStoreOff,
                exception.Message,
                Array.Empty<SlowQuerySnapshot>(),
                DateTimeOffset.UtcNow - started);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new TargetResult(
                target.Name,
                target.Group,
                TargetResultStatus.Failed,
                $"Timed out after {timeoutSeconds} seconds.",
                Array.Empty<SlowQuerySnapshot>(),
                DateTimeOffset.UtcNow - started);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Target {TargetName} collection failed", target.Name);
            return new TargetResult(
                target.Name,
                target.Group,
                TargetResultStatus.Failed,
                exception.Message,
                Array.Empty<SlowQuerySnapshot>(),
                DateTimeOffset.UtcNow - started);
        }
    }

    private QueryStoreCollector CreateCollector(string connectionString)
    {
        var collectorOptions = new QueryStoreOptions
        {
            ConnectionString = connectionString,
            CommandTimeoutSeconds = _monitoring.PerTargetTimeoutSeconds > 0 && _monitoring.PerTargetTimeoutSeconds <= 300
                ? _monitoring.PerTargetTimeoutSeconds
                : 60
        };

        return new QueryStoreCollector(
            Options.Create(collectorOptions),
            null,
            _loggerFactory.CreateLogger<QueryStoreCollector>());
    }
}
