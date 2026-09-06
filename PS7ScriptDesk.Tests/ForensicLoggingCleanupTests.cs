using PS7ScriptDesk.Application.Diagnostics;
using PS7ScriptDesk.PowerShell.Services;

namespace PS7ScriptDesk.Tests;

public sealed class ForensicLoggingCleanupTests
{
    [Fact]
    public void InvestigationOnlyLoggersDoNotCreateFilesWhenDeveloperDiagnosticsAreDisabled()
    {
        Assert.False(DeveloperDiagnostics.IsEnabled);
        Assert.False(AdmissionForensicLog.IsEnabled);
        Assert.False(StartupEnablementForensicLog.IsEnabled);

        var root = FindRepositoryRoot();
        var admission = File.ReadAllText(Path.Combine(root, "PS7ScriptDesk.Application", "Diagnostics", "AdmissionForensicLog.cs"));
        var startup = File.ReadAllText(Path.Combine(root, "PS7ScriptDesk.Application", "Diagnostics", "StartupEnablementForensicLog.cs"));
        var recall = File.ReadAllText(Path.Combine(root, "PS7ScriptDesk.Application", "Diagnostics", "TerminalRecallEnterForensicLog.cs"));

        Assert.DoesNotContain("File.WriteAllText(LogPath", admission, StringComparison.Ordinal);
        Assert.DoesNotContain("File.WriteAllText(LogPath", startup, StringComparison.Ordinal);
        Assert.Contains("if (!DeveloperDiagnostics.IsEnabled)", recall, StringComparison.Ordinal);
    }

    [Fact]
    public void LegacyMigrationRetainsMigrationButDoesNotWriteForensicEvents()
    {
        var command = LegacyHistoryMigration.BuildStartupCommand();

        Assert.Contains("HistorySavePath", command, StringComparison.Ordinal);
        Assert.Contains("IsLegacyManagedLine", File.ReadAllText(Path.Combine(FindRepositoryRoot(), "PS7ScriptDesk.PowerShell", "Services", "LegacyHistoryMigration.cs")), StringComparison.Ordinal);
        Assert.DoesNotContain("[IO.File]::AppendAllText($__pssdMigrationLogPath", command, StringComparison.Ordinal);
        Assert.DoesNotContain("Write-Ps7SdMigrationEvent([string] $event, [hashtable] $fields) {\n                    try", command, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "PS7ScriptDesk.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
