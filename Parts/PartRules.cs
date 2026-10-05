namespace Coflnet.Ane.Parts;

/// <summary>What a photo measurement of a gear reported, as the image service returns it (see AneImageService <c>gear_teeth.py</c>).</summary>
/// <param name="Teeth">The count, or null when the service refused to answer.</param>
/// <param name="Verified">True only when the counting methods agreed, the gear is whole in the frame and the shape checks passed.</param>
/// <param name="Confidence">0..1, the service's own confidence (regularity of the teeth, agreement, no occlusion).</param>
/// <param name="Reason">Why it refused or how sure it is, one short line.</param>
/// <param name="BoreToOuterRatio">Bore diameter over outer diameter as seen in the photo (scale free), null when no bore is visible.</param>
/// <param name="TeethObserved">Fragment mode: the intact teeth counted on a broken gear.</param>
/// <param name="ArcDegrees">Fragment mode: the arc those teeth cover.</param>
public sealed record GearPhotoMeasurement(int? Teeth, bool Verified, double Confidence, string? Reason, double? BoreToOuterRatio, int? TeethObserved = null, double? ArcDegrees = null);

/// <summary>The decided tooth count of a gear page with its source and confidence, or unknown with the reason.</summary>
public sealed record GearToothDecision(int? Teeth, string? Source, double Confidence, string Evidence)
{
    public bool IsKnown => Teeth != null;
}

/// <summary>
/// The rule that turns what the text states and what the photo shows into the one stored tooth count. "Exactly how many teeth" must not be a guess, so:
/// text and photo agree: the count, source text+image, high confidence. Text and photo disagree: the text wins unless the photo is verified and at least <see cref="PhotoVetoConfidence"/>
/// sure, in which case nothing is stored and both numbers go to the reviewer (an unverified or unsure photo never vetoes a clear text). Text only: the count when the text was clear
/// (confidence at least <see cref="MinTextConfidence"/>). Photo only: the count only when the photo measurement is verified and confident (at least <see cref="MinImageConfidence"/>).
/// Everything else: unknown. An unknown count is stored as the absence of the attribute, never as a number.
/// </summary>
public static class GearToothVerdict
{
    public const double MinTextConfidence = 0.65;
    public const double MinImageConfidence = 0.8;
    public const double PhotoVetoConfidence = 0.9;

    public static GearToothDecision Decide(TextValue<int>? text, GearPhotoMeasurement? photo, string? textNote = null)
    {
        var textOk = text != null && text.Confidence >= MinTextConfidence;
        var photoOk = photo is { Teeth: not null, Verified: true } && photo.Confidence >= MinImageConfidence;

        if (textOk && photo?.Teeth != null)
        {
            if (photo.Teeth == text!.Value)
                return new GearToothDecision(text.Value, PartValueSources.TextAndImage, Math.Min(0.98, Math.Max(text.Confidence, photo.Confidence) + 0.05),
                    $"{text.Evidence}; photo counted {photo.Teeth} ({(photo.Verified ? "verified" : "unverified")}, {PartText.Num(photo.Confidence)})");
            if (photo.Verified && photo.Confidence >= PhotoVetoConfidence)
                return new GearToothDecision(null, null, 0, $"conflict: text says {text.Value} ({text.Evidence}), photo counted {photo.Teeth} ({PartText.Num(photo.Confidence)}, verified); not stored");
            return new GearToothDecision(text.Value, PartValueSources.Text, text.Confidence,
                $"{text.Evidence}; photo counted {photo.Teeth} ({(photo.Verified ? "verified" : "unverified")}, {PartText.Num(photo.Confidence)}) and was not trusted over the text");
        }
        if (textOk)
            return new GearToothDecision(text!.Value, PartValueSources.Text, text.Confidence, text.Evidence);
        if (photoOk)
            return new GearToothDecision(photo!.Teeth, PartValueSources.Image, photo.Confidence, $"photo counted {photo.Teeth}, verified{(photo.Reason != null ? $"; {photo.Reason}" : "")}");

        var why = new List<string>();
        if (textNote != null) why.Add(textNote);
        else if (text != null) why.Add($"text unclear: {text.Evidence} ({PartText.Num(text.Confidence)})");
        else why.Add("text states no tooth count");
        if (photo != null)
            why.Add(photo.Teeth == null ? $"photo: {photo.Reason ?? "no count"}" : $"photo counted {photo.Teeth} but {(photo.Verified ? $"confidence {PartText.Num(photo.Confidence)} too low" : "not verified")}{(photo.Reason != null ? $" ({photo.Reason})" : "")}; not stored");
        return new GearToothDecision(null, null, 0, string.Join("; ", why));
    }
}

/// <summary>The outcome of the vote over the listings of a page for one attribute.</summary>
/// <param name="Value">The winning value, null when there is no majority.</param>
/// <param name="Confidence">Confidence of the winner: the best single confidence scaled by its share of the support.</param>
/// <param name="Support">Listings that stated the winner.</param>
/// <param name="Against">Listings that stated something else.</param>
public sealed record PartVoteResult(string? Value, double? Numeric, double Confidence, int Support, int Against, string Evidence)
{
    public bool IsKnown => Value != null;
}

/// <summary>
/// Majority vote with confidence over the per-listing readings of one attribute: the value with the most support (count, then confidence sum) wins when its support outweighs all other
/// values together; two listings that say 42 and 40 give no value (conflicting counts never become a fact), three that say 42, 42, 40 give 42 with a lowered confidence.
/// </summary>
public static class PartVote
{
    public static PartVoteResult Decide(IEnumerable<PartMeasurement> readings)
    {
        var rows = readings.Where(r => r.Source != PartValueSources.Review && r.Source != PartValueSources.Vote && r.Value.Length > 0 && r.Value != "unknown").ToList();
        if (rows.Count == 0)
            return new PartVoteResult(null, null, 0, 0, 0, "no listing states a value");
        var groups = rows.GroupBy(r => r.Value, StringComparer.Ordinal)
            .Select(g => (Value: g.Key, Numeric: g.First().Numeric, Count: g.Select(r => r.ListingKey).Distinct().Count(), Weight: g.Sum(r => r.Confidence), Best: g.Max(r => r.Confidence)))
            .OrderByDescending(g => g.Count).ThenByDescending(g => g.Weight).ToList();
        var winner = groups[0];
        var against = groups.Skip(1).Sum(g => g.Count);
        var againstWeight = groups.Skip(1).Sum(g => g.Weight);
        if (groups.Count > 1 && (winner.Count <= against || winner.Weight <= againstWeight))
            return new PartVoteResult(null, null, 0, winner.Count, against, $"listings disagree: {string.Join(", ", groups.Select(g => $"{g.Value} x{g.Count}"))}; not stored");
        var share = winner.Count / (double)(winner.Count + against);
        var confidence = Math.Round(winner.Best * (0.5 + 0.5 * share), 3);
        return new PartVoteResult(winner.Value, winner.Numeric, confidence, winner.Count, against,
            against == 0 ? $"{winner.Count} listing(s) state {winner.Value}" : $"{winner.Count} of {winner.Count + against} listings state {winner.Value}");
    }
}

/// <summary>Tolerances of a replacement part search: how far an approximate measure may be off and still match.</summary>
/// <param name="Relative">Relative tolerance on diameters and bores (0.08 = 8 %).</param>
/// <param name="AbsoluteMm">Absolute floor of the tolerance in millimetres, so small parts are not held to a fraction of a millimetre.</param>
public sealed record PartTolerance(double Relative = 0.08, double AbsoluteMm = 1.5)
{
    public static readonly PartTolerance Default = new();
    /// <summary>Stricter tolerance of the same-part rules: 2 % with a 0.2 mm floor (an 8 mm and a 9 mm bore are different shafts).</summary>
    public static readonly PartTolerance SamePart = new(0.02, 0.2);

    public bool Within(double a, double b) => Math.Abs(a - b) <= Allowed(a, b);

    public double Allowed(double a, double b) => Math.Max(AbsoluteMm, Relative * Math.Max(Math.Abs(a), Math.Abs(b)));

    public static PartTolerance FromRelative(double relative) => new(Math.Clamp(relative, 0.01, 0.5), Default.AbsoluteMm);
}

/// <summary>
/// What a replacement part search asks for. <see cref="PartKey"/> is the exact primary key value (a gear's tooth count, a bearing's designation); <see cref="TeethMin"/>..<see cref="TeethMax"/>
/// a range of tooth counts from a reverse search by a broken gear (<see cref="GearEstimate"/>), ranked by closeness to <see cref="Teeth"/>; the measures are approximate (<see cref="PartTolerance"/>).
/// Null is "not asked".
/// </summary>
public sealed record PartQuery(string PartClass, int? Teeth, double? OuterDiameterMm, double? BoreMm, double? Module, double? WidthMm, string? PartNumberKey, string? MachineKey, PartTolerance Tolerance,
    string? PartKey = null, int? TeethMin = null, int? TeethMax = null, double? InnerDiameterMm = null)
{
    public bool HasMeasure => OuterDiameterMm != null || BoreMm != null || Module != null || WidthMm != null || InnerDiameterMm != null;

    /// <summary>The partition keys to read: the tooth range, the exact tooth count, or the primary key.</summary>
    public IEnumerable<string> Keys()
    {
        if (TeethMin != null && TeethMax != null)
            for (var t = TeethMin.Value; t <= TeethMax.Value; t++) yield return t.ToString(System.Globalization.CultureInfo.InvariantCulture);
        else if (Teeth != null)
            yield return Teeth.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        else if (PartKey != null)
            yield return PartKey;
    }

    public bool AsksForAKey => Teeth != null || PartKey != null || (TeethMin != null && TeethMax != null);
}

/// <summary>Matching of a stored signature against a query, with a score (1 = everything asked for matches exactly, 0 = does not match).</summary>
public static class PartSignatureMatch
{
    /// <summary>
    /// Whether <paramref name="row"/> satisfies <paramref name="query"/>: the key must be equal when asked (a tooth range: inside it, scored by closeness to the estimate); a measure the row
    /// does not know never excludes it (unknown is not a mismatch) but only earns the neutral <see cref="UnknownScore"/>, so a page that states the size ranks above one that does not; a known
    /// measure outside the tolerance excludes it. Module must agree within 0.01 when both are known. The score is the mean over the asked values of their closeness.
    /// </summary>
    public static double Score(PartSignatureRow row, PartQuery query)
    {
        if (!string.Equals(row.PartClass, query.PartClass, StringComparison.Ordinal))
            return 0;
        var scores = new List<double>();
        if (query.TeethMin != null && query.TeethMax != null)
        {
            if (row.Teeth == null || row.Teeth < query.TeethMin || row.Teeth > query.TeethMax)
                return 0;
            var span = Math.Max(1, Math.Max(query.Teeth.GetValueOrDefault(row.Teeth.Value) - query.TeethMin.Value, query.TeethMax.Value - query.Teeth.GetValueOrDefault(row.Teeth.Value)));
            scores.Add(1 - 0.5 * Math.Abs(row.Teeth.Value - query.Teeth.GetValueOrDefault(row.Teeth.Value)) / span);
        }
        else if (query.Teeth != null)
        {
            if (row.Teeth != query.Teeth) return 0;
            scores.Add(1);
        }
        else if (query.PartKey != null)
        {
            if (!string.Equals(row.PartKey, query.PartKey, StringComparison.Ordinal)) return 0;
            scores.Add(1);
        }
        if (!Measure(query.OuterDiameterMm, row.OuterDiameterMm, query.Tolerance, scores)) return 0;
        if (!Measure(query.BoreMm, row.BoreMm, query.Tolerance, scores)) return 0;
        if (!Measure(query.InnerDiameterMm, row.InnerDiameterMm, query.Tolerance, scores)) return 0;
        if (!Measure(query.WidthMm, row.WidthMm, query.Tolerance, scores)) return 0;
        if (query.Module != null)
        {
            if (row.Module == null) scores.Add(UnknownScore);
            else if (Math.Abs(query.Module.Value - row.Module.Value) > 0.011) return 0;
            else scores.Add(1);
        }
        return scores.Count == 0 ? 0.5 : scores.Average();
    }

    /// <summary>What an asked measure the page does not know is worth: between a mismatch (excluded) and an exact match (1).</summary>
    public const double UnknownScore = 0.4;

    private static bool Measure(double? asked, double? known, PartTolerance tolerance, List<double> scores)
    {
        if (asked == null)
            return true;
        if (known == null)
        {
            scores.Add(UnknownScore);
            return true;
        }
        if (!tolerance.Within(asked.Value, known.Value))
            return false;
        scores.Add(1 - Math.Abs(asked.Value - known.Value) / tolerance.Allowed(asked.Value, known.Value));
        return true;
    }
}

/// <summary>
/// When two part pages are the same part. Confirmed (a fact) only on the strongest evidence: a labelled manufacturer part number both pages state (<see cref="PartNumberEdge"/>, 0.95), or
/// an identical (teeth, module, bore) triple, all three known on both pages and the bore within <see cref="PartTolerance.SamePart"/> (0.9). A bare code shared by both pages, or a
/// secondary part number, is only a candidate (0.6). Measured signatures without the full triple give a candidate: the primary key must be known on both and equal, and at least two of
/// outer diameter, bore and module must be known on both and agree within the same-part tolerance; below <see cref="MinCandidateConfidence"/> no edge. A known different module, tooth
/// count or designation blocks; different materials never do (sellers name them loosely).
/// </summary>
public static class SamePartRules
{
    public const double MinCandidateConfidence = 0.5;
    public const double ConfirmedTripleConfidence = 0.9;

    /// <summary>The edge for a part number both pages state: confirmed when it is a labelled primary number on both, a candidate otherwise.</summary>
    public static PartSameEdge PartNumberEdge(string seoId, string otherSeoId, PartNumberRow mine, PartNumberRow other)
    {
        var confirmed = mine.Labelled && other.Labelled && mine.Primary && other.Primary;
        return new PartSameEdge
        {
            SeoId = seoId, OtherSeoId = otherSeoId, Kind = PartEdgeKinds.PartNumber, Status = confirmed ? PartEdgeKinds.Confirmed : PartEdgeKinds.Candidate,
            Confidence = confirmed ? 0.95 : 0.6,
            Evidence = PartText.Evidence(confirmed ? $"same part number {mine.PartNumber}" : $"same {(mine.Labelled && other.Labelled ? "secondary" : "unlabelled")} part number {mine.PartNumber}"),
        };
    }

    public static PartSameEdge? Evaluate(PartSignatureRow a, PartSignatureRow b)
    {
        if (a.SeoId == b.SeoId || a.PartClass != b.PartClass)
            return null;
        if (a.PartNumberKey != null && a.PartNumberKey == b.PartNumberKey)
        {
            var confirmed = a.PartNumberLabelled && b.PartNumberLabelled;
            return new PartSameEdge
            {
                SeoId = a.SeoId, OtherSeoId = b.SeoId, Kind = PartEdgeKinds.PartNumber, Status = confirmed ? PartEdgeKinds.Confirmed : PartEdgeKinds.Candidate, Confidence = confirmed ? 0.95 : 0.6,
                Evidence = PartText.Evidence($"same {(confirmed ? "" : "unlabelled ")}part number {a.PartNumber ?? a.PartNumberKey}"),
            };
        }

        if (!a.HasKey || a.PartKey != b.PartKey)
            return null;
        var tolerance = PartTolerance.SamePart;
        var agreed = new List<string>();
        var disagreed = false;
        void Check(string name, double? x, double? y)
        {
            if (x == null || y == null) return;
            if (tolerance.Within(x.Value, y.Value)) agreed.Add($"{name} {PartText.Num(x.Value)}~{PartText.Num(y.Value)} mm");
            else disagreed = true;
        }
        Check("outer diameter", a.OuterDiameterMm, b.OuterDiameterMm);
        Check("bore", a.BoreMm, b.BoreMm);
        Check("inner diameter", a.InnerDiameterMm, b.InnerDiameterMm);
        Check("width", a.WidthMm, b.WidthMm);
        var moduleAgreed = false;
        if (a.Module != null && b.Module != null)
        {
            if (Math.Abs(a.Module.Value - b.Module.Value) <= 0.011) { agreed.Add($"module {PartText.Num(a.Module.Value)}"); moduleAgreed = true; }
            else return null; // a different module is a different gear
        }
        if (disagreed || agreed.Count < 2)
            return null;

        var keyName = a.PartClass == PartClasses.Bearing ? "designation" : "teeth";
        var boreAgreed = a.BoreMm != null && b.BoreMm != null && tolerance.Within(a.BoreMm.Value, b.BoreMm.Value);
        if (a.Teeth != null && moduleAgreed && boreAgreed)
            return new PartSameEdge
            {
                SeoId = a.SeoId, OtherSeoId = b.SeoId, Kind = PartEdgeKinds.Signature, Status = PartEdgeKinds.Confirmed, Confidence = ConfirmedTripleConfidence,
                Evidence = PartText.Evidence($"identical triple: {a.Teeth} teeth, module {PartText.Num(a.Module!.Value)}, bore {PartText.Num(a.BoreMm!.Value)} mm"),
            };

        var confidence = 0.5 + 0.1 * (agreed.Count - 2);
        if (a.Material != null && a.Material == b.Material)
        {
            confidence += 0.05;
            agreed.Add($"material {a.Material}");
        }
        if (a.BoreToOuterRatio != null && b.BoreToOuterRatio != null && Math.Abs(a.BoreToOuterRatio.Value - b.BoreToOuterRatio.Value) <= 0.03)
        {
            confidence += 0.05;
            agreed.Add("photo bore ratio");
        }
        confidence = Math.Min(0.75, confidence);
        if (confidence < MinCandidateConfidence)
            return null;
        return new PartSameEdge
        {
            SeoId = a.SeoId, OtherSeoId = b.SeoId, Kind = PartEdgeKinds.Signature, Status = PartEdgeKinds.Candidate, Confidence = confidence,
            Evidence = PartText.Evidence($"same signature: {keyName} {a.PartKey}, {string.Join(", ", agreed)}"),
        };
    }
}

/// <summary>The estimated tooth count of a gear from what a broken one still shows, with its range.</summary>
public sealed record GearTeethEstimate(int Teeth, int Min, int Max, string Method, string Evidence);

/// <summary>
/// Reverse search by a broken gear: a seller or searcher who only has a fragment with a run of intact teeth, or a gear whose diameter and pitch they can measure. The original count is
/// <c>round(teeth_observed × 360 / arc_degrees)</c>; from a measured outer diameter D and tooth pitch p it is about π·D/p; from the module m it is D/m − 2 (tip diameter = m·(N+2)).
/// Every estimate comes with a range (the half-tooth uncertainty of a measured arc, two teeth for a measured pitch, one for a module) that the index is searched over.
/// </summary>
public static class GearEstimate
{
    public const int MaxTeeth = GearTextExtractor.MaxTeeth;

    public static GearTeethEstimate? FromFragment(int teethObserved, double arcDegrees)
    {
        if (teethObserved < 2 || arcDegrees < 10 || arcDegrees > 360)
            return null;
        var exact = teethObserved * 360.0 / arcDegrees;
        var teeth = (int)Math.Round(exact);
        if (teeth < 3 || teeth > MaxTeeth)
            return null;
        // half a tooth of arc at each end is unknown: ±(360 / arc) / 2 teeth, at least one
        var range = Math.Max(1, (int)Math.Ceiling(180.0 / arcDegrees));
        return new GearTeethEstimate(teeth, Math.Max(3, teeth - range), Math.Min(MaxTeeth, teeth + range), "fragment", $"{teethObserved} teeth over {PartText.Num(arcDegrees)}° ≈ {PartText.Num(exact)} teeth");
    }

    public static GearTeethEstimate? FromPitch(double pitchMm, double outerDiameterMm)
    {
        if (pitchMm <= 0 || outerDiameterMm <= 0)
            return null;
        var exact = Math.PI * outerDiameterMm / pitchMm;
        var teeth = (int)Math.Round(exact);
        if (teeth < 3 || teeth > MaxTeeth)
            return null;
        return new GearTeethEstimate(teeth, Math.Max(3, teeth - 2), Math.Min(MaxTeeth, teeth + 2), "pitch", $"π × {PartText.Num(outerDiameterMm)} mm / {PartText.Num(pitchMm)} mm ≈ {PartText.Num(exact)} teeth");
    }

    public static GearTeethEstimate? FromModule(double module, double outerDiameterMm)
    {
        if (module <= 0 || outerDiameterMm <= 0)
            return null;
        var exact = outerDiameterMm / module - 2;
        var teeth = (int)Math.Round(exact);
        if (teeth < 3 || teeth > MaxTeeth)
            return null;
        return new GearTeethEstimate(teeth, Math.Max(3, teeth - 1), Math.Min(MaxTeeth, teeth + 1), "module", $"{PartText.Num(outerDiameterMm)} mm / module {PartText.Num(module)} − 2 ≈ {PartText.Num(exact)} teeth");
    }
}
