namespace Ckp.Tests;

using Ckp.Core;

/// <summary>
/// Builds an <see cref="ExtractionVocabulary"/> for tests.
/// Mirrors the JSON files in KnowledgeBase/reference-data/extraction-*.json.
/// If those files change, update this helper to match.
/// </summary>
internal static class TestExtractionVocabulary
{
    public static ExtractionVocabulary Build() => new(
        KnownDomains: new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "mechanotransduction", "bioelectricity", "autonomic-nervous-system",
            "fascia", "connective-tissue-anatomy", "myofascial-force-transmission",
            "ion-channel-gating", "nitric-oxide-signaling", "chronobiology",
            "piezoelectricity", "gut-brain-axis", "photochemistry",
            "trigeminal-autonomic", "locus-coeruleus", "stomatognathic-proprioception",
            "trigeminal-autonomic-cephalalgia",
            "traditional-chinese-medicine", "ayurveda", "yoga",
            "electromagnetic-field", "quantum-biology", "porphyrin-biochemistry",
            "homeopathy", "craniosacral-therapy",
            "malocclusion-epidemiology", "craniofacial-growth", "dental-development",
            "orthodontic-biology", "orthodontic-mechanics", "craniofacial-development",
            "treatment-philosophy", "orthodontic-biomechanics",
            "extracellular-matrix", "fascial-innervation", "fascial-physiology",
            "wound-healing"
        },
        MechanisticKeywords:
        [
            "FAK", "RANKL", "OPG", "PgE2", "prostaglandin", "osteoclast", "osteoblast",
            "integrin", "TGF-beta", "TGF-\u03b2", "CTGF", "IGF", "cytokine", "interleukin",
            "collagen", "myofibroblast", "mechanotransduction", "signal transduction",
            "cascade", "pathway", "receptor", "kinase", "phosphorylation"
        ],
        HedgingMarkers:
        [
            "appears to", "seems to", "suggests that", "may ", "might ", "could be",
            "possibly", "probably", "likely", "hypothesized", "hypothesizes", "hypothesize",
            "it is thought that", "it is believed that", "is not yet clear",
            "remains unclear", "remains to be", "further research",
            "not fully understood", "not yet established", "preliminary evidence",
            "emerging evidence", "evidence indicates", "proposed", "partially"
        ]);
}
