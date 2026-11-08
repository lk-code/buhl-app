// Runs all tests with code coverage, creates an HTML report and a GitHub Markdown summary in coverage/report/
// and prints the total line coverage and the line coverage per project.
// Test projects and Program.cs are excluded from the coverage.
//
// Usage: dotnet run coverage.cs [-- [--no-build] [--threshold <percent>]]
//   --no-build             skips the build; the solution must already be built
//   --threshold <percent>  fails with exit code 1 if the total line coverage is below the given percentage;
//                          the check is skipped as long as there are no coverable lines

using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

const string ProjectPrefix = "LKCode.AddressManagement.";
const string NotAvailable = "n/a (no coverable lines)";
const string Usage = "Usage: dotnet run coverage.cs [-- [--no-build] [--threshold <percent>]]";

bool build = true;
decimal? threshold = null;
for (int index = 0; index < args.Length; index++)
{
    switch (args[index])
    {
        case "--no-build":
            build = false;
            break;
        case "--threshold":
            index++;
            if (index >= args.Length || !TryParsePercentage(args[index], out decimal minimum))
            {
                return WriteUsageError("--threshold expects a percentage between 0 and 100.");
            }

            threshold = minimum;
            break;
        default:
            return WriteUsageError($"Unknown argument: {args[index]}");
    }
}

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

if (build)
{
    exitCode = await RunDotnetAsync(root, "build", "AddressManagement.slnx");
    if (exitCode != 0)
    {
        return exitCode;
    }
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
    return CheckThreshold(threshold, coveredLines: 0, coverableLines: 0);
}

// ReportGenerator merges the reports of all test projects.
exitCode = await RunDotnetAsync(
    root,
    "tool",
    "run",
    "reportgenerator",
    $"-reports:{string.Join(';', reports)}",
    $"-targetdir:{reportDirectory}",
    "-reporttypes:Html;JsonSummary;MarkdownSummaryGithub",
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

JsonElement total = summary.RootElement.GetProperty("summary");

Console.WriteLine();
Console.WriteLine("Line coverage");
WriteCoverage("Total", FormatCoverage(total));

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

exitCode = CheckThreshold(
    threshold,
    total.GetProperty("coveredlines").GetInt32(),
    total.GetProperty("coverablelines").GetInt32());

Console.WriteLine();
Console.WriteLine($"HTML report: {Path.Combine(reportDirectory, "index.html")}");
return exitCode;

static bool TryParsePercentage(string text, out decimal percentage)
{
    return decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out percentage)
        && percentage <= 100;
}

static int WriteUsageError(string message)
{
    Console.Error.WriteLine(message);
    Console.Error.WriteLine(Usage);
    return 1;
}

// Compares the exact coverage, not the rounded value that is shown in the console.
static int CheckThreshold(decimal? threshold, int coveredLines, int coverableLines)
{
    if (threshold is not decimal minimum)
    {
        return 0;
    }

    string formattedMinimum = minimum.ToString("0.##", CultureInfo.InvariantCulture);
    Console.WriteLine();
    if (coverableLines == 0)
    {
        Console.WriteLine($"Minimum line coverage {formattedMinimum} %: skipped (no coverable lines)");
        return 0;
    }

    if (100m * coveredLines / coverableLines < minimum)
    {
        Console.Error.WriteLine($"Minimum line coverage {formattedMinimum} %: FAILED");
        return 1;
    }

    Console.WriteLine($"Minimum line coverage {formattedMinimum} %: OK");
    return 0;
}

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
