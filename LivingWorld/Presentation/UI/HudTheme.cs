namespace LivingWorld.Presentation;
public static class HudTheme
{
    public static StyleBoxFlat Panel(string color="#171c1bee", int radius=12, int padding=16)
    {
        return new StyleBoxFlat
        {
            BgColor=new Color(color), CornerRadiusTopLeft=radius, CornerRadiusTopRight=radius, CornerRadiusBottomLeft=radius, CornerRadiusBottomRight=radius, ContentMarginLeft=padding, ContentMarginRight=padding, ContentMarginTop=padding, ContentMarginBottom=padding
        };
    }
    private static void Style(Theme theme, string name, string type, StyleBox style) => theme.Call("set_stylebox", name, type, style);
    public static Theme Create()
    {
        var theme=new Theme
        {
            DefaultFontSize=14
        };
        theme.SetColor("font_color", "Label", new Color("#ecede5"));
        theme.SetColor("default_color", "RichTextLabel", new Color("#cdd1c7"));
        Style(theme, "panel", "PanelContainer", Panel());
        Style(theme, "normal", "Button", Panel("#2a302c", 7, 10));
        Style(theme, "hover", "Button", Panel("#3a423b", 7, 10));
        Style(theme, "pressed", "Button", Panel("#626b5f", 7, 10));
        Style(theme, "focus", "Button", new StyleBoxEmpty());
        theme.SetColor("font_color", "Button", new Color("#eeeee6"));
        Style(theme, "normal", "LineEdit", Panel("#252b27", 7, 9));
        Style(theme, "focus", "LineEdit", Panel("#333c34", 7, 9));
        Style(theme, "normal", "OptionButton", Panel("#2a302c", 7, 9));
        Style(theme, "hover", "OptionButton", Panel("#3a423b", 7, 9));
        Style(theme, "panel", "PopupMenu", Panel("#202620", 8, 8));
        Style(theme, "background", "ProgressBar", Panel("#303831", 3, 0));
        Style(theme, "fill", "ProgressBar", Panel("#bfc9b6", 3, 0));
        theme.SetConstant("separation", "VBoxContainer", 10);
        theme.SetConstant("separation", "HBoxContainer", 8);
        return theme;
    }
}
