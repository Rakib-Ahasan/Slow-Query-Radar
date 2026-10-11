#!/bin/bash
set -e

SLN="SlowQueryRadar.slnx"

echo "▶ Adding remaining projects to solution..."
dotnet sln "$SLN" add src/SlowQueryRadar.Profiler/SlowQueryRadar.Profiler.csproj
dotnet sln "$SLN" add src/SlowQueryRadar.Analyzer/SlowQueryRadar.Analyzer.csproj
dotnet sln "$SLN" add src/SlowQueryRadar.Suggestions/SlowQueryRadar.Suggestions.csproj
dotnet sln "$SLN" add src/SlowQueryRadar.Benchmark/SlowQueryRadar.Benchmark.csproj
dotnet sln "$SLN" add src/SlowQueryRadar.Api/SlowQueryRadar.Api.csproj
dotnet sln "$SLN" add src/SlowQueryRadar.Cli/SlowQueryRadar.Cli.csproj
dotnet sln "$SLN" add tests/SlowQueryRadar.Tests.Unit/SlowQueryRadar.Tests.Unit.csproj
dotnet sln "$SLN" add tests/SlowQueryRadar.Tests.Integration/SlowQueryRadar.Tests.Integration.csproj

CORE=src/SlowQueryRadar.Core/SlowQueryRadar.Core.csproj

echo "▶ Adding project references..."

# Profiler, Analyzer, Suggestions, Benchmark → Core
dotnet add src/SlowQueryRadar.Profiler/SlowQueryRadar.Profiler.csproj          reference $CORE
dotnet add src/SlowQueryRadar.Analyzer/SlowQueryRadar.Analyzer.csproj          reference $CORE
dotnet add src/SlowQueryRadar.Suggestions/SlowQueryRadar.Suggestions.csproj    reference $CORE
dotnet add src/SlowQueryRadar.Benchmark/SlowQueryRadar.Benchmark.csproj        reference $CORE

# Api → সব
dotnet add src/SlowQueryRadar.Api/SlowQueryRadar.Api.csproj reference $CORE
dotnet add src/SlowQueryRadar.Api/SlowQueryRadar.Api.csproj reference src/SlowQueryRadar.Collector/SlowQueryRadar.Collector.csproj
dotnet add src/SlowQueryRadar.Api/SlowQueryRadar.Api.csproj reference src/SlowQueryRadar.Profiler/SlowQueryRadar.Profiler.csproj
dotnet add src/SlowQueryRadar.Api/SlowQueryRadar.Api.csproj reference src/SlowQueryRadar.Analyzer/SlowQueryRadar.Analyzer.csproj
dotnet add src/SlowQueryRadar.Api/SlowQueryRadar.Api.csproj reference src/SlowQueryRadar.Suggestions/SlowQueryRadar.Suggestions.csproj
dotnet add src/SlowQueryRadar.Api/SlowQueryRadar.Api.csproj reference src/SlowQueryRadar.Benchmark/SlowQueryRadar.Benchmark.csproj

# Cli → Core, Collector, Analyzer, Suggestions, Benchmark
dotnet add src/SlowQueryRadar.Cli/SlowQueryRadar.Cli.csproj reference $CORE
dotnet add src/SlowQueryRadar.Cli/SlowQueryRadar.Cli.csproj reference src/SlowQueryRadar.Collector/SlowQueryRadar.Collector.csproj
dotnet add src/SlowQueryRadar.Cli/SlowQueryRadar.Cli.csproj reference src/SlowQueryRadar.Analyzer/SlowQueryRadar.Analyzer.csproj
dotnet add src/SlowQueryRadar.Cli/SlowQueryRadar.Cli.csproj reference src/SlowQueryRadar.Suggestions/SlowQueryRadar.Suggestions.csproj
dotnet add src/SlowQueryRadar.Cli/SlowQueryRadar.Cli.csproj reference src/SlowQueryRadar.Benchmark/SlowQueryRadar.Benchmark.csproj

# Test projects
dotnet add tests/SlowQueryRadar.Tests.Unit/SlowQueryRadar.Tests.Unit.csproj reference $CORE
dotnet add tests/SlowQueryRadar.Tests.Unit/SlowQueryRadar.Tests.Unit.csproj reference src/SlowQueryRadar.Profiler/SlowQueryRadar.Profiler.csproj
dotnet add tests/SlowQueryRadar.Tests.Unit/SlowQueryRadar.Tests.Unit.csproj reference src/SlowQueryRadar.Analyzer/SlowQueryRadar.Analyzer.csproj
dotnet add tests/SlowQueryRadar.Tests.Integration/SlowQueryRadar.Tests.Integration.csproj reference $CORE
dotnet add tests/SlowQueryRadar.Tests.Integration/SlowQueryRadar.Tests.Integration.csproj reference src/SlowQueryRadar.Collector/SlowQueryRadar.Collector.csproj

echo "▶ Adding NuGet packages (Collector)..."
dotnet add src/SlowQueryRadar.Collector package Dapper
dotnet add src/SlowQueryRadar.Collector package Microsoft.Data.SqlClient
dotnet add src/SlowQueryRadar.Collector package Serilog
dotnet add src/SlowQueryRadar.Collector package Microsoft.Extensions.Options.ConfigurationExtensions
dotnet add src/SlowQueryRadar.Collector package Microsoft.Extensions.DependencyInjection.Abstractions

echo "▶ Adding NuGet packages (Profiler)..."
dotnet add src/SlowQueryRadar.Profiler package Serilog
dotnet add src/SlowQueryRadar.Profiler package Microsoft.Extensions.Options
dotnet add src/SlowQueryRadar.Profiler package Microsoft.Extensions.DependencyInjection.Abstractions

echo "▶ Adding NuGet packages (Analyzer)..."
dotnet add src/SlowQueryRadar.Analyzer package Microsoft.SqlServer.TransactSql.ScriptDom
dotnet add src/SlowQueryRadar.Analyzer package Microsoft.Extensions.DependencyInjection.Abstractions

echo "▶ Adding NuGet packages (Suggestions)..."
dotnet add src/SlowQueryRadar.Suggestions package Microsoft.Extensions.Http
dotnet add src/SlowQueryRadar.Suggestions package Microsoft.Extensions.Options
dotnet add src/SlowQueryRadar.Suggestions package Microsoft.Extensions.DependencyInjection.Abstractions

echo "▶ Adding NuGet packages (Benchmark)..."
dotnet add src/SlowQueryRadar.Benchmark package Dapper
dotnet add src/SlowQueryRadar.Benchmark package Microsoft.Data.SqlClient
dotnet add src/SlowQueryRadar.Benchmark package Microsoft.Extensions.Options

echo "▶ Adding NuGet packages (Api)..."
dotnet add src/SlowQueryRadar.Api package MudBlazor
dotnet add src/SlowQueryRadar.Api package Serilog.AspNetCore
dotnet add src/SlowQueryRadar.Api package Serilog.Sinks.Console
dotnet add src/SlowQueryRadar.Api package Serilog.Sinks.File

echo "▶ Adding NuGet packages (Cli)..."
dotnet add src/SlowQueryRadar.Cli package System.CommandLine --prerelease
dotnet add src/SlowQueryRadar.Cli package Spectre.Console
dotnet add src/SlowQueryRadar.Cli package Microsoft.Extensions.Hosting
dotnet add src/SlowQueryRadar.Cli package Microsoft.Extensions.Configuration.Json

echo "▶ Adding NuGet packages (Tests)..."
dotnet add tests/SlowQueryRadar.Tests.Unit package Microsoft.Data.Sqlite
dotnet add tests/SlowQueryRadar.Tests.Unit package Moq
dotnet add tests/SlowQueryRadar.Tests.Integration package Microsoft.Data.SqlClient

echo "▶ Creating extra folders..."
mkdir -p .github
mkdir -p docs
mkdir -p src/SlowQueryRadar.Core/Models
mkdir -p src/SlowQueryRadar.Core/Abstractions
mkdir -p src/SlowQueryRadar.Core/Exceptions
mkdir -p src/SlowQueryRadar.Collector/Sql
mkdir -p src/SlowQueryRadar.Collector/Options
mkdir -p src/SlowQueryRadar.Profiler/Sinks
mkdir -p src/SlowQueryRadar.Analyzer/Plan
mkdir -p src/SlowQueryRadar.Analyzer/Rules
mkdir -p src/SlowQueryRadar.Suggestions/Deterministic
mkdir -p src/SlowQueryRadar.Suggestions/Ai
mkdir -p src/SlowQueryRadar.Benchmark/Sql
mkdir -p src/SlowQueryRadar.Api/Controllers
mkdir -p src/SlowQueryRadar.Api/Components/Pages
mkdir -p src/SlowQueryRadar.Cli/Commands
mkdir -p tests/SlowQueryRadar.Tests.Unit/Analyzer
mkdir -p tests/SlowQueryRadar.Tests.Unit/Profiler
mkdir -p tests/SlowQueryRadar.Tests.Unit/Fixtures/Plans


echo "▶ Removing default template files..."
rm -f src/SlowQueryRadar.Core/Class1.cs

rm -f src/SlowQueryRadar.Collector/Class1.cs
rm -f src/SlowQueryRadar.Profiler/Class1.cs
rm -f src/SlowQueryRadar.Analyzer/Class1.cs
rm -f src/SlowQueryRadar.Suggestions/Class1.cs
rm -f src/SlowQueryRadar.Benchmark/Class1.cs

rm -f tests/SlowQueryRadar.Tests.Unit/UnitTest1.cs
rm -f tests/SlowQueryRadar.Tests.Integration/UnitTest1.cs

echo "▶ Creating placeholder files..."
touch .github/copilot-instructions.md
touch docs/architecture.md
touch docs/rules.md

touch docs/demo-script.md
touch src/SlowQueryRadar.Collector/Sql/TopSlowQueries.sql
touch src/SlowQueryRadar.Benchmark/Sql/BenchmarkHistory.sql
touch tests/SlowQueryRadar.Tests.Unit/Fixtures/Plans/table-scan.sqlplan
touch tests/SlowQueryRadar.Tests.Unit/Fixtures/Plans/key-lookup.sqlplan
touch tests/SlowQueryRadar.Tests.Unit/Fixtures/Plans/implicit-conversion.sqlplan


echo "▶ Building..."
dotnet build

echo ""
echo "✅ Fixup complete!"
