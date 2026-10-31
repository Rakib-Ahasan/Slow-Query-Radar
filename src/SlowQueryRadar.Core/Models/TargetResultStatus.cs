//src/SlowQueryRadar.Core/Models/TargetResultStatus.cs
namespace SlowQueryRadar.Core.Models;

/// <summary>Represents the status of one target collection run.</summary>
public enum TargetResultStatus
{
    /// <summary>Collection succeeded.</summary>
    Ok,
    /// <summary>Collection failed due to an error.</summary>
    Failed,
    /// <summary>Query Store is disabled on the target database.</summary>
    QueryStoreOff
}
