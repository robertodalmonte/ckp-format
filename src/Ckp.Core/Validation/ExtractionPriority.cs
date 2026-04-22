namespace Ckp.Core.Validation;

/// <summary>
/// Extraction priority classification for CKP 1.1 claims. Determines which conditional
/// validation rules fire (e.g., P0/P1 claims require observables). Optional on claims —
/// when absent, priority-conditional rules are skipped.
/// </summary>
/// <remarks>
/// <b>Intended consumer:</b> library users. Part of the CKP 1.x wire contract —
/// serialized into the package manifest or a section file and consumed by every
/// CKP reader, writer, and validator.
/// </remarks>
public enum ExtractionPriority
{
    /// <summary>P0 — Mechanistic claims with named molecules/pathways. Bridge points across books.</summary>
    Mechanistic = 0,

    /// <summary>P1 — Quantitative thresholds: forces, times, concentrations. Falsifiable, observable-rich.</summary>
    Quantitative = 1,

    /// <summary>P2 — Contested/evolving claims (natural T2-T3). Where science is alive.</summary>
    Contested = 2,

    /// <summary>P3 — T4 claims from ancient/traditional observations. Rescue candidates.</summary>
    AncientObservation = 3,

    /// <summary>P4 — Established anatomy/physiology anchoring other claims.</summary>
    Anchoring = 4,

    /// <summary>P5 — Epidemiological statistics, classification schemes. Low bridge potential.</summary>
    Epidemiological = 5
}
