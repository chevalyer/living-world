namespace LivingWorld.Presentation;
using System.Globalization;
public partial class SimulationHud : CanvasLayer
{
    public GameRoot Game { get; set; } = null!;
    public GridPoint? SelectedTile { get; set; }
    private Control _root=null!;
    private Label _clock=null!, _weather=null!, _population=null!, _status=null!, _inspectorTitle=null!;
    private RichTextLabel _details=null!, _overview=null!;
    private Button _pause=null!;
    private LineEdit _seed=null!, _newSeed=null!;
    private Control _modal=null!;
    private SpinBox _people=null!;
    private OptionButton _mapSize=null!;
    private string _tab="npc";
    private readonly CultureInfo _russian=CultureInfo.GetCultureInfo("ru-RU");
    public override void _Ready()
    {
        _root=new Control
        {
            Theme=HudTheme.Create(), MouseFilter=Control.MouseFilterEnum.Ignore
        };
        AddChild(_root);
        _root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var top=new PanelContainer();
        _root.AddChild(top);
        top.SetAnchorsPreset(Control.LayoutPreset.TopWide);
        top.OffsetLeft=20;
        top.OffsetRight=-20;
        top.OffsetTop=18;
        top.OffsetBottom=78;
        var row=new HBoxContainer();
        top.AddChild(row);
        var clockBox=new VBoxContainer
        {
            CustomMinimumSize=new Vector2(225, 0)
        };
        row.AddChild(clockBox);
        clockBox.AddThemeConstantOverride("separation", 3);
        _clock=Label("создание мира…", 18);
        clockBox.AddChild(_clock);
        _weather=Label("", 12);
        _weather.Modulate=new Color("#a9b2a3");
        clockBox.AddChild(_weather);
        row.AddChild(new Control
        {
            SizeFlagsHorizontal=Control.SizeFlags.ExpandFill, MouseFilter=Control.MouseFilterEnum.Ignore
        });
        _pause=Button("Ⅱ", ()=> { if (!Game.Busy) Game.Paused=!Game.Paused; }, "пауза · пробел");
        row.AddChild(_pause);
        foreach (var speed in new[]
        {
            1, 4, 12, 32
        })
        {
            var value=speed;
            row.AddChild(Button(speed+"×", ()=>Game.Speed=value, "скорость симуляции"));
        }
        row.AddChild(new Control
        {
            CustomMinimumSize=new Vector2(12, 0), MouseFilter=Control.MouseFilterEnum.Ignore
        });
        _seed=new LineEdit
        {
            Text="1847", PlaceholderText="сид", CustomMinimumSize=new Vector2(90, 0), MaxLength=11, TooltipText="числовой сид для нового мира"
        };
        row.AddChild(_seed);
        row.AddChild(Button("новый мир", ShowNewWorld, "карта и начальное население"));
        row.AddChild(Button("↓", Game.Save, "сохранить · F5"));
        row.AddChild(Button("↑", Game.Load, "загрузить · F9"));
        var inspector=new PanelContainer();
        _root.AddChild(inspector);
        inspector.AnchorLeft=1;
        inspector.AnchorRight=1;
        inspector.AnchorBottom=1;
        inspector.OffsetLeft=-338;
        inspector.OffsetRight=-20;
        inspector.OffsetTop=96;
        inspector.OffsetBottom=-64;
        var stack=new VBoxContainer();
        inspector.AddChild(stack);
        var tabs=new HBoxContainer();
        stack.AddChild(tabs);
        tabs.AddChild(Button("житель", ()=>
        {
            _tab="npc"; Refresh();
        }));
        tabs.AddChild(Button("мир", ()=>
        {
            _tab="world"; Refresh();
        }));
        tabs.AddChild(Button("системы", ()=>
        {
            _tab="systems"; Refresh();
        }));
        _inspectorTitle=Label("выбери жителя", 20);
        _inspectorTitle.AutowrapMode=TextServer.AutowrapMode.WordSmart;
        stack.AddChild(_inspectorTitle);
        var scroll=new ScrollContainer
        {
            SizeFlagsVertical=Control.SizeFlags.ExpandFill, HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled
        };
        stack.AddChild(scroll);
        _details=new RichTextLabel
        {
            BbcodeEnabled=true, FitContent=true, ScrollActive=false, SelectionEnabled=true, SizeFlagsHorizontal=Control.SizeFlags.ExpandFill, AutowrapMode=TextServer.AutowrapMode.WordSmart
        };
        scroll.AddChild(_details);
        var overviewPanel=new PanelContainer();
        _root.AddChild(overviewPanel);
        overviewPanel.AnchorTop=1;
        overviewPanel.AnchorBottom=1;
        overviewPanel.OffsetLeft=20;
        overviewPanel.OffsetTop=-217;
        overviewPanel.OffsetRight=294;
        overviewPanel.OffsetBottom=-82;
        var overviewBox=new VBoxContainer();
        overviewPanel.AddChild(overviewBox);
        _population=Label("жизнь поселения", 16);
        overviewBox.AddChild(_population);
        _overview=new RichTextLabel
        {
            BbcodeEnabled=true, FitContent=true, ScrollActive=false, MouseFilter=Control.MouseFilterEnum.Ignore
        };
        overviewBox.AddChild(_overview);
        var bottom=new VBoxContainer();
        _root.AddChild(bottom);
        bottom.AnchorTop=1;
        bottom.AnchorBottom=1;
        bottom.OffsetLeft=24;
        bottom.OffsetTop=-62;
        bottom.OffsetRight=1080;
        bottom.OffsetBottom=-14;
        _status=Label("", 13);
        bottom.AddChild(_status);
        var keys=Label("WASD / СКМ — камера    колесо — масштаб    F — вся карта    F2 — память и маршрут", 12);
        keys.Modulate=new Color("#d8ddcc");
        bottom.AddChild(keys);
        BuildNewWorldDialog();
    }
    private static Label Label(string text, int size=14)
    {
        var label=new Label
        {
            Text=text
        };
        label.AddThemeFontSizeOverride("font_size", size);
        return label;
    }
    private static Button Button(string text, Action pressed, string tip="")
    {
        var button=new Button
        {
            Text=text, TooltipText=tip, FocusMode=Control.FocusModeEnum.None
        };
        button.Pressed+=pressed;
        return button;
    }
    public void Status(string message)=>_status.Text=message;
    private void BuildNewWorldDialog()
    {
        _modal=new Control
        {
            Visible=false
        };
        _root.AddChild(_modal);
        _modal.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var dim=new ColorRect
        {
            Color=new Color(0, 0, 0, .6f)
        };
        _modal.AddChild(dim);
        dim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var center=new CenterContainer();
        _modal.AddChild(center);
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var panel=new PanelContainer
        {
            CustomMinimumSize=new Vector2(430, 0)
        };
        center.AddChild(panel);
        var column=new VBoxContainer();
        column.AddThemeConstantOverride("separation", 16);
        panel.AddChild(column);
        column.AddChild(Label("новый мир", 26));
        column.AddChild(Label("один сид — один начальный мир", 14));
        _newSeed=new LineEdit
        {
            Text="1847", PlaceholderText="числовой сид", MaxLength=11
        };
        column.AddChild(_newSeed);
        var sizeRow=new HBoxContainer();
        column.AddChild(sizeRow);
        sizeRow.AddChild(Label("размер карты"));
        _mapSize=new OptionButton
        {
            SizeFlagsHorizontal=Control.SizeFlags.ExpandFill
        };
        foreach (var value in new[]
        {
            96, 128, 192, 256
        })_mapSize.AddItem(value+" × "+value, value);
        _mapSize.Selected=1;
        sizeRow.AddChild(_mapSize);
        var populationRow=new HBoxContainer();
        column.AddChild(populationRow);
        populationRow.AddChild(Label("жители"));
        _people=new SpinBox
        {
            MinValue=1, MaxValue=500, Value=14, Step=1, SizeFlagsHorizontal=Control.SizeFlags.ExpandFill
        };
        populationRow.AddChild(_people);
        var note=Label("F5 сохраняет текущий мир перед заменой.", 12);
        note.Modulate=new Color("#a9b2a3");
        column.AddChild(note);
        var buttons=new HBoxContainer();
        column.AddChild(buttons);
        buttons.AddChild(Button("отмена", ()=>_modal.Hide()));
        buttons.AddChild(Button("создать", ()=>
        {
            if (!int.TryParse(_newSeed.Text, out var seed))
            {
                Status("сид должен быть целым числом"); return;
            }
            _seed.Text=_newSeed.Text; _modal.Hide(); _=Game.NewWorld(seed, _mapSize.GetSelectedId(), (int)_people.Value);
        }));
    }
    private void ShowNewWorld()
    {
        if (Game.Busy)return;
        _newSeed.Text=_seed.Text;
        _modal.Show();
    }
    public void Refresh()
    {
        if (Game.Snapshot is not { } snapshot) return;
        _clock.Text = snapshot.Date.ToString("d MMMM yyyy  ·  HH:mm", _russian).ToLowerInvariant();
        _weather.Text = $"{snapshot.Season}  ·  {snapshot.Air:F1} °C  ·  {(snapshot.Rain ? "осадки" : "без осадков")}  ·  сид {snapshot.Seed}";
        _pause.Text = Game.Paused ? "▶" : "Ⅱ";
        _population.Text = $"{snapshot.Population} жителей   ·   {snapshot.Homes} домов";
        var actual = snapshot.TicksPerSecond / SimulationRunner.BaseTicksPerSecond;
        _overview.Text = $"[color=#a9b2a3]скорость[/color]   {(Game.Paused ? "пауза" : Game.Speed + "×")}\n[color=#a9b2a3]фактически[/color]   {actual:F1}× · {snapshot.TicksPerSecond:F0} тиков/с\n{Safe(snapshot.LastEvent)}";
        if (_tab == "systems")
        {
            _inspectorTitle.Text = "производительность";
            _details.Text = $"{Engine.GetFramesPerSecond():F0} FPS\nцель {Game.Speed * SimulationRunner.BaseTicksPerSecond} тиков/с\nфактически {snapshot.TicksPerSecond:F0} тиков/с ({actual:F1}×)\nдетализация {Game.View.CurrentLod} · участков {Game.View.VisibleChunks}\nобъектов нарисовано {Game.View.VisibleObjects}\n" + snapshot.SystemsText;
        }
        else if (_tab == "world")
        {
            _inspectorTitle.Text = "мир";
            _details.Text = snapshot.WorldText;
        }
        else
        {
            _inspectorTitle.Text = snapshot.Inspector.Title;
            _details.Text = snapshot.Inspector.Text;
        }
    }
    private static string Safe(string value) => value.Replace("[", "[lb]");
}
