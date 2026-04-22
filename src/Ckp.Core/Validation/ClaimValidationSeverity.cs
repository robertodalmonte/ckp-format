namespace Ckp.Core.Validation;

/// <summary>
/// Severity level for a CKP extraction validation diagnostic. Three-tier triage:
/// Errors block the build, Warnings require justification, Notices prompt a gut-check.
/// </summary>
/// <remarks>
/// <b>Intended consumer:</b> library users. Part of the CKP 1.x wire contract —
/// serialized into the package manifest or a section file and consumed by every
/// CKP reader, writer, and validator.
/// </remarks>
public enum ClaimValidationSeverity
{
    /// <summary>Package must not be accepted. The claim is structurally or semantically broken.</summary>
    Error = 0,

    /// <summary>Package can proceed, but the claim should be reviewed. Requires justification.</summary>
    Warning = 1,

    /// <summary>Advisory flag for human reviewer. Quick gut-check of source text recommended.</summary>
    Notice = 2
}
