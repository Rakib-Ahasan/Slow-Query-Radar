//src/SlowQueryRadar.Profiler/CallerContext.cs
using System.Runtime.CompilerServices;

namespace SlowQueryRadar.Profiler;

/// <summary>Describes the source location that initiated a profiled database call.</summary>
public sealed record CallerInfo(string MemberName, string FilePath, int LineNumber);

/// <summary>Stores caller information across asynchronous execution flows.</summary>
public static class CallerContext
{
    /// <summary>Gets the caller information for the current asynchronous flow.</summary>
    public static AsyncLocal<CallerInfo?> Current { get; } = new();

    /// <summary>Begins a caller scope and restores the previous caller when disposed.</summary>
    /// <param name="member">The caller member name.</param>
    /// <param name="file">The caller file path.</param>
    /// <param name="line">The caller line number.</param>
    /// <returns>A scope that restores the previous caller.</returns>
    public static IDisposable BeginCall(string member, string file, int line)
    {
        var previous = Current.Value;
        Current.Value = new CallerInfo(member, file, line);
        return new CallerScope(previous);
    }

    private sealed class CallerScope(CallerInfo? previous) : IDisposable
    {
        public void Dispose() => Current.Value = previous;
    }
}

/// <summary>Provides caller tracking for Dapper calls.</summary>
public static class CallerContextExtensions
{
    /// <summary>Begins a caller scope for a Dapper operation.</summary>
    /// <param name="_">The receiver used to invoke the extension.</param>
    /// <param name="member">The compiler-supplied caller member name.</param>
    /// <param name="file">The compiler-supplied caller file path.</param>
    /// <param name="line">The compiler-supplied caller line number.</param>
    /// <returns>A scope that restores the previous caller.</returns>
    public static IDisposable BeginDapperCall(
        this object _,
        [CallerMemberName] string member = "",
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0) =>
        CallerContext.BeginCall(member, file, line);
}
