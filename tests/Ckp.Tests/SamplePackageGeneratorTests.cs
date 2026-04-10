namespace Ckp.Tests;

using Ckp.Core;
using Ckp.IO;

public sealed class SamplePackageGeneratorTests
{
    private readonly CkpPackageWriter _writer = new();
    private readonly CkpPackageReader _reader = new();

    [Fact]
    public async Task Generate_sample_biomechanics_package_and_verify_round_trip()
    {
        var ct = TestContext.Current.CancellationToken;

        // --- Claims ---

        var claim1 = PackageClaim.CreateNew(
            id: "biomech-3e.MP.001",
            statement: "Skeletal muscle generates force through actin-myosin cross-bridge cycling, requiring ATP hydrolysis for each power stroke.",
            tier: "T1",
            domain: "muscle-physiology",
            chapter: 4,
            section: "Cross-Bridge Mechanics",
            pageRange: "78-82",
            keywords: ["actin", "myosin", "cross-bridge", "ATP", "power stroke"],
            meshTerms: ["D009119", "D000205"],
            evidence:
            [
                new EvidenceReference(EvidenceReferenceType.Citation, "PMID:00000001",
                    EvidenceRelationship.Supports, EvidenceStrength.Primary,
                    "Landmark sliding filament study"),
                new EvidenceReference(EvidenceReferenceType.Citation, "PMID:00000002",
                    EvidenceRelationship.Supports, EvidenceStrength.Confirmatory,
                    "Independent replication via cryo-EM")
            ],
            observables:
            [
                new Observable("Isometric force output", "N", "increase",
                    "<100 ms after stimulation", "Force transducer")
            ],
            sinceEdition: 1);

        var claim2 = PackageClaim.CreateNew(
            id: "biomech-3e.BB.001",
            statement: "Wolff's law states that bone remodels along lines of mechanical stress, increasing density in load-bearing regions.",
            tier: "T1",
            domain: "bone-biology",
            chapter: 6,
            section: "Adaptive Bone Remodeling",
            pageRange: "134-139",
            keywords: ["Wolff's law", "bone remodeling", "mechanical stress", "bone density"],
            meshTerms: ["D016474"],
            evidence:
            [
                new EvidenceReference(EvidenceReferenceType.Citation, "PMID:00000003",
                    EvidenceRelationship.Supports, EvidenceStrength.Primary,
                    "Meta-analysis of bone density studies in athletes")
            ],
            observables:
            [
                new Observable("Cortical bone mineral density", "g/cm\u00b2", "increase",
                    "6-12 months of loading", "DXA scan")
            ],
            sinceEdition: 1);

        var claim3 = PackageClaim.CreateNew(
            id: "biomech-3e.CT.001",
            statement: "Fascial tissue exhibits viscoelastic creep under sustained low-load stretching, with time constants between 60 and 300 seconds.",
            tier: "T2",
            domain: "connective-tissue",
            chapter: 8,
            section: "Viscoelastic Properties of Fascia",
            pageRange: "195-200",
            keywords: ["fascia", "viscoelastic", "creep", "stretching"],
            meshTerms: ["D005205"],
            evidence:
            [
                new EvidenceReference(EvidenceReferenceType.Citation, "PMID:00000004",
                    EvidenceRelationship.Supports, EvidenceStrength.Primary,
                    "In-vitro creep testing of human thoracolumbar fascia")
            ],
            sinceEdition: 2);

        var claim4 = PackageClaim.CreateNew(
            id: "biomech-3e.MT.001",
            statement: "Interstitial fluid flow through the extracellular matrix may function as a mechanotransduction signaling pathway independent of direct cell-cell contact.",
            tier: "T3",
            domain: "mechanotransduction",
            chapter: 10,
            section: "Extracellular Fluid Dynamics",
            pageRange: "248-253",
            keywords: ["interstitial fluid", "extracellular matrix", "mechanotransduction", "signaling"],
            meshTerms: ["D040542"],
            sinceEdition: 3);

        var claim5 = PackageClaim.CreateNew(
            id: "biomech-3e.BB.002",
            statement: "Traditional martial arts conditioning of the forearm through progressive impact loading increases cortical bone density, consistent with Wolff's law.",
            tier: "T4",
            domain: "bone-biology",
            chapter: 12,
            section: "Impact Loading and Bone Adaptation",
            pageRange: "310-314",
            keywords: ["martial arts", "impact loading", "cortical bone", "Wolff's law"],
            evidence:
            [
                new EvidenceReference(EvidenceReferenceType.InternalRef, "biomech-3e.BB.001",
                    EvidenceRelationship.Supports, null,
                    "Builds on established Wolff's law claim")
            ],
            sinceEdition: 1,
            tierHistory:
            [
                new TierHistoryEntry(1, "T3", "Introduced as speculative bridge from traditional practice"),
                new TierHistoryEntry(3, "T4", "Reclassified as ancient observation after new radiographic evidence supported the practice")
            ]);

        var claims = new List<PackageClaim> { claim1, claim2, claim3, claim4, claim5 };

        // --- Citations ---

        var citations = new List<CitationEntry>
        {
            new("PMID:00000001", "Sliding filament mechanism of muscle contraction",
                "Huxley AF, Niedergerke R", 1954, "Nature", ["biomech-3e.MP.001"]),
            new("PMID:00000002", "Cryo-EM structure of the actomyosin complex",
                "Chen L, Park S", 2018, "J Mol Biol", ["biomech-3e.MP.001"]),
            new("PMID:00000003", "Bone mineral density adaptation in weight-bearing athletes: a meta-analysis",
                "Zhao W, Fernandez A, Kim T", 2020, "Bone", ["biomech-3e.BB.001"]),
            new("PMID:00000004", "Viscoelastic creep behaviour of human thoracolumbar fascia",
                "Chen M, Torres A", 2012, "J Biomech", ["biomech-3e.CT.001"])
        };

        // --- Glossary ---

        var glossary = new List<GlossaryEntry>
        {
            new("cross-bridge cycling", "actomyosin interaction", "D009119",
                new Dictionary<string, string>
                {
                    ["gamma-2e"] = "actin-myosin sliding",
                    ["delta-14e"] = "cross-bridge mechanism"
                },
                "Multiple terms for the same molecular motor event across textbooks.")
        };

        // --- Chapters ---

        var chapters = new List<ChapterInfo>
        {
            new(4, "Muscle Mechanics and Force Generation", 1, ["muscle-physiology"]),
            new(6, "Bone Biology and Adaptive Remodeling", 1, ["bone-biology"]),
            new(8, "Connective Tissue Biomechanics", 1, ["connective-tissue"]),
            new(10, "Mechanotransduction Pathways", 1, ["mechanotransduction"]),
            new(12, "Applied Loading and Traditional Practices", 1, ["bone-biology"])
        };

        // --- Domains ---

        var domains = new List<DomainInfo>
        {
            new("muscle-physiology", 1, 1, 0, 0, 0),
            new("bone-biology", 2, 1, 0, 0, 1),
            new("connective-tissue", 1, 0, 1, 0, 0),
            new("mechanotransduction", 1, 0, 0, 1, 0)
        };

        // --- Fingerprint ---

        var fingerprint = new ContentFingerprint(
            Algorithm: "SHA-256",
            ClaimCount: 5,
            DomainCount: 4,
            T1Count: 2,
            T2Count: 1,
            T3Count: 1,
            T4Count: 1,
            CitationCount: 4);

        // --- Book metadata ---

        var book = new BookMetadata(
            Key: "biomech-3e",
            Title: "Fundamentals of Biomechanics, 3rd Edition",
            Edition: 3,
            Authors: ["A. Sample", "B. Example"],
            Publisher: "Open Science Press",
            Year: 2024,
            Isbn: "978-0-000-00000-0",
            Language: "en-US",
            Domains: ["muscle-physiology", "bone-biology", "connective-tissue", "mechanotransduction"]);

        // --- Manifest ---

        var manifest = PackageManifest.CreateNew(book, fingerprint);

        // --- Package ---

        var package = new CkpPackage(
            Manifest: manifest,
            Claims: claims,
            Citations: citations,
            AxiomRefs: [],
            Chapters: chapters,
            Domains: domains,
            Glossary: glossary,
            Editions: [],
            Alignments: [],
            Mechanisms: [],
            Phenomena: [],
            PublisherCommentary: [],
            CommunityCommentary: []);

        // --- Write to disk ---

        var outputDir = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "examples"));
        Directory.CreateDirectory(outputDir);
        var outputPath = Path.Combine(outputDir, "sample-biomechanics.ckp");

        await using (var fileStream = File.Create(outputPath))
        {
            await _writer.WriteAsync(package, fileStream, ct);
        }

        // --- Round-trip verification ---

        CkpPackage roundTripped;
        await using (var readStream = File.OpenRead(outputPath))
        {
            roundTripped = await _reader.ReadAsync(readStream, ct);
        }

        // Manifest
        roundTripped.Manifest.FormatVersion.Should().Be("1.0");
        roundTripped.Manifest.Book.Key.Should().Be("biomech-3e");
        roundTripped.Manifest.Book.Title.Should().Be("Fundamentals of Biomechanics, 3rd Edition");
        roundTripped.Manifest.Book.Edition.Should().Be(3);
        roundTripped.Manifest.Book.Authors.Should().HaveCount(2);
        roundTripped.Manifest.Book.Authors[0].Should().Be("A. Sample");
        roundTripped.Manifest.Book.Authors[1].Should().Be("B. Example");
        roundTripped.Manifest.Book.Isbn.Should().Be("978-0-000-00000-0");
        roundTripped.Manifest.Book.Publisher.Should().Be("Open Science Press");
        roundTripped.Manifest.Book.Year.Should().Be(2024);

        // Fingerprint
        roundTripped.Manifest.ContentFingerprint.ClaimCount.Should().Be(5);
        roundTripped.Manifest.ContentFingerprint.DomainCount.Should().Be(4);
        roundTripped.Manifest.ContentFingerprint.T1Count.Should().Be(2);
        roundTripped.Manifest.ContentFingerprint.T2Count.Should().Be(1);
        roundTripped.Manifest.ContentFingerprint.T3Count.Should().Be(1);
        roundTripped.Manifest.ContentFingerprint.T4Count.Should().Be(1);
        roundTripped.Manifest.ContentFingerprint.CitationCount.Should().Be(4);

        // Claims
        roundTripped.Claims.Should().HaveCount(5);

        // T1 muscle claim
        var muscClaim = roundTripped.Claims.First(c => c.Id == "biomech-3e.MP.001");
        muscClaim.Tier.Should().Be("T1");
        muscClaim.Domain.Should().Be("muscle-physiology");
        muscClaim.Chapter.Should().Be(4);
        muscClaim.Evidence.Should().HaveCount(2);
        muscClaim.Observables.Should().HaveCount(1);
        muscClaim.Observables[0].Unit.Should().Be("N");
        muscClaim.Hash.Should().StartWith("sha256:");

        // T1 bone claim
        var boneClaim = roundTripped.Claims.First(c => c.Id == "biomech-3e.BB.001");
        boneClaim.Tier.Should().Be("T1");
        boneClaim.Evidence.Should().HaveCount(1);
        boneClaim.Observables.Should().HaveCount(1);
        boneClaim.Observables[0].Unit.Should().Be("g/cm\u00b2");

        // T2 fascia claim
        var fasciaClaim = roundTripped.Claims.First(c => c.Id == "biomech-3e.CT.001");
        fasciaClaim.Tier.Should().Be("T2");
        fasciaClaim.Domain.Should().Be("connective-tissue");
        fasciaClaim.Evidence.Should().HaveCount(1);

        // T3 mechanotransduction claim
        var mechClaim = roundTripped.Claims.First(c => c.Id == "biomech-3e.MT.001");
        mechClaim.Tier.Should().Be("T3");
        mechClaim.Evidence.Should().BeEmpty();

        // T4 martial arts claim with tier history
        var martialClaim = roundTripped.Claims.First(c => c.Id == "biomech-3e.BB.002");
        martialClaim.Tier.Should().Be("T4");
        martialClaim.TierHistory.Should().HaveCount(2);
        martialClaim.TierHistory[0].Edition.Should().Be(1);
        martialClaim.TierHistory[0].Tier.Should().Be("T3");
        martialClaim.TierHistory[1].Edition.Should().Be(3);
        martialClaim.TierHistory[1].Tier.Should().Be("T4");
        martialClaim.Evidence.Should().HaveCount(1);
        martialClaim.Evidence[0].Type.Should().Be(EvidenceReferenceType.InternalRef);
        martialClaim.Evidence[0].Ref.Should().Be("biomech-3e.BB.001");

        // Citations
        roundTripped.Citations.Should().HaveCount(4);
        roundTripped.Citations.Should().Contain(c => c.Ref == "PMID:00000001");
        roundTripped.Citations.Should().Contain(c => c.Ref == "PMID:00000004");

        // Glossary
        roundTripped.Glossary.Should().HaveCount(1);
        roundTripped.Glossary[0].BookTerm.Should().Be("cross-bridge cycling");
        roundTripped.Glossary[0].StandardTerm.Should().Be("actomyosin interaction");
        roundTripped.Glossary[0].EquivalentsInOtherBooks.Should().ContainKey("gamma-2e");
        roundTripped.Glossary[0].EquivalentsInOtherBooks.Should().ContainKey("delta-14e");

        // Chapters
        roundTripped.Chapters.Should().HaveCount(5);
        roundTripped.Chapters.Should().Contain(ch => ch.Number == 4);
        roundTripped.Chapters.Should().Contain(ch => ch.Number == 12);

        // Domains
        roundTripped.Domains.Should().HaveCount(4);
        var boneDomain = roundTripped.Domains.First(d => d.Name == "bone-biology");
        boneDomain.ClaimCount.Should().Be(2);
        boneDomain.T1Count.Should().Be(1);
        boneDomain.T4Count.Should().Be(1);

        // Hash integrity: verify original hashes survived round-trip
        foreach (var original in claims)
        {
            var rt = roundTripped.Claims.First(c => c.Id == original.Id);
            rt.Hash.Should().Be(original.Hash, $"hash mismatch for {original.Id}");
            rt.Hash.Should().StartWith("sha256:", $"hash format for {original.Id}");
        }
    }
}
