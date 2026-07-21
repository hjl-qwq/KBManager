namespace KBManager.GUI.Models;

/// <summary>
/// 主题色彩配置模型 — 所有 GUI 颜色由此统一管理。
/// 反序列化自 theme.json。
/// </summary>
public class ThemeConfig
{
    // ── 品牌色 ──
    public string PrimaryColor { get; set; } = "#0078D4";
    public string PrimaryHoverColor { get; set; } = "#106EBE";
    public string PrimaryForegroundColor { get; set; } = "#FFFFFF";

    // ── 危险色 ──
    public string DangerColor { get; set; } = "#D32F2F";
    public string DangerHoverColor { get; set; } = "#C62828";
    public string DangerForegroundColor { get; set; } = "#FFFFFF";

    // ── 次要按钮色 ──
    public string SecondaryColor { get; set; } = "#E0E0E0";
    public string SecondaryHoverColor { get; set; } = "#D0D0D0";
    public string SecondaryForegroundColor { get; set; } = "#333333";

    // ── 文字色 ──
    public string TextPrimaryColor { get; set; } = "#333333";
    public string TextSecondaryColor { get; set; } = "#555555";
    public string TextMutedColor { get; set; } = "#888888";
    public string TextStatusColor { get; set; } = "#666666";
    public string TextOnPrimaryColor { get; set; } = "#FFFFFF";
    public string TextOnPrimaryLightColor { get; set; } = "#B3D9FF";
    public string TextOnDarkColor { get; set; } = "#D4D4D4";
    public string TextLinkColor { get; set; } = "#1565C0";
    public string TextDangerColor { get; set; } = "#C62828";

    // ── 背景色 ──
    public string BackgroundDefaultColor { get; set; } = "#FFFFFF";
    public string BackgroundSidebarColor { get; set; } = "#F5F5F5";
    public string BackgroundSectionColor { get; set; } = "#F8F8F8";
    public string BackgroundHeaderRowColor { get; set; } = "#F5F5F5";
    public string BackgroundFooterColor { get; set; } = "#FAFAFA";
    public string BackgroundStatusBarColor { get; set; } = "#F0F0F0";
    public string BackgroundDarkColor { get; set; } = "#1E1E1E";
    public string BackgroundChipColor { get; set; } = "#E3F2FD";
    public string BackgroundHoverColor { get; set; } = "#F5F5F5";
    public string BackgroundSelectedColor { get; set; } = "#E3F2FD";
    public string BackgroundNavHoverColor { get; set; } = "#E8E8E8";
    public string BackgroundNavPressedColor { get; set; } = "#D0D0D0";
    public string BackgroundInputColor { get; set; } = "#FFFFFF";

    // ── 边框色 ──
    public string BorderDefaultColor { get; set; } = "#E0E0E0";
    public string BorderInputColor { get; set; } = "#D0D0D0";
    public string BorderFocusColor { get; set; } = "#0078D4";

    // ── 标题栏 ──
    public string TitleBarBackgroundColor { get; set; } = "#0078D4";

    // ── 光标 / 特殊 ──
    public string CaretColor { get; set; } = "#333333";
    public string WatermarkColor { get; set; } = "#AAAAAA";
    public string ScrollBarThumbColor { get; set; } = "#C0C0C0";

    /// <summary>
    /// 将所有属性转为 Dictionary，用于注入 Avalonia ResourceDictionary。
    /// Key 格式与 XAML 中 DynamicResource 引用一致。
    /// </summary>
    public Dictionary<string, string> ToResourceDictionary()
    {
        return new Dictionary<string, string>
        {
            ["PrimaryColor"] = PrimaryColor,
            ["PrimaryHoverColor"] = PrimaryHoverColor,
            ["PrimaryForegroundColor"] = PrimaryForegroundColor,
            ["DangerColor"] = DangerColor,
            ["DangerHoverColor"] = DangerHoverColor,
            ["DangerForegroundColor"] = DangerForegroundColor,
            ["SecondaryColor"] = SecondaryColor,
            ["SecondaryHoverColor"] = SecondaryHoverColor,
            ["SecondaryForegroundColor"] = SecondaryForegroundColor,
            ["TextPrimaryColor"] = TextPrimaryColor,
            ["TextSecondaryColor"] = TextSecondaryColor,
            ["TextMutedColor"] = TextMutedColor,
            ["TextStatusColor"] = TextStatusColor,
            ["TextOnPrimaryColor"] = TextOnPrimaryColor,
            ["TextOnPrimaryLightColor"] = TextOnPrimaryLightColor,
            ["TextOnDarkColor"] = TextOnDarkColor,
            ["TextLinkColor"] = TextLinkColor,
            ["TextDangerColor"] = TextDangerColor,
            ["BackgroundDefaultColor"] = BackgroundDefaultColor,
            ["BackgroundSidebarColor"] = BackgroundSidebarColor,
            ["BackgroundSectionColor"] = BackgroundSectionColor,
            ["BackgroundHeaderRowColor"] = BackgroundHeaderRowColor,
            ["BackgroundFooterColor"] = BackgroundFooterColor,
            ["BackgroundStatusBarColor"] = BackgroundStatusBarColor,
            ["BackgroundDarkColor"] = BackgroundDarkColor,
            ["BackgroundChipColor"] = BackgroundChipColor,
            ["BackgroundHoverColor"] = BackgroundHoverColor,
            ["BackgroundSelectedColor"] = BackgroundSelectedColor,
            ["BackgroundNavHoverColor"] = BackgroundNavHoverColor,
            ["BackgroundNavPressedColor"] = BackgroundNavPressedColor,
            ["BackgroundInputColor"] = BackgroundInputColor,
            ["BorderDefaultColor"] = BorderDefaultColor,
            ["BorderInputColor"] = BorderInputColor,
            ["BorderFocusColor"] = BorderFocusColor,
            ["TitleBarBackgroundColor"] = TitleBarBackgroundColor,
            ["CaretColor"] = CaretColor,
            ["WatermarkColor"] = WatermarkColor,
            ["ScrollBarThumbColor"] = ScrollBarThumbColor,
        };
    }
}
