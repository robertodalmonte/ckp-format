namespace Ckp.Tests;

using Ckp.Core;

public sealed class GlossaryEntryTests
{
    [Fact]
    public void Glossary_captures_cross_book_vocabulary_fragmentation()
    {
        var equivalents = new Dictionary<string, string>
        {
            ["delta-14e"] = "stretch receptor",
            ["ching-tcm-5e"] = "jing-luo pressure point",
            ["alpha-4e"] = "periodontal mechanoreceptor"
        };

        var entry = new GlossaryEntry(
            BookTerm: "fascial mechanoreceptor",
            StandardTerm: "tissue mechanoreceptor",
            MeshTerm: "D008465",
            EquivalentsInOtherBooks: equivalents,
            Note: "Four books, four names, one transducer type.");

        entry.BookTerm.Should().Be("fascial mechanoreceptor");
        entry.StandardTerm.Should().Be("tissue mechanoreceptor");
        entry.MeshTerm.Should().Be("D008465");
        entry.EquivalentsInOtherBooks.Should().HaveCount(3);
        entry.EquivalentsInOtherBooks["delta-14e"].Should().Be("stretch receptor");
    }

    [Fact]
    public void Glossary_without_mesh_or_equivalents()
    {
        var entry = new GlossaryEntry(
            BookTerm: "qi",
            StandardTerm: "vital energy (prescientific)",
            MeshTerm: null,
            EquivalentsInOtherBooks: new Dictionary<string, string>(),
            Note: null);

        entry.MeshTerm.Should().BeNull();
        entry.EquivalentsInOtherBooks.Should().BeEmpty();
    }
}
