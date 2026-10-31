//src/SlowQueryRadar.Api/Services/CollectionScheduler.cs
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SlowQueryRadar.Collector;
using SlowQueryRadar.Core.Abstractions;
using SlowQueryRadar.Core.Models;

namespace SlowQueryRadar.Api.Services;

/// <summary>Runs periodic fleet collection and persists snapshots.</summary>
public sealed class CollectionScheduler : BackgroundService
{
    private readonly IFleetCollector _fleetCollector;
    private readonly ISnapshotStore _snapshotStore;
    private readonly IRegressionDetector _regressionDetector;
    private readonly INotifier _notifier;
    private readonly QueryStoreOptions _queryStore;
    private readonly MonitoringOptions _monitoring;
    private readonly SnapshotStoreOptions _snapshotOptions;
    private readonly ILogger<CollectionScheduler> _logger;
    private readonly SemaphoreSlim _runGate = new(1, 1);

    /// <summary>Initializes a collection scheduler.</summary>
    public CollectionScheduler(
        IFleetCollector fleetCollector,
        ISnapshotStore snapshotStore,
        IRegressionDetector regressionDetector,
        INotifier notifier,
        IOptions<QueryStoreOptions> queryStore,
        IOptions<MonitoringOptions> monitoring,
        IOptions<SnapshotStoreOptions> snapshotOptions,
        ILogger<CollectionScheduler> logger)
    {
        ArgumentNullException.ThrowIfNull(fleetCollector);
        ArgumentNullException.ThrowIfNull(snapshotStore);
        ArgumentNullException.ThrowIfNull(regressionDetector);
        ArgumentNullException.ThrowIfNull(notifier);
        ArgumentNullException.ThrowIfNull(queryStore);
        ArgumentNullException.ThrowIfNull(monitoring);
        ArgumentNullException.ThrowIfNull(snapshotOptions);
        ArgumentNullException.ThrowIfNull(logger);

        _fleetCollector = fleetCollector;
        _snapshotStore = snapshotStore;
        _regressionDetector = regressionDetector;
        _notifier = notifier;
        _queryStore = queryStore.Value;
        _monitoring = monitoring.Value;
        _snapshotOptions = snapshotOptions.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _snapshotStore.InitializeAsync(stoppingToken).ConfigureAwait(false);

        var intervalMinutes = _monitoring.IntervalMinutes > 0 ? _monitoring.IntervalMinutes : 15;
        await RunOnceGuardedAsync(stoppingToken).ConfigureAwait(false);

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(intervalMinutes));
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            await RunOnceGuardedAsync(stoppingToken).ConfigureAwait(false);
        }
    }

    /// <summary>Runs one guarded scheduler cycle without overlap.</summary>
    public async Task RunOnceGuardedAsync(CancellationToken ct)
    {
        if (!await _runGate.WaitAsync(0, ct).ConfigureAwait(false))
        {
            _logger.LogInformation("Collection scheduler skipped run because previous run is still in progress.");
            return;
        }

        try
        {
            await RunOnceAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _runGate.Release();
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        var startedUtc = DateTimeOffset.UtcNow;
        try
        {
            var options = new CollectOptions
            {
                Top = _queryStore.TopN > 0 ? _queryStore.TopN : 20,
                Lookback = TimeSpan.FromHours(_queryStore.LookbackHours > 0 ? _queryStore.LookbackHours : 24),
                RankBy = RankBy.TotalDuration,
                MinExecutions = 1,
                ExcludeNoise = true
            };

            var result = await _fleetCollector.CollectAsync(options, null, null, ct).ConfigureAwait(false);
            var duration = DateTimeOffset.UtcNow - startedUtc;
            await _snapshotStore.SaveRunAsync(startedUtc, duration, result, ct).ConfigureAwait(false);

            var regressions = await _regressionDetector.DetectAsync(null, ct).ConfigureAwait(false);
            await _notifier.NotifyAsync(regressions, ct).ConfigureAwait(false);

            var retentionDays = _snapshotOptions.RetentionDays > 0 ? _snapshotOptions.RetentionDays : 30;
            var deleted = await _snapshotStore.CleanupAsync(TimeSpan.FromDays(retentionDays), ct).ConfigureAwait(false);
            _logger.LogInformation("Collection scheduler saved run with {Targets} target results, detected {Regressions} regressions, and cleanup removed {DeletedRows} samples.", result.Results.Count, regressions.Count, deleted);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            _logger.LogInformation("Collection scheduler stopping.");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Collection scheduler run failed.");
        }
    }
}
