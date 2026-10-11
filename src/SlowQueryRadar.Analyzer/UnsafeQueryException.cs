//src/SlowQueryRadar.Analyzer/UnsafeQueryException.cs
namespace SlowQueryRadar.Analyzer;

/// <summary>Indicates that SQL could not be safely parsed for analysis.</summary>
public sealed class UnsafeQueryException : Exception
{
    /// <summary>Initializes an unsafe-query exception.</summary>
    /// <param name="message">The first SQL parse error message.</param>
    public UnsafeQueryException(string message) : base(message)
    {
    }
}
