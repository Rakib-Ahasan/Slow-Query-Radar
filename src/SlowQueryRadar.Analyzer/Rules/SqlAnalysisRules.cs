//src/SlowQueryRadar.Analyzer/Rules/SqlAnalysisRules.cs
using System.Text.RegularExpressions;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using SlowQueryRadar.Core.Models;

namespace SlowQueryRadar.Analyzer.Rules;

/// <summary>Provides shared finding construction for SQL text rules.</summary>
public abstract class SqlAnalysisRule : IAnalysisRule
{
    /// <inheritdoc />
    public abstract string Id { get; }
    /// <inheritdoc />
    public abstract Severity Severity { get; }
    /// <inheritdoc />
    public abstract IEnumerable<Finding> Analyze(AnalysisContext context);

    protected Finding Finding(string title, string evidence, string why, string fix, Confidence confidence = Confidence.High) => new(Id, Severity, title, evidence, fix, 0)
    {
        WhyItMatters = why,
        Confidence = confidence,
        DocsLink = "docs/rules.md"
    };
}

/// <summary>Reports SELECT star projections.</summary>
public sealed class SelectStarAnalysisRule : SqlAnalysisRule
{
    /// <inheritdoc />
    public override string Id => "SQL-001";
    /// <inheritdoc />
    public override Severity Severity => Severity.Medium;
    /// <inheritdoc />
    public override IEnumerable<Finding> Analyze(AnalysisContext context)
    {
        var visitor = new SelectStarVisitor();
        context.ParsedStatement.Accept(visitor);
        if (visitor.Found)
            yield return Finding("SELECT * projection detected", "ScriptDom node SelectStarExpression", "Unneeded columns increase network and IO costs and make covering indexes harder.", "List the required columns explicitly.");
    }

    private sealed class SelectStarVisitor : TSqlFragmentVisitor
    {
        public bool Found { get; private set; }
        public override void ExplicitVisit(SelectStarExpression node) => Found = true;
    }
}

/// <summary>Reports functions applied to columns in predicates and leading wildcard LIKE patterns.</summary>
public sealed class NonSargableAnalysisRule : SqlAnalysisRule
{
    private static readonly Regex FunctionPattern = new(@"\b(?<function>YEAR|MONTH|DAY|LEFT|RIGHT|ISNULL|COALESCE|LOWER|UPPER|LTRIM|RTRIM|CAST|CONVERT)\s*\(\s*(?<column>\[[^]]+\]|[A-Za-z_][\w.]*)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex LikePattern = new(@"\bLIKE\s+(?:N)?'%[^']*'", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    /// <inheritdoc />
    public override string Id => "SQL-002";
    /// <inheritdoc />
    public override Severity Severity => Severity.Medium;
    /// <inheritdoc />
    public override IEnumerable<Finding> Analyze(AnalysisContext context)
    {
        var function = FunctionPattern.Match(context.SqlText);
        if (function.Success && Regex.IsMatch(context.SqlText, @"\bWHERE\b", RegexOptions.IgnoreCase))
            yield return Finding("Function applied to a filtered column", function.Value, "Wrapping a column in a function can prevent an index seek.", "Rewrite the predicate as a searchable range or normalize the value before comparison.");
        var like = LikePattern.Match(context.SqlText);
        if (like.Success)
            yield return Finding("Leading wildcard LIKE detected", like.Value, "A leading wildcard normally prevents a conventional index seek.", "Consider full-text search or a trailing-wildcard strategy.");
    }
}

/// <summary>Reports OR chains with several equality predicates.</summary>
public sealed class OrChainAnalysisRule : SqlAnalysisRule
{
    private static readonly Regex OrPattern = new(@"\bOR\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    /// <inheritdoc />
    public override string Id => "SQL-003";
    /// <inheritdoc />
    public override Severity Severity => Severity.Medium;
    /// <inheritdoc />
    public override IEnumerable<Finding> Analyze(AnalysisContext context)
    {
        var matches = OrPattern.Matches(context.SqlText);
        if (matches.Count >= 2)
            yield return Finding("Long OR predicate chain detected", $"ScriptDom-compatible SQL evidence: {matches.Count} OR operators", "Long disjunctions can produce unstable estimates and repeated predicate work.", "Compare an IN predicate or a UNION ALL rewrite after checking duplicate semantics.", Confidence.Medium);
    }
}

/// <summary>Reports likely scalar user-defined function calls.</summary>
public sealed class ScalarUdfAnalysisRule : SqlAnalysisRule
{
    private static readonly Regex UdfPattern = new(@"(?<!\.)\b(?<name>[A-Za-z_]\w*)\s*\([^)]*\)", RegexOptions.Compiled);
    private static readonly HashSet<string> BuiltIns = new(StringComparer.OrdinalIgnoreCase) { "YEAR", "MONTH", "DAY", "LEFT", "RIGHT", "ISNULL", "COALESCE", "LOWER", "UPPER", "LTRIM", "RTRIM", "CAST", "CONVERT", "COUNT", "SUM", "AVG", "MIN", "MAX" };
    /// <inheritdoc />
    public override string Id => "SQL-004";
    /// <inheritdoc />
    public override Severity Severity => Severity.High;
    /// <inheritdoc />
    public override IEnumerable<Finding> Analyze(AnalysisContext context)
    {
        var match = UdfPattern.Matches(context.SqlText).Cast<Match>().FirstOrDefault(candidate => !BuiltIns.Contains(candidate.Groups["name"].Value));
        if (match is not null)
            yield return Finding("Possible scalar UDF call detected", match.Value, "Scalar UDFs can execute once per row and inhibit optimizer transformations.", "Review whether the function can be inlined or replaced with a relational expression.", Confidence.Low);
    }
}

/// <summary>Reports cursor and WHILE-loop constructs.</summary>
public sealed class CursorLoopAnalysisRule : SqlAnalysisRule
{
    private static readonly Regex Pattern = new(@"\b(CURSOR|WHILE)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    /// <inheritdoc />
    public override string Id => "SQL-005";
    /// <inheritdoc />
    public override Severity Severity => Severity.Medium;
    /// <inheritdoc />
    public override IEnumerable<Finding> Analyze(AnalysisContext context)
    {
        var match = Pattern.Match(context.SqlText);
        if (match.Success)
            yield return Finding("Cursor or WHILE loop detected", match.Value, "Row-by-row control flow often scales worse than set-based operations.", "Evaluate a set-based rewrite and measure it with representative data.");
    }
}

/// <summary>Reports NOLOCK table hints.</summary>
public sealed class NoLockAnalysisRule : SqlAnalysisRule
{
    private static readonly Regex Pattern = new(@"\bNOLOCK\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    /// <inheritdoc />
    public override string Id => "SQL-006";
    /// <inheritdoc />
    public override Severity Severity => Severity.Medium;
    /// <inheritdoc />
    public override IEnumerable<Finding> Analyze(AnalysisContext context)
    {
        var match = Pattern.Match(context.SqlText);
        if (match.Success)
            yield return Finding("NOLOCK table hint detected", match.Value, "NOLOCK permits dirty and otherwise inconsistent reads; it is not a general performance fix.", "Use an appropriate isolation strategy and validate consistency requirements.");
    }
}
