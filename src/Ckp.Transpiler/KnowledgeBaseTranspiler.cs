namespace Ckp.Transpiler;

using Ckp.Core;
using System.Text.Json;

/// <summary>
/// Reads a KnowledgeBase directory and produces a CkpPackage.
/// Domain-agnostic — works with any KnowledgeBase regardless of topic.
/// Requires a package.json metadata file in the KnowledgeBase root.
/// </summary>
public sealed class KnowledgeBaseTranspiler
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly string _kbPath;

    public KnowledgeBaseTranspiler(string knowledgeBasePath)
    {
        _kbPath = knowledgeBasePath;
    }

    public async Task<CkpPackage> TranspileAsync(CancellationToken ct = default)
    {
        // 0. Read package metadata
        var metadata = await ReadMetadataAsync(ct);

        // 1. Read all source files
        var (allClaims, allEvidence, signaturesByClaimId, traditionSignaturesByFile) = await ReadAllSourcesAsync(ct);
        var transitions = await ReadJsonArrayAsync<TransitionRecord>(
            Path.Combine(_kbPath, "integrations", "transitions.json"), ct);
        var bridges = await ReadJsonArrayAsync<BridgeRecord>(
            Path.Combine(_kbPath, "integrations", "bridges.json"), ct);
        var connections = await ReadJsonArrayAsync<ConnectionRecord>(
            Path.Combine(_kbPath, "integrations", "connections.json"), ct);

        // 2. Build lookup structures
        var transitionsByClaimId = transitions
            .GroupBy(t => t.ClaimId)
            .ToDictionary(g => g.Key, g => g.OrderBy(t => t.TransitionDate).ToList());

        var bridgesByAncient = bridges
            .GroupBy(b => b.AncientObservationClaimId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var connectionsBySource = connections
            .GroupBy(c => c.SourceClaimId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // 3. Build citation index (deduplicated by evidence ID)
        var citationIndex = new Dictionary<string, (KbEvidence ev, List<string> referencedBy)>();
        foreach (var (kbClaim, evidenceList) in allClaims)
        {
            string ckpId = DomainRegistry.ToCkpClaimId(kbClaim.Id, metadata.Key);
            foreach (var ev in evidenceList)
            {
                if (!citationIndex.TryGetValue(ev.Id, out var entry))
                {
                    entry = (ev, []);
                    citationIndex[ev.Id] = entry;
                }

                entry.referencedBy.Add(ckpId);
            }
        }

        // 4. Map claims to PackageClaim
        var packageClaims = new List<PackageClaim>();
        foreach (var (kbClaim, evidenceList) in allClaims)
        {
            signaturesByClaimId.TryGetValue(kbClaim.Id, out var signatures);
            traditionSignaturesByFile.TryGetValue(kbClaim.Id, out var tradSigs);
            var claim = MapClaim(kbClaim, evidenceList, metadata, transitionsByClaimId,
                bridgesByAncient, connectionsBySource, signatures, tradSigs);
            packageClaims.Add(claim);
        }

        // 5. Build citations
        var citations = citationIndex.Values
            .Select(e => MapCitation(e.ev, e.referencedBy))
            .ToList();

        // 6. Build domain index + package-wide tier counts in a single pass.
        // Pre-P3 this was five separate LINQ enumerations (one per tier, plus ClaimCount),
        // computed twice: once inside a GroupBy/Select for DomainInfo and again in
        // ContentFingerprint construction. For an N-claim package that's 9N comparisons
        // and nine linq-iterator allocations. Collapsing to a single foreach-per-scope
        // makes the transpile time linear again and keeps the inline tier-count logic
        // identical to the loop in CkpPackageWriter (no divergent counting rules).
        var domainAccum = new Dictionary<string, (int Claims, int T1, int T2, int T3, int T4)>(StringComparer.Ordinal);
        int pkgT1 = 0, pkgT2 = 0, pkgT3 = 0, pkgT4 = 0;
        foreach (var claim in packageClaims)
        {
            domainAccum.TryGetValue(claim.Domain, out var d);
            d.Claims++;
            switch (claim.Tier)
            {
                case Tier.T1: d.T1++; pkgT1++; break;
                case Tier.T2: d.T2++; pkgT2++; break;
                case Tier.T3: d.T3++; pkgT3++; break;
                case Tier.T4: d.T4++; pkgT4++; break;
            }
            domainAccum[claim.Domain] = d;
        }

        var domains = domainAccum
            .OrderBy(kvp => kvp.Key, StringComparer.Ordinal)
            .Select(kvp => new DomainInfo(
                Name: kvp.Key,
                ClaimCount: kvp.Value.Claims,
                T1Count: kvp.Value.T1,
                T2Count: kvp.Value.T2,
                T3Count: kvp.Value.T3,
                T4Count: kvp.Value.T4))
            .ToList();

        // 7. Build manifest
        var book = new BookMetadata(
            Key: metadata.Key,
            Title: metadata.Title,
            Edition: metadata.Edition,
            Authors: metadata.Authors,
            Publisher: metadata.Publisher,
            Year: metadata.Year,
            Isbn: null,
            Language: metadata.Language,
            Domains: domains.Select(d => d.Name).ToList());

        var fingerprint = new ContentFingerprint(
            Algorithm: "SHA-256",
            ClaimCount: packageClaims.Count,
            DomainCount: domains.Count,
            T1Count: pkgT1,
            T2Count: pkgT2,
            T3Count: pkgT3,
            T4Count: pkgT4,
            CitationCount: citations.Count);

        var manifest = PackageManifest.CreateNew(book, fingerprint);

        var edition = new EditionInfo(
            Edition: metadata.Edition,
            Year: metadata.Year,
            Isbn: null,
            Editor: string.Join(", ", metadata.Authors),
            Note: null);

        // TODO: multi-claim mechanism grouping — currently no mechanisms are emitted.
        return new CkpPackage
        {
            Manifest = manifest,
            Claims = packageClaims,
            Citations = citations,
            Domains = domains,
            Editions = [edition],
        };
    }

    private PackageClaim MapClaim(
        KbClaim kbClaim,
        List<KbEvidence> evidenceList,
        KbPackageMetadata metadata,
        Dictionary<string, List<TransitionRecord>> transitionsByClaimId,
        Dictionary<string, List<BridgeRecord>> bridgesByAncient,
        Dictionary<string, List<ConnectionRecord>> connectionsBySource,
        KbSignatures? signatures,
        KbTraditionSignatures? traditionSignatures)
    {
        string ckpId = DomainRegistry.ToCkpClaimId(kbClaim.Id, metadata.Key);
        string domainCode = DomainRegistry.ExtractDomainCode(kbClaim.Id);
        string domainName = DomainRegistry.ToDomainName(domainCode);
        Tier tier = (Tier)kbClaim.Tier;

        // Evidence references from citations
        var evidenceRefs = evidenceList
            .Select(e => new EvidenceReference(
                Type: EvidenceReferenceType.Citation,
                Ref: e.PubMedId is not null ? $"PMID:{e.PubMedId}" : e.Id,
                Relationship: EvidenceRelationship.Supports,
                Strength: EvidenceStrength.Primary,
                Note: e.KeyFindings))
            .ToList();

        // Internal refs from bridges (ancient claim → modern mechanism)
        if (bridgesByAncient.TryGetValue(kbClaim.Id, out var bridges))
        {
            foreach (var bridge in bridges)
            {
                string targetId = DomainRegistry.ToCkpClaimId(bridge.ModernMechanismClaimId, metadata.Key);
                evidenceRefs.Add(new EvidenceReference(
                    Type: EvidenceReferenceType.InternalRef,
                    Ref: targetId,
                    Relationship: EvidenceRelationship.Supports,
                    Strength: null,
                    Note: bridge.Notes));
            }
        }

        // Internal refs from connections (source → target)
        if (connectionsBySource.TryGetValue(kbClaim.Id, out var connections))
        {
            foreach (var conn in connections)
            {
                string targetId = DomainRegistry.ToCkpClaimId(conn.TargetClaimId, metadata.Key);
                evidenceRefs.Add(new EvidenceReference(
                    Type: EvidenceReferenceType.InternalRef,
                    Ref: targetId,
                    Relationship: EvidenceRelationship.Supports,
                    Strength: null,
                    Note: conn.Relationship));
            }
        }

        // Observables from falsification criteria + predicted measurements
        var observables = BuildObservables(kbClaim, signatures);

        // Tier history from transitions
        var tierHistory = new List<TierHistoryEntry>();
        if (transitionsByClaimId.TryGetValue(kbClaim.Id, out var transitions))
        {
            foreach (var t in transitions)
            {
                tierHistory.Add(new TierHistoryEntry(
                    Edition: metadata.Edition,
                    Tier: (Tier)t.ToTier,
                    Note: $"[{t.TransitionDate}] {t.Justification}"));
            }
        }

        // Keywords from falsification criteria + proposed mechanism + tradition signatures
        var keywords = new List<string>();
        if (kbClaim.FalsificationCriteria is not null)
            keywords.Add("falsifiable");
        if (kbClaim.ProposedMechanism is not null)
            keywords.Add("mechanism-identified");
        if (traditionSignatures is not null)
        {
            if (traditionSignatures.SearchKeywords is { Count: > 0 })
                keywords.AddRange(traditionSignatures.SearchKeywords);
            if (traditionSignatures.PhysiologicalTerms is { Count: > 0 })
                keywords.AddRange(traditionSignatures.PhysiologicalTerms);
            if (traditionSignatures.PracticeTerms is { Count: > 0 })
                keywords.AddRange(traditionSignatures.PracticeTerms);
        }

        return PackageClaim.CreateNew(
            id: ckpId,
            statement: kbClaim.Statement,
            tier: tier,
            domain: domainName,
            subDomain: null,
            keywords: keywords,
            meshTerms: [],
            evidence: evidenceRefs,
            observables: observables,
            sinceEdition: metadata.Edition,
            tierHistory: tierHistory);
    }

    private static List<Observable> BuildObservables(KbClaim kbClaim, KbSignatures? signatures)
    {
        var observables = new List<Observable>();

        if (kbClaim.FalsificationCriteria is not null)
        {
            observables.Add(new Observable(
                Measurement: "falsification-test",
                Unit: null,
                Direction: "expected",
                Latency: null,
                Instrument: null));
        }

        // Parse predicted measurements into structured observables
        if (signatures?.PredictedMeasurements is { Count: > 0 })
        {
            foreach (string pm in signatures.PredictedMeasurements)
                observables.Add(ParsePredictedMeasurement(pm));
        }

        return observables;
    }

    /// <summary>
    /// Parses a free-text predicted measurement into a structured Observable.
    /// Extracts direction (Increased/Decreased/change), measurement name,
    /// and instrument (if "measured by" / "using" pattern found).
    /// </summary>
    /// <remarks>Internal parser; exposed to <c>Ckp.Tests</c> via <c>InternalsVisibleTo</c>.</remarks>
    internal static Observable ParsePredictedMeasurement(string text)
    {
        string direction = "expected";
        string measurement = text;
        string? instrument = null;

        // Extract instrument from "measured by X" or "using X" patterns
        int measuredByIdx = text.IndexOf("measured by ", StringComparison.OrdinalIgnoreCase);
        int usingIdx = text.IndexOf(" using ", StringComparison.OrdinalIgnoreCase);
        if (measuredByIdx >= 0)
        {
            instrument = text[(measuredByIdx + 12)..].Trim().TrimEnd('.');
            measurement = text[..measuredByIdx].Trim();
        }
        else if (usingIdx >= 0)
        {
            instrument = text[(usingIdx + 7)..].Trim().TrimEnd('.');
            measurement = text[..usingIdx].Trim();
        }

        // Extract direction from leading words
        if (measurement.StartsWith("Increased ", StringComparison.OrdinalIgnoreCase)
            || measurement.StartsWith("Increase in ", StringComparison.OrdinalIgnoreCase))
        {
            direction = "increase";
            measurement = measurement.StartsWith("Increase in ", StringComparison.OrdinalIgnoreCase)
                ? measurement[12..] : measurement[10..];
        }
        else if (measurement.StartsWith("Decreased ", StringComparison.OrdinalIgnoreCase)
            || measurement.StartsWith("Decrease in ", StringComparison.OrdinalIgnoreCase))
        {
            direction = "decrease";
            measurement = measurement.StartsWith("Decrease in ", StringComparison.OrdinalIgnoreCase)
                ? measurement[12..] : measurement[10..];
        }
        else if (measurement.StartsWith("Altered ", StringComparison.OrdinalIgnoreCase))
        {
            direction = "change";
            measurement = measurement[8..];
        }
        // Check for "changes" or "improvement" in the text
        else if (measurement.Contains("changes", StringComparison.OrdinalIgnoreCase))
        {
            direction = "change";
        }
        else if (measurement.Contains("improvement", StringComparison.OrdinalIgnoreCase))
        {
            direction = "increase";
        }

        return new Observable(
            Measurement: measurement.Trim().TrimEnd('.'),
            Unit: null,
            Direction: direction,
            Latency: null,
            Instrument: instrument);
    }

    private static CitationEntry MapCitation(KbEvidence ev, List<string> referencedBy)
    {
        string refId = ev.PubMedId is not null ? $"PMID:{ev.PubMedId}" : ev.Id;
        return new CitationEntry(
            Ref: refId,
            Title: ev.Title,
            Authors: ev.Authors,
            Year: ev.Year,
            Journal: ev.Journal,
            ReferencedBy: referencedBy);
    }

    private async Task<KbPackageMetadata> ReadMetadataAsync(CancellationToken ct)
    {
        string metadataPath = Path.Combine(_kbPath, "package.json");
        if (!File.Exists(metadataPath))
            throw new FileNotFoundException(
                $"KnowledgeBase is missing package.json metadata file: {metadataPath}");

        await using var stream = File.OpenRead(metadataPath);
        return await JsonSerializer.DeserializeAsync<KbPackageMetadata>(stream, JsonOptions, ct)
            ?? throw new InvalidOperationException($"Failed to parse metadata: {metadataPath}");
    }

    private async Task<(
        List<(KbClaim claim, List<KbEvidence> evidence)> allClaims,
        List<KbEvidence> allEvidence,
        Dictionary<string, KbSignatures> signaturesByClaimId,
        Dictionary<string, KbTraditionSignatures> traditionSignaturesByClaimId)> ReadAllSourcesAsync(CancellationToken ct)
    {
        var allClaims = new List<(KbClaim, List<KbEvidence>)>();
        var allEvidence = new List<KbEvidence>();
        var signaturesByClaimId = new Dictionary<string, KbSignatures>();
        var traditionSignaturesByClaimId = new Dictionary<string, KbTraditionSignatures>();

        // Mechanisms — single claim per file
        string mechanismsDir = Path.Combine(_kbPath, "mechanisms");
        if (Directory.Exists(mechanismsDir))
        {
            foreach (string file in Directory.GetFiles(mechanismsDir, "*.json").OrderBy(f => f))
            {
                var mech = await ReadJsonAsync<MechanismFile>(file, ct);
                if (mech?.Claim is not null)
                {
                    allClaims.Add((mech.Claim, mech.Evidence));
                    allEvidence.AddRange(mech.Evidence);
                    if (mech.Signatures is not null)
                        signaturesByClaimId[mech.Claim.Id] = mech.Signatures;
                }
            }
        }

        // Traditions — multiple claims per file
        string traditionsDir = Path.Combine(_kbPath, "traditions");
        if (Directory.Exists(traditionsDir))
        {
            foreach (string file in Directory.GetFiles(traditionsDir, "*.json").OrderBy(f => f))
            {
                var trad = await ReadJsonAsync<TraditionFile>(file, ct);
                if (trad is not null)
                {
                    foreach (var claim in trad.Claims)
                    {
                        allClaims.Add((claim, trad.Evidence));
                        if (trad.TraditionSignatures is not null)
                            traditionSignaturesByClaimId[claim.Id] = trad.TraditionSignatures;
                    }
                    allEvidence.AddRange(trad.Evidence);
                }
            }
        }

        // Observations — multiple claims per file
        string observationsDir = Path.Combine(_kbPath, "observations");
        if (Directory.Exists(observationsDir))
        {
            foreach (string file in Directory.GetFiles(observationsDir, "*.json").OrderBy(f => f))
            {
                var obs = await ReadJsonAsync<ObservationFile>(file, ct);
                if (obs is not null)
                {
                    foreach (var claim in obs.Claims)
                        allClaims.Add((claim, obs.Evidence));
                    allEvidence.AddRange(obs.Evidence);
                }
            }
        }

        return (allClaims, allEvidence, signaturesByClaimId, traditionSignaturesByClaimId);
    }

    private static async Task<T?> ReadJsonAsync<T>(string path, CancellationToken ct) where T : class
    {
        if (!File.Exists(path))
            return null;
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, ct);
    }

    private static async Task<List<T>> ReadJsonArrayAsync<T>(string path, CancellationToken ct)
    {
        if (!File.Exists(path))
            return [];
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<List<T>>(stream, JsonOptions, ct) ?? [];
    }
}
