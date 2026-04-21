namespace Ckp.Core.Claims;

/// <summary>
/// A measurable prediction tied to a CKP claim — what you'd measure to test it.
/// This is what empirical verification requires: a concrete, measurable prediction.
/// </summary>
/// <param name="Measurement">What is being measured (e.g., "Heart rate decrease").</param>
/// <param name="Unit">Unit of measurement (e.g., "bpm").</param>
/// <param name="Direction">Expected direction of change (e.g., "decrease").</param>
/// <param name="Latency">Expected time to observe the effect (e.g., "&lt;1 cardiac cycle").</param>
/// <param name="Instrument">Measurement instrument (e.g., "ECG").</param>
public sealed record Observable(
    string Measurement,
    string? Unit,
    string Direction,
    string? Latency,
    string? Instrument);
