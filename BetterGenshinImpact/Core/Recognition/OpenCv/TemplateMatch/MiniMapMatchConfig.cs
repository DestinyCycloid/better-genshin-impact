namespace BetterGenshinImpact.Core.Recognition.OpenCv.TemplateMatch;

public static class MiniMapMatchConfig
{
    /// <summary>
    /// 原始小地图尺寸
    /// </summary>
    public const int OriginalSize = 156;

    /// <summary>
    /// 粗匹配时的地图尺寸
    /// </summary>
    public const int RoughSize = 52;

    /// <summary>
    /// 精确匹配时的地图尺寸
    /// </summary>
    public const int ExactSize = 260;

    public const int RoughZoom = 5;
    public const int ExactZoom = 1;
    public const int RoughSearchRadius = 50;
    public const int ExactSearchRadius = 20;
    // 降低阈值以支持手柄模式（手柄模式UI略有差异，置信度约0.91）
    public static readonly float[] ConfidenceThresholds = { 0.88f, 0.92f, 0.95f };
    
}