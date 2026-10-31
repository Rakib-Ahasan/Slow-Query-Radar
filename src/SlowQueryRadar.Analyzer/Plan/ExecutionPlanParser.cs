//src/SlowQueryRadar.Analyzer/Plan/ExecutionPlanParser.cs
using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using Serilog;

namespace SlowQueryRadar.Analyzer.Plan;

/// <summary>Parses selected SQL Server execution-plan information.</summary>
public sealed class ExecutionPlanParser
{
    private static readonly XNamespace Showplan = "http://schemas.microsoft.com/sqlserver/2004/07/showplan";
    /// <summary>Parses execution-plan XML without using XPath.</summary>
    /// <param name="xml">The execution-plan XML.</param>
    /// <returns>A parsed plan, or an empty plan for malformed or empty XML.</returns>
    public ParsedPlan Parse(ReadOnlySpan<char> xml)
    {
        if (xml.IsEmpty)
        {
            return ParsedPlan.Empty;
        }

        try
        {
            using var stringReader = new StringReader(xml.ToString());
            using var reader = XmlReader.Create(stringReader, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                IgnoreComments = true,
                IgnoreWhitespace = true
            });
            var document = XDocument.Load(reader, LoadOptions.None);
            var elements = document.Descendants().ToArray();
            var operators = elements
                .Where(element => NameIs(element, "RelOp"))
                .Select(ParseOperator)
                .ToArray();
            var missingIndexes = elements
                .Where(element => NameIs(element, "MissingIndexGroup"))
                .Select(ParseMissingIndex)
                .Where(suggestion => suggestion is not null)
                .Cast<MissingIndexSuggestion>()
                .ToArray();
            var hasImplicitConversion = elements.Any(ContainsImplicitConversion)
                || elements.Any(element => NameIs(element, "PlanAffectingConvert"));
            var memoryGrant = elements.FirstOrDefault(element => NameIs(element, "MemoryGrantInfo"));

            return new ParsedPlan(operators, missingIndexes, hasImplicitConversion)
            {
                GrantedMemoryKb = DoubleAttribute(memoryGrant, "GrantedMemoryKb"),
                MaxUsedMemoryKb = DoubleAttribute(memoryGrant, "MaxUsedMemoryKb")
            };
        }
        catch (XmlException exception)
        {
            Log.Warning(exception, "Malformed execution plan XML could not be parsed");
            return new ParsedPlan([], [], false) { IsMalformed = true, ParseError = exception.Message };
        }
        catch (InvalidOperationException exception)
        {
            Log.Warning(exception, "Execution plan XML could not be parsed");
            return new ParsedPlan([], [], false) { IsMalformed = true, ParseError = exception.Message };
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException)
        {
            Log.Warning(exception, "Execution plan XML could not be parsed");
            return new ParsedPlan([], [], false) { IsMalformed = true, ParseError = exception.Message };
        }
    }

    private static PlanOperator ParseOperator(XElement element)
    {
        var objectElement = element.Descendants().FirstOrDefault(child => NameIs(child, "Object"));
        var predicateElement = element.Descendants().FirstOrDefault(child => NameIs(child, "Predicate"));
        var runtime = element.Descendants().FirstOrDefault(child => NameIs(child, "RunTimeCountersPerThread"));
        var warnings = element.Descendants().Where(child => NameIs(child, "Warnings")).ToArray();
        return new PlanOperator(
            AttributeValue(element, "PhysicalOp") ?? string.Empty,
            AttributeValue(element, "LogicalOp") ?? string.Empty,
            DoubleAttribute(element, "EstimateRows"),
            DoubleAttribute(element, "EstimateCPU"),
            AttributeValue(objectElement, "Table"),
            AttributeValue(objectElement, "Index"),
            predicateElement?.DescendantsAndSelf()
                .SelectMany(predicate => predicate.Attributes())
                .FirstOrDefault(attribute => string.Equals(attribute.Name.LocalName, "ScalarString", StringComparison.OrdinalIgnoreCase))?.Value
                ?? predicateElement?.Value.Trim())
        {
            NodeId = AttributeValue(element, "NodeId"),
            ActualRows = DoubleAttribute(runtime, "ActualRows"),
            ActualExecutions = DoubleAttribute(runtime, "ActualExecutions"),
            EstimatedExecutions = Math.Max(1, DoubleAttribute(element, "EstimateExecutions")),
            HasSpill = warnings.Any(warning => warning.DescendantsAndSelf().Any(child => NameIs(child, "SpillToTempDb"))),
            HasPlanAffectingConvert = warnings.Any(warning => warning.DescendantsAndSelf().Any(child => NameIs(child, "PlanAffectingConvert"))),
            HasParallelismSkew = element.Descendants().Any(child => NameIs(child, "RunTimeCountersPerThread")
                && DoubleAttribute(child, "ActualRows") > 0
                && DoubleAttribute(child, "ActualRowsRead") > DoubleAttribute(child, "ActualRows") * 2)
        };
    }

    private static MissingIndexSuggestion? ParseMissingIndex(XElement group)
    {
        var missingIndex = group.Descendants().FirstOrDefault(element => NameIs(element, "MissingIndex"));
        if (missingIndex is null)
        {
            return null;
        }

        var equality = new List<string>();
        var inequality = new List<string>();
        var include = new List<string>();
        foreach (var columnGroup in missingIndex.Descendants().Where(element => NameIs(element, "ColumnGroup")))
        {
            var destination = (AttributeValue(columnGroup, "Usage") ?? string.Empty).ToUpperInvariant() switch
            {
                "EQUALITY" => equality,
                "INEQUALITY" => inequality,
                "INCLUDE" => include,
                _ => null
            };
            if (destination is null)
            {
                continue;
            }

            destination.AddRange(columnGroup.Descendants()
                .Where(element => NameIs(element, "Column"))
                .Select(element => AttributeValue(element, "Name"))
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name!));
        }

        return new MissingIndexSuggestion(
            AttributeValue(missingIndex, "Table") ?? string.Empty,
            equality,
            inequality,
            include,
            DoubleAttribute(group, "Impact"));
    }

    private static bool ContainsImplicitConversion(XElement element) =>
        element.Value.Contains("CONVERT_IMPLICIT", StringComparison.OrdinalIgnoreCase)
        || element.Attributes().Any(attribute =>
            attribute.Value.Contains("CONVERT_IMPLICIT", StringComparison.OrdinalIgnoreCase));

    private static bool NameIs(XElement element, string name) =>
        element.Name.Namespace == Showplan
        && string.Equals(element.Name.LocalName, name, StringComparison.Ordinal);

    private static string? AttributeValue(XElement? element, string name) =>
        element?.Attributes().FirstOrDefault(attribute =>
            string.Equals(attribute.Name.LocalName, name, StringComparison.OrdinalIgnoreCase))?.Value;

    private static double DoubleAttribute(XElement? element, string name) =>
        double.TryParse(AttributeValue(element, name), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0;
}
