namespace Ckp.Tests;

using Ckp.Core;

public sealed class CkpValidationReportTests
{
    [Fact]
    public void Valid_report_has_no_diagnostics()
    {
        var report = CkpValidationReport.Valid();

        report.IsValid.Should().BeTrue();
        report.Diagnostics.Should().BeEmpty();
        report.ErrorCount.Should().Be(0);
        report.WarningCount.Should().Be(0);
    }

    [Fact]
    public void Report_with_only_warnings_is_valid()
    {
        var diagnostics = new List<ClaimValidationDiagnostic>
        {
            new("SEM1", ClaimValidationSeverity.Warning, "test.001", "Hedging detected.")
        };

        var report = CkpValidationReport.WithDiagnostics(diagnostics);

        report.IsValid.Should().BeTrue();
        report.ErrorCount.Should().Be(0);
        report.WarningCount.Should().Be(1);
    }

    [Fact]
    public void Report_with_error_is_invalid()
    {
        var diagnostics = new List<ClaimValidationDiagnostic>
        {
            new("S1", ClaimValidationSeverity.Error, "test.001", "Bad hash."),
            new("SEM1", ClaimValidationSeverity.Warning, "test.001", "Hedging detected.")
        };

        var report = CkpValidationReport.WithDiagnostics(diagnostics);

        report.IsValid.Should().BeFalse();
        report.ErrorCount.Should().Be(1);
        report.WarningCount.Should().Be(1);
    }

    [Fact]
    public void Diagnostic_records_rule_and_claim()
    {
        var diag = new ClaimValidationDiagnostic("SET3", ClaimValidationSeverity.Error, "alpha-3e.BIO.007", "Dangling ref.");

        diag.RuleId.Should().Be("SET3");
        diag.Severity.Should().Be(ClaimValidationSeverity.Error);
        diag.ClaimId.Should().Be("alpha-3e.BIO.007");
        diag.Message.Should().Be("Dangling ref.");
    }

    [Fact]
    public void Package_level_diagnostic_has_null_claim_id()
    {
        var diag = new ClaimValidationDiagnostic("SET5", ClaimValidationSeverity.Error, null, "Count mismatch.");

        diag.ClaimId.Should().BeNull();
    }

    [Fact]
    public void Report_with_only_notices_is_valid()
    {
        var diagnostics = new List<ClaimValidationDiagnostic>
        {
            new("SEM6", ClaimValidationSeverity.Notice, "test.001", "Epistemic rigidity detected.")
        };

        var report = CkpValidationReport.WithDiagnostics(diagnostics);

        report.IsValid.Should().BeTrue();
        report.NoticeCount.Should().Be(1);
        report.ErrorCount.Should().Be(0);
        report.WarningCount.Should().Be(0);
    }

    [Fact]
    public void Report_counts_all_three_severities()
    {
        var diagnostics = new List<ClaimValidationDiagnostic>
        {
            new("S1", ClaimValidationSeverity.Error, "a.001", "Bad hash."),
            new("SEM1", ClaimValidationSeverity.Warning, "a.002", "Hedging."),
            new("SEM6", ClaimValidationSeverity.Notice, "a.003", "Rigid.")
        };

        var report = CkpValidationReport.WithDiagnostics(diagnostics);

        report.IsValid.Should().BeFalse();
        report.ErrorCount.Should().Be(1);
        report.WarningCount.Should().Be(1);
        report.NoticeCount.Should().Be(1);
    }
}
