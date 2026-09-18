namespace LivingWorld.Presentation;

public static class HudTheme
{
    public static StyleBoxFlat Panel(string color="#101819f2", int radius=12, int padding=16, string border="#2a3a39")
    {
        var style=new StyleBoxFlat
        {
            BgColor=new Color(color),
            CornerRadiusTopLeft=radius,
            CornerRadiusTopRight=radius,
            CornerRadiusBottomLeft=radius,
            CornerRadiusBottomRight=radius,
            ContentMarginLeft=padding,
            ContentMarginRight=padding,
            ContentMarginTop=padding,
            ContentMarginBottom=padding
        };
        if (border.Length>0)
        {
            style.BorderColor=new Color(border);
            style.BorderWidthLeft=1;
            style.BorderWidthTop=1;
            style.BorderWidthRight=1;
            style.BorderWidthBottom=1;
        }
        return style;
    }

    private static void Style(Theme theme, string name, string type, StyleBox style) =>
        theme.Call("set_stylebox", name, type, style);

    public static Theme Create()
    {
        var theme=new Theme { DefaultFontSize=14 };

        theme.SetColor("font_color","Label",new Color("#f0eee6"));
        theme.SetColor("font_shadow_color","Label",new Color(0,0,0,.35f));
        theme.SetColor("default_color","RichTextLabel",new Color("#d8d9d1"));

        Style(theme,"panel","PanelContainer",Panel());
        Style(theme,"normal","Button",Panel("#182324",8,10,"#2c3c3b"));
        Style(theme,"hover","Button",Panel("#243332",8,10,"#49605a"));
        Style(theme,"pressed","Button",Panel("#344742",8,10,"#7f9b83"));
        Style(theme,"focus","Button",new StyleBoxEmpty());
        theme.SetColor("font_color","Button",new Color("#e9e8df"));
        theme.SetColor("font_hover_color","Button",new Color("#fff4d1"));
        theme.SetColor("font_pressed_color","Button",new Color("#fff4d1"));

        Style(theme,"normal","LineEdit",Panel("#0c1314",8,10,"#2b3b3a"));
        Style(theme,"focus","LineEdit",Panel("#111d1d",8,10,"#91aa89"));
        theme.SetColor("font_color","LineEdit",new Color("#f1efe7"));
        theme.SetColor("font_placeholder_color","LineEdit",new Color("#71817d"));

        Style(theme,"normal","OptionButton",Panel("#182324",8,9,"#2c3c3b"));
        Style(theme,"hover","OptionButton",Panel("#243332",8,9,"#49605a"));
        Style(theme,"pressed","OptionButton",Panel("#344742",8,9,"#7f9b83"));
        Style(theme,"panel","PopupMenu",Panel("#11191a",8,8,"#334744"));

        Style(theme,"background","ProgressBar",Panel("#1b2827",3,0,""));
        Style(theme,"fill","ProgressBar",Panel("#8fa983",3,0,""));

        theme.SetConstant("separation","VBoxContainer",10);
        theme.SetConstant("separation","HBoxContainer",8);
        return theme;
    }
}
