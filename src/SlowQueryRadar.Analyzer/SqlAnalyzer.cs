//src/SlowQueryRadar.Analyzer/SqlAnalyzer.cs
using Microsoft.SqlServer.TransactSql.ScriptDom;
using SlowQueryRadar.Core.Abstractions;
using SlowQueryRadar.Core.Models;
using SlowQueryRadar.Analyzer.Rules;
using System.Text.RegularExpressions;

namespace SlowQueryRadar.Analyzer;

/// <summary>Analyzes SQL text, execution plans, and recent profiler entries.</summary>
public sealed class SqlAnalyzer
{
    private static readonly Regex ParameterPrefixRegex = new(
        @"^\s*\((?<parameters>.*?)\)\s*(?=(?:SELECT|WITH|INSERT|UPDATE|DELETE|MERGE|EXEC|DECLARE)\b)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private readonly IReadOnlyList<ISqlRule> _rules;

    /// <summary>Initializes an analyzer with the configured SQL rules.</summary>
    /// <param name="rules">The rules to execute.</param>
    public SqlAnalyzer(IEnumerable<ISqlRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        _rules = rules.ToArray();
    }

    /// <summary>Parses SQL into statement fragments using TSql170Parser.</summary>
    /// <param name="sql">The SQL text to parse.</param>
    /// <returns>The parsed statement fragments.</returns>
    public static IReadOnlyList<TSqlFragment> ParseSql(string sql)
    {
        ArgumentNullException.ThrowIfNull(sql);
        var parser = new TSql170Parser(initialQuotedIdentifiers: true);
        var transformedSql = TransformParameterPrefix(sql);
        using var reader = new StringReader(transformedSql);
        var fragment = parser.Parse(reader, out IList<ParseError> errors);
        if (errors.Count > 0)
        {
            var preview = sql.Length <= 200 ? sql : sql[..200];
            throw new UnsafeQueryException($"{errors[0].Message} SQL: {preview}");
        }

        if (fragment is TSqlScript script)
        {
            return script.Batches
                .SelectMany(batch => batch.Statements.Cast<TSqlFragment>())
                .ToArray();
        }

        return [fragment];
    }

    private static string TransformParameterPrefix(string sql)
    {
        var match = ParameterPrefixRegex.Match(sql);
        if (!match.Success || string.IsNullOrWhiteSpace(match.Groups["parameters"].Value))
        {
            return sql;
        }

        var declarations = match.Groups["parameters"].Value
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(parameter => parameter.Trim())
            .ToArray();
        if (declarations.Length == 0 || declarations.Any(parameter =>
                !Regex.IsMatch(parameter, @"^@\w+\s+.+$", RegexOptions.CultureInvariant)))
        {
            return sql;
        }

        var declareBlock = string.Join(
            Environment.NewLine,
            declarations.Select(parameter => $"DECLARE {parameter};"));
        return declareBlock + Environment.NewLine + sql[match.Length..];
    }

    /// <summary>Analyzes a snapshot synchronously using all configured rules.</summary>
    /// <param name="snapshot">The slow-query snapshot.</param>
    /// <param name="recentLogs">Recent Dapper profiler logs.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The ordered analysis result.</returns>
    public AnalysisResult Analyze(
        SlowQuerySnapshot snapshot,
        IReadOnlyList<DapperLogEntry> recentLogs,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(recentLogs);
        ct.ThrowIfCancellationRequested();

        var statements = ParseSql(snapshot.SqlText);
        if (statements.Count == 0)
        {
            throw new UnsafeQueryException("No SQL statement was found.");
        }

        var parsedStatement = statements.FirstOrDefault(statement => statement is not DeclareVariableStatement)
            ?? statements[0];
        var context = new SqlAnalysisContext(snapshot.SqlText, parsedStatement, snapshot.ExecutionPlanXml);
        var findings = new List<Finding>();
        foreach (var rule in _rules)
        {
            ct.ThrowIfCancellationRequested();
            if (rule is NPlus1Rule nPlus1Rule)
            {
                findings.AddRange(nPlus1Rule.Evaluate(new DapperLogBatch(recentLogs)));
            }
            else
            {
                findings.AddRange(rule.Evaluate(context));
            }
        }

        var ordered = findings
            .OrderByDescending(finding => finding.Severity)
            .ThenByDescending(finding => finding.EstimatedImpactPercent)
            .ToArray();
        return new AnalysisResult(snapshot.QueryId, ordered, DateTime.UtcNow);
    }

    /// <summary>Analyzes a snapshot asynchronously for future asynchronous rule support.</summary>
    /// <param name="snapshot">The slow-query snapshot.</param>
    /// <param name="recentLogs">Recent Dapper profiler logs.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task containing the analysis result.</returns>
    public Task<AnalysisResult> AnalyzeAsync(
        SlowQuerySnapshot snapshot,
        IReadOnlyList<DapperLogEntry> recentLogs,
        CancellationToken ct) =>
        Task.FromResult(Analyze(snapshot, recentLogs, ct));
}
