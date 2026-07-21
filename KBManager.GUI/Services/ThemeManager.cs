using Avalonia;
using Avalonia.Media;
using KBManager.GUI.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace KBManager.GUI.Services;

/// <summary>
/// 主题管理器 — 从 theme.json 加载色彩配置并注入 Avalonia 资源字典。
/// 优先级：可执行文件同目录的 theme.json > 内嵌默认资源。
/// </summary>
public class ThemeManager
{
    private const string ThemeFileName = "theme.json";
    private const string EmbeddedResourcePath = "KBManager.GUI.Config.theme.json";

    public ThemeConfig CurrentTheme { get; private set; } = new();

    /// <summary>
    /// 加载主题：先尝试外部文件（用户自定义），再回退到内嵌资源。
    /// </summary>
    public void LoadTheme()
    {
        string? json = null;

        // 1) 尝试加载可执行文件旁边的 theme.json
        try
        {
            var exeDir = AppContext.BaseDirectory;
            var externalPath = Path.Combine(exeDir, ThemeFileName);
            if (File.Exists(externalPath))
            {
                json = File.ReadAllText(externalPath);
            }
            else
            {
                // 首次运行：自动生成 theme.json 样例文件
                GenerateSampleTheme(externalPath);
            }
        }
        catch
        {
            // 忽略外部文件加载/生成错误
        }

        // 2) 回退到内嵌资源
        if (json == null)
        {
            json = LoadEmbeddedTheme();
        }

        if (json != null)
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                };
                CurrentTheme = JsonSerializer.Deserialize<ThemeConfig>(json, options) ?? new ThemeConfig();
            }
            catch (JsonException)
            {
                CurrentTheme = new ThemeConfig();
            }
        }
    }

    /// <summary>
    /// 将当前主题注入到 Application 资源字典中。
    /// 所有 XAML 中的 {DynamicResource Key} 将从这里取值。
    /// </summary>
    public void ApplyToApplication(Application app)
    {
        var resources = app.Resources;
        var dict = CurrentTheme.ToResourceDictionary();

        foreach (var kvp in dict)
        {
            var brush = ParseBrush(kvp.Value);
            resources[kvp.Key] = brush;
        }

        // 同时注册颜色字符串版本（某些场景用得到）
        foreach (var kvp in dict)
        {
            resources[kvp.Key + "String"] = kvp.Value;
        }
    }

    /// <summary>
    /// 从内嵌资源读取默认主题。
    /// </summary>
    private static string? LoadEmbeddedTheme()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream(EmbeddedResourcePath);
            if (stream == null) return null;
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 将 hex 字符串 (#RRGGBB 或 #AARRGGBB) 转为 Avalonia SolidColorBrush。
    /// </summary>
    private static IBrush ParseBrush(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
            return Brushes.Transparent;

        hex = hex.Trim();

        // 支持 "Transparent" 关键字
        if (hex.Equals("Transparent", StringComparison.OrdinalIgnoreCase))
            return Brushes.Transparent;

        if (hex.StartsWith("#"))
            hex = hex.Substring(1);

        try
        {
            byte a = 255, r, g, b;

            if (hex.Length == 8) // #AARRGGBB
            {
                a = byte.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
                r = byte.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
                g = byte.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
                b = byte.Parse(hex.Substring(6, 2), System.Globalization.NumberStyles.HexNumber);
            }
            else if (hex.Length == 6) // #RRGGBB
            {
                r = byte.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
                g = byte.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
                b = byte.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
            }
            else
            {
                return Brushes.Transparent;
            }

            return new SolidColorBrush(new Color(a, r, g, b));
        }
        catch
        {
            return Brushes.Transparent;
        }
    }

    /// <summary>
    /// 在指定路径生成带注释的 theme.json 样例文件，方便用户自定义。
    /// </summary>
    private static void GenerateSampleTheme(string path)
    {
        var sample = @"{
  // ============================================
  //  KBManager 主题色彩配置
  //  修改颜色后重启应用即可生效
  //  格式：#RRGGBB 或 #AARRGGBB 或 Transparent
  // ============================================

  // ── 品牌色（主色调，用于按钮、标题栏、链接）──
  ""PrimaryColor"": ""#0078D4"",
  ""PrimaryHoverColor"": ""#106EBE"",
  ""PrimaryForegroundColor"": ""#FFFFFF"",

  // ── 危险色（删除、移除按钮）──
  ""DangerColor"": ""#D32F2F"",
  ""DangerHoverColor"": ""#C62828"",
  ""DangerForegroundColor"": ""#FFFFFF"",

  // ── 次要按钮色 ──
  ""SecondaryColor"": ""#E0E0E0"",
  ""SecondaryHoverColor"": ""#D0D0D0"",
  ""SecondaryForegroundColor"": ""#333333"",

  // ── 文字色 ──
  ""TextPrimaryColor"": ""#333333"",          // 正文、标题
  ""TextSecondaryColor"": ""#555555"",        // 标签、次要文字
  ""TextMutedColor"": ""#888888"",            // 提示、副标题
  ""TextStatusColor"": ""#666666"",           // 状态栏文字
  ""TextOnPrimaryColor"": ""#FFFFFF"",        // 主色背景上的文字
  ""TextOnPrimaryLightColor"": ""#B3D9FF"",   // 标题栏副标题
  ""TextOnDarkColor"": ""#D4D4D4"",           // 深色背景上的文字（日志区）
  ""TextLinkColor"": ""#1565C0"",             // 链接、标签文字
  ""TextDangerColor"": ""#C62828"",           // 危险操作文字

  // ── 背景色 ──
  ""BackgroundDefaultColor"": ""#FFFFFF"",    // 主内容区背景
  ""BackgroundInputColor"": ""#FFFFFF"",      // 输入框背景
  ""BackgroundSidebarColor"": ""#F5F5F5"",    // 左侧导航背景
  ""BackgroundSectionColor"": ""#F8F8F8"",    // 设置页分段背景
  ""BackgroundHeaderRowColor"": ""#F5F5F5"",  // 表格表头
  ""BackgroundFooterColor"": ""#FAFAFA"",     // 表格底部摘要
  ""BackgroundStatusBarColor"": ""#F0F0F0"",  // 状态栏
  ""BackgroundDarkColor"": ""#1E1E1E"",       // 日志输出区
  ""BackgroundChipColor"": ""#E3F2FD"",       // 标签芯片
  ""BackgroundHoverColor"": ""#F5F5F5"",      // 列表项悬停
  ""BackgroundSelectedColor"": ""#E3F2FD"",   // 列表项选中
  ""BackgroundNavHoverColor"": ""#E8E8E8"",   // 导航按钮悬停
  ""BackgroundNavPressedColor"": ""#D0D0D0"", // 导航按钮按下

  // ── 边框色 ──
  ""BorderDefaultColor"": ""#E0E0E0"",        // 通用边框、分隔线
  ""BorderInputColor"": ""#D0D0D0"",          // 输入框边框
  ""BorderFocusColor"": ""#0078D4"",          // 输入框聚焦边框

  // ── 标题栏 ──
  ""TitleBarBackgroundColor"": ""#0078D4"",

  // ── 光标 / 特殊 ──
  ""CaretColor"": ""#333333"",
  ""WatermarkColor"": ""#AAAAAA"",              // 输入框占位文字
  ""ScrollBarThumbColor"": ""#C0C0C0""         // 滚动条滑块
}
";
        File.WriteAllText(path, sample, System.Text.Encoding.UTF8);
    }
}
