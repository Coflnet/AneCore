namespace Coflnet.Ane;

/// <summary>
/// Backward compatibility wrapper for UnifiedCategoryService.
/// For AneApi access to unified categories only.
/// For full platform mapping functionality, use the full CategoryMapper from AneNotifier.
/// </summary>
public class CategoryMapper : UnifiedCategoryService
{
    public CategoryMapper() : base()
    {
    }
}
