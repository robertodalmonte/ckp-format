namespace Ckp.Tests;

using Ckp.Transpiler;

public sealed class PredictedMeasurementParserTests
{
    [Fact]
    public void Parses_increased_direction()
    {
        var obs = KnowledgeBaseTranspiler.ParsePredictedMeasurement(
            "Increased heart rate variability (RMSSD and/or SDNN)");

        obs.Direction.Should().Be("increase");
        obs.Measurement.Should().Be("heart rate variability (RMSSD and/or SDNN)");
    }

    [Fact]
    public void Parses_decreased_direction()
    {
        var obs = KnowledgeBaseTranspiler.ParsePredictedMeasurement(
            "Decreased salivary or blood cortisol");

        obs.Direction.Should().Be("decrease");
        obs.Measurement.Should().Be("salivary or blood cortisol");
    }

    [Fact]
    public void Parses_altered_as_change()
    {
        var obs = KnowledgeBaseTranspiler.ParsePredictedMeasurement(
            "Altered gene expression after cyclic strain");

        obs.Direction.Should().Be("change");
        obs.Measurement.Should().Be("gene expression after cyclic strain");
    }

    [Fact]
    public void Parses_changes_keyword_as_change()
    {
        var obs = KnowledgeBaseTranspiler.ParsePredictedMeasurement(
            "Tissue stiffness changes measured by shear wave elastography");

        obs.Direction.Should().Be("change");
        obs.Instrument.Should().Be("shear wave elastography");
        obs.Measurement.Should().Be("Tissue stiffness changes");
    }

    [Fact]
    public void Parses_improvement_as_increase()
    {
        var obs = KnowledgeBaseTranspiler.ParsePredictedMeasurement(
            "Range of motion improvement after fascial manipulation");

        obs.Direction.Should().Be("increase");
    }

    [Fact]
    public void Extracts_instrument_from_measured_by()
    {
        var obs = KnowledgeBaseTranspiler.ParsePredictedMeasurement(
            "Fascial gliding measured by ultrasound");

        obs.Instrument.Should().Be("ultrasound");
        obs.Measurement.Should().Be("Fascial gliding");
    }

    [Fact]
    public void Extracts_instrument_from_using()
    {
        var obs = KnowledgeBaseTranspiler.ParsePredictedMeasurement(
            "Cell stiffness measured using atomic force microscopy");

        obs.Instrument.Should().Be("atomic force microscopy");
    }

    [Fact]
    public void Falls_back_to_expected_direction()
    {
        var obs = KnowledgeBaseTranspiler.ParsePredictedMeasurement(
            "Focal adhesion formation under tension");

        obs.Direction.Should().Be("expected");
        obs.Measurement.Should().Be("Focal adhesion formation under tension");
        obs.Instrument.Should().BeNull();
    }

    [Fact]
    public void Unit_is_always_null()
    {
        var obs = KnowledgeBaseTranspiler.ParsePredictedMeasurement(
            "Increased heart rate variability");

        obs.Unit.Should().BeNull();
    }

    [Fact]
    public void Decrease_in_pattern()
    {
        var obs = KnowledgeBaseTranspiler.ParsePredictedMeasurement(
            "Decrease in sympathetic/parasympathetic ratio");

        obs.Direction.Should().Be("decrease");
        obs.Measurement.Should().Be("sympathetic/parasympathetic ratio");
    }
}
