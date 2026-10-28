using System.Text.Json;
using System.Text.RegularExpressions;

namespace Coflnet.Ane.Parts;

public sealed record ManufacturerBearingReference(string Article, double InnerDiameterMm, double OuterDiameterMm, double WidthMm, int SourcePage);

/// <summary>Manufacturer dimensions for exact SKF open-bearing articles. A reference proves neither offer quantity nor fitment or product identity.</summary>
public static class ManufacturerBearingCatalog
{
    public const string SourceUrl = "https://cdn.skfmediahub.skf.com/api/public/0901d196807026e8/pdf_preview_medium/0901d196807026e8_pdf_preview_medium.pdf";
    private static readonly Lazy<IReadOnlyList<ManufacturerBearingReference>> references = new(Load);
    private static readonly Regex Article = new(@"(?<![\p{L}\p{N}])6\d{2,3}(?!\d)", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex BrandedArticle = new(@"(?<![\p{L}\p{N}])skf\s+(?<article>6\d{2,3})(?![\p{L}\p{N}])", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex Variant = new(@"^\s*[-/]|^\s+(?:(?:2\s*)?(?:rs\w*|rz|zz|z)|[a-z]{1,4}\s*\d+|m|mb|ma|n|nr|tn\w*|jem|gjn|mt|lht|gmb)\b", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static IReadOnlyList<ManufacturerBearingReference> References => references.Value;

    /// <summary>Title-only evidence: one explicit SKF article, no seal, clearance or cage variant, and no second bearing designation.</summary>
    public static ManufacturerBearingReference? Match(string? title)
    {
        var text = PartText.Fold(title);
        var match = BrandedArticle.Match(text);
        if (!match.Success || Article.Matches(text).Count != 1 || Variant.IsMatch(text[(match.Index + match.Length)..]))
            return null;
        return References.FirstOrDefault(r => r.Article == match.Groups["article"].Value);
    }

    private static IReadOnlyList<ManufacturerBearingReference> Load()
    {
        using var stream = typeof(ManufacturerBearingCatalog).Assembly.GetManifestResourceStream("Coflnet.Ane.Parts.skf-bearings.json")!;
        using var document = JsonDocument.Parse(stream);
        return document.RootElement.GetProperty("products").EnumerateArray().Select(p => new ManufacturerBearingReference(
            p.GetProperty("article").GetString()!,
            p.GetProperty(PartAttributeKeys.InnerDiameterMm).GetDouble(),
            p.GetProperty(PartAttributeKeys.OuterDiameterMm).GetDouble(),
            p.GetProperty(PartAttributeKeys.WidthMm).GetDouble(),
            p.GetProperty("source_page").GetInt32())).ToList().AsReadOnly();
    }
}
