// Runs all tests with code coverage, creates an HTML report in coverage/report/
// and prints the total line coverage and the line coverage per project.
// Test projects and Program.cs are excluded from the coverage.
//
// Usage: dotnet run coverage.cs

using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

const string ProjectPrefix = "LKCode.AddressManagement.";
const string NotAvailable = "n/a (no coverable lines)";

string root = AppContext.GetData("EntryPointFileDirectoryPath") as string ?? Directory.GetCurrentDirectory();
string coverageDirectory = Path.Combine(root, "coverage");
string resultsDirectory = Path.Combine(coverageDirectory, "results");
string reportDirectory = Path.Combine(coverageDirectory, "report");

if (Directory.Exists(coverageDirectory))
{
    Directory.Delete(coverageDirectory, recursive: true);
}

int exitCode = await RunDotnetAsync(root, "tool", "restore");
if (exitCode != 0)
{
    return exitCode;
}

exitCode = await RunDotnetAsync(root, "build", "AddressManagement.slnx");
if (exitCode != 0)
{
    return exitCode;
}

// Each test project writes into its own results directory, so the reports do not overwrite each other.
foreach (string testProject in Directory.GetFiles(Path.Combine(root, "tests"), "*.csproj", SearchOption.AllDirectories)
    .Order(StringComparer.Ordinal))
{
    exitCode = await RunDotnetAsync(
        root,
        "test",
        "--project",
        testProject,
        "--no-build",
        "--results-directory",
        Path.Combine(resultsDirectory, Path.GetFileNameWithoutExtension(testProject)),
        "--coverlet",
        "--coverlet-output-format",
        "cobertura",
        "--coverlet-include",
        $"[{ProjectPrefix}*]*",
        "--coverlet-exclude",
        "[*Tests]*",
        "--coverlet-exclude-by-file",
        "**/Program.cs");
    if (exitCode != 0)
    {
        return exitCode;
    }
}

string[] reports = Directory.Exists(resultsDirectory)
    ? Directory.GetFiles(resultsDirectory, "*.cobertura*.xml", SearchOption.AllDirectories)
    : [];
if (reports.Length == 0)
{
    Console.WriteLine("No coverage data found.");
    return 0;
}

// ReportGenerator merges the reports of all test projects.
exitCode = await RunDotnetAsync(
    root,
    "tool",
    "run",
    "reportgenerator",
    $"-reports:{string.Join(';', reports)}",
    $"-targetdir:{reportDirectory}",
    "-reporttypes:Html;JsonSummary",
    "-verbosity:Warning");
if (exitCode != 0)
{
    return exitCode;
}

await using FileStream summaryStream = File.OpenRead(Path.Combine(reportDirectory, "Summary.json"));
using JsonDocument summary = await JsonDocument.ParseAsync(summaryStream);
var assemblies = summary.RootElement
    .GetProperty("coverage")
    .GetProperty("assemblies")
    .EnumerateArray()
    .ToDictionary(assembly => assembly.GetProperty("name").GetString() ?? string.Empty, StringComparer.Ordinal);

Console.WriteLine();
Console.WriteLine("Line coverage");
WriteCoverage("Total", FormatCoverage(summary.RootElement.GetProperty("summary")));

foreach (string projectDirectory in Directory.GetDirectories(Path.Combine(root, "src")).Order(StringComparer.Ordinal))
{
    string assemblyName = Path.GetFileName(projectDirectory);
    string displayName = assemblyName.StartsWith(ProjectPrefix, StringComparison.Ordinal)
        ? assemblyName[ProjectPrefix.Length..]
        : assemblyName;
    WriteCoverage(
        displayName,
        assemblies.TryGetValue(assemblyName, out JsonElement assembly) ? FormatCoverage(assembly) : NotAvailable);
}

Console.WriteLine();
Console.WriteLine($"HTML report: {Path.Combine(reportDirectory, "index.html")}");
return 0;

static async Task<int> RunDotnetAsync(string workingDirectory, params string[] arguments)
{
    var startInfo = new ProcessStartInfo("dotnet", arguments)
    {
        WorkingDirectory = workingDirectory,
    };
    using Process process = Process.Start(startInfo)
        ?? throw new InvalidOperationException("The dotnet process could not be started.");
    await process.WaitForExitAsync();
    return process.ExitCode;
}

// Rounds down, so that coverage below 100 % is never shown as 100 %.
static string FormatCoverage(JsonElement element)
{
    int coverableLines = element.GetProperty("coverablelines").GetInt32();
    if (coverableLines == 0)
    {
        return NotAvailable;
    }

    int coveredLines = element.GetProperty("coveredlines").GetInt32();
    double percentage = Math.Floor(1000d * coveredLines / coverableLines) / 10d;
    return string.Create(CultureInfo.InvariantCulture, $"{percentage:0.0} % ({coveredLines}/{coverableLines})");
}

static void WriteCoverage(string name, string coverage)
{
    Console.WriteLine($"  {name,-16}{coverage}");
}
