//src/SlowQueryRadar.Analyzer/Rules/SelectStarRule.cs
using Microsoft.SqlServer.TransactSql.ScriptDom;
using SlowQueryRadar.Core.Abstractions;
using SlowQueryRadar.Core.Models;

namespace SlowQueryRadar.Analyzer.Rules;

/// <summary>Reports SELECT star projections.</summary>
public sealed class SelectStarRule : ISqlRule
{
    /// <inheritdoc />
    public string RuleId => "SQLR-006";

    /// <inheritdoc />
    public IEnumerable<Finding> Evaluate(SqlAnalysisContext context)
    {
        var visitor = new SelectStarVisitor();
        context.ParsedStatement.Accept(visitor);
        if (visitor.Found)
        {
            yield return new Finding(
                RuleId,
                Severity.Medium,
                "The query uses SELECT *.",
                "SelectStarExpression was found in the parsed query.",
                "List explicit columns to reduce IO and enable covering indexes.",
                0);
        }
    }

    private sealed class SelectStarVisitor : TSqlFragmentVisitor
    {
        public bool Found { get; private set; }

        public override void ExplicitVisit(SelectStarExpression node) => Found = true;
    }
}
