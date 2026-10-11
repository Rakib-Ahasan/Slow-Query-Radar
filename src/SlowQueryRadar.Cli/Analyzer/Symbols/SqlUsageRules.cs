//src/SlowQueryRadar.Cli/Analyzer/Symbols/SqlUsageRules.cs
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.RegularExpressions;

namespace SlowQueryRadar.Cli.Analyzer.Symbols;

/// <summary>Contains syntax rules for common SQL performance issues.</summary>
public sealed class DapperSyncQueryRule : ISqlUsageRule
{
    /// <inheritdoc />
    public IEnumerable<ScanFinding> Analyze(SyntaxNode root, string filePath) =>
        root.DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Where(invocation => IsDapperMethod(invocation, "Query") || IsDapperMethod(invocation, "Execute"))
            .Where(invocation => !invocation.Expression.ToString().Contains("Async", StringComparison.Ordinal))
            .Select(invocation => Finding("DAPPER001", ScanSeverity.Warning, filePath, invocation, "Synchronous Dapper query detected; use the async API on an async-capable connection."));

    private static bool IsDapperMethod(InvocationExpressionSyntax invocation, string method) => invocation.Expression.ToString().EndsWith("." + method, StringComparison.Ordinal) || invocation.Expression.ToString().Contains("." + method + "<", StringComparison.Ordinal);
    private static ScanFinding Finding(string id, ScanSeverity severity, string path, SyntaxNode node, string message) => new(id, severity, path, node.GetLocation().GetLineSpan().StartLinePosition.Line + 1, message);
}

/// <summary>Detects SQL string concatenation passed to Dapper.</summary>
public sealed class SqlStringConcatenationRule : ISqlUsageRule
{
    /// <inheritdoc />
    public IEnumerable<ScanFinding> Analyze(SyntaxNode root, string filePath) =>
        root.DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Where(IsDapperSqlInvocation)
            .Where(invocation => invocation.ArgumentList.Arguments.Count > 0 && invocation.ArgumentList.Arguments[0].Expression is BinaryExpressionSyntax { RawKind: (int)Microsoft.CodeAnalysis.CSharp.SyntaxKind.AddExpression })
            .Select(invocation => Finding("SQL001", ScanSeverity.Error, filePath, invocation, "SQL is built by string concatenation; use parameters instead."));

    private static bool IsDapperSqlInvocation(InvocationExpressionSyntax invocation) => invocation.Expression.ToString().Contains(".Query", StringComparison.Ordinal) || invocation.Expression.ToString().Contains(".Execute", StringComparison.Ordinal);
    private static ScanFinding Finding(string id, ScanSeverity severity, string path, SyntaxNode node, string message) => new(id, severity, path, node.GetLocation().GetLineSpan().StartLinePosition.Line + 1, message);
}

/// <summary>Detects SELECT star SQL literals passed to Dapper.</summary>
public sealed class SelectStarRule : ISqlUsageRule
{
    /// <inheritdoc />
    public IEnumerable<ScanFinding> Analyze(SyntaxNode root, string filePath)
    {
        foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>().Where(IsDapperSqlInvocation))
        {
            var sqlArgument = invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression;
            if (sqlArgument is not null && CollectStringValues(sqlArgument, root).Any(ContainsSelectStar))
            {
                yield return Finding("SQLR-006-ROSLYN", ScanSeverity.Warning, filePath, invocation, "SELECT * was found in a SQL argument; select only the required columns.");
            }
        }
    }

    private static bool IsDapperSqlInvocation(InvocationExpressionSyntax invocation)
    {
        var methodName = invocation.Expression switch
        {
            MemberAccessExpressionSyntax { Name: GenericNameSyntax genericName } => genericName.Identifier.ValueText,
            MemberAccessExpressionSyntax { Name: IdentifierNameSyntax identifierName } => identifierName.Identifier.ValueText,
            _ => string.Empty
        };

        return methodName is "Query" or "QueryAsync" or "Execute" or "ExecuteAsync" or
            "QueryFirst" or "QueryFirstOrDefault" or "QuerySingle" or "QuerySingleOrDefault" or "QueryMultiple";
    }

    private static IEnumerable<string> CollectStringValues(ExpressionSyntax expression, SyntaxNode root)
    {
        foreach (var literal in expression.DescendantNodesAndSelf().OfType<LiteralExpressionSyntax>())
        {
            if (literal.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.StringLiteralExpression))
            {
                yield return literal.Token.ValueText;
            }
        }

        foreach (var interpolated in expression.DescendantNodesAndSelf().OfType<InterpolatedStringExpressionSyntax>())
        {
            foreach (var text in interpolated.Contents.OfType<InterpolatedStringTextSyntax>())
            {
                yield return text.TextToken.ValueText;
            }
        }

        foreach (var identifier in expression.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>())
        {
            var declaration = root.DescendantNodes().OfType<VariableDeclaratorSyntax>()
                .FirstOrDefault(variable => variable.Identifier.ValueText == identifier.Identifier.ValueText && IsConst(variable));

            if (declaration?.Initializer?.Value is ExpressionSyntax initializer)
            {
                foreach (var value in CollectStringValues(initializer, root))
                {
                    yield return value;
                }
            }
        }
    }

    private static bool ContainsSelectStar(string value)
    {
        var withoutComments = Regex.Replace(value, @"/\*.*?\*/|--[^\r\n]*", string.Empty, RegexOptions.Singleline);
        var normalized = new string(withoutComments.Where(character => !char.IsWhiteSpace(character)).ToArray());
        return normalized.Contains("SELECT*", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsConst(VariableDeclaratorSyntax variable)
    {
        return variable.Parent?.Parent is FieldDeclarationSyntax field &&
               field.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.ConstKeyword)) ||
               variable.Parent?.Parent is LocalDeclarationStatementSyntax local &&
               local.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.ConstKeyword));
    }

    private static ScanFinding Finding(string id, ScanSeverity severity, string path, SyntaxNode node, string message) => new(id, severity, path, node.GetLocation().GetLineSpan().StartLinePosition.Line + 1, message);
}

/// <summary>Detects long SQL literals without a visible command timeout.</summary>
public sealed class MissingCommandTimeoutRule : ISqlUsageRule
{
    /// <inheritdoc />
    public IEnumerable<ScanFinding> Analyze(SyntaxNode root, string filePath) =>
        root.DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Where(IsDapperSqlInvocation)
            .Where(invocation => invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression is LiteralExpressionSyntax literal && literal.Token.ValueText.Length > 200)
            .Where(invocation => !invocation.Ancestors().OfType<ObjectCreationExpressionSyntax>().Any(creation => creation.Type.ToString().Contains("CommandDefinition", StringComparison.Ordinal)))
            .Select(invocation => Finding("SQL003", ScanSeverity.Info, filePath, invocation, "Long SQL string has no visible CommandTimeout."));

    private static bool IsDapperSqlInvocation(InvocationExpressionSyntax invocation) => invocation.Expression.ToString().Contains(".Query", StringComparison.Ordinal) || invocation.Expression.ToString().Contains(".Execute", StringComparison.Ordinal);
    private static ScanFinding Finding(string id, ScanSeverity severity, string path, SyntaxNode node, string message) => new(id, severity, path, node.GetLocation().GetLineSpan().StartLinePosition.Line + 1, message);
}
