//src/SlowQueryRadar.Core/QueryStoreDisabledException.cs
using System;

namespace SlowQueryRadar.Core;

/// <summary>
/// Exception thrown when Query Store is disabled.
/// </summary>
public class QueryStoreDisabledException : Exception
{
    /// <summary>Initializes a new instance of the exception.</summary>
    /// <param name="message">The exception message.</param>
    public QueryStoreDisabledException(string message) : base(message) { }
}