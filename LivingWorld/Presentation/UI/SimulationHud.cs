namespace LivingWorld.Presentation;

using System.Globalization;
using System.Text;

public partial class SimulationHud : CanvasLayer
{
    public GameRoot Game { get; set; } = null!;
    public GridPoint? SelectedTile { get; set; }

    private Control _root=null!;
    private Label _clock=null!, _weather=null!, _population=null!, _performance=null!, _status=null!, _inspectorTitle=null!;
    private RichTextLabel _details=null!;
    private Button _pause=null!;
    private LineEdit _search=null!, _newSeed=null!;
    private VBoxContainer _searchResults=null!, _settlementList=null!;
    private Control _modal=null!;
    private SpinBox _people=null!;
    private OptionButton _mapSize=null!, _saveSlots=null!;

    private readonly Dictionary<string,Button> _tabButtons=new(StringComparer.Ordinal);
    private readonly Dictionary<MapOverlay,Button> _overlayButtons=[];
    private string _tab="npc";
    private int _settlementAnchor;
    private string _navigatorSignature="";
    private readonly CultureInfo _russian=CultureInfo.GetCultureInfo("ru-RU");

    public override void _Ready()
    {
        _root=new Control { Theme=HudTheme.Create(), MouseFilter=Control.MouseFilterEnum.Ignore };
        AddChild(_root);
        _root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        BuildTopBar();
        BuildNavigator();
        BuildInspector();
        BuildOverlayBar();
        BuildStatusBar();
        BuildNewWorldDialog();
    }

    private void BuildTopBar()
    {
        var top=new PanelContainer();
        _root.AddChild(top);
        top.SetAnchorsPreset(Control.LayoutPreset.TopWide);
        top.OffsetLeft=18;
        top.OffsetRight=-18;
        top.OffsetTop=16;
        top.OffsetBottom=86;

        var row=new HBoxContainer();
        top.AddChild(row);

        var clockBox=new VBoxContainer { CustomMinimumSize=new Vector2(280,0) };
        clockBox.AddThemeConstantOverride("separation",2);
        row.AddChild(clockBox);
        _clock=Label("создание мира…",19);
        _clock.Modulate=new Color("#fff2d7");
        clockBox.AddChild(_clock);
        _weather=Label("",12);
        _weather.Modulate=new Color("#91a39e");
        clockBox.AddChild(_weather);

        var metrics=new HBoxContainer { SizeFlagsHorizontal=Control.SizeFlags.ExpandFill };
        metrics.Alignment=BoxContainer.AlignmentMode.Center;
        row.AddChild(metrics);

        var populationBox=new VBoxContainer { CustomMinimumSize=new Vector2(235,0) };
        populationBox.AddThemeConstantOverride("separation",2);
        metrics.AddChild(populationBox);
        _population=Label("мир загружается",15);
        populationBox.AddChild(_population);
        _performance=Label("",11);
        _performance.Modulate=new Color("#91a39e");
        populationBox.AddChild(_performance);

        _pause=Button("Ⅱ",()=> { if(!Game.Busy)Game.Paused=!Game.Paused; },"пауза · пробел");
        Accent(_pause);
        row.AddChild(_pause);
        foreach(var speed in new[]{1,4,12,32})
        {
            var value=speed;
            row.AddChild(Button(speed+"×",()=>Game.Speed=value,"скорость симуляции · "+(Array.IndexOf(new[]{1,4,12,32},speed)+1)));
        }

        row.AddChild(Spacer(10));
        row.AddChild(Button("новый мир",ShowNewWorld,"создать мир с другим сидом"));
        _saveSlots=new OptionButton
        {
            CustomMinimumSize=new Vector2(86,0),
            TooltipText="слот сохранения"
        };
        for(var slot=1;slot<=3;slot++)_saveSlots.AddItem("слот "+slot,slot);
        _saveSlots.Selected=0;
        _saveSlots.ItemSelected+=index=>Game.SaveSlot=_saveSlots.GetItemId((int)index);
        row.AddChild(_saveSlots);
        row.AddChild(Button("сохранить",Game.Save,"сохранить · F5"));
        row.AddChild(Button("загрузить",Game.Load,"загрузить · F9"));
    }

    private void BuildNavigator()
    {
        var panel=new PanelContainer();
        _root.AddChild(panel);
        panel.AnchorBottom=1;
        panel.OffsetLeft=18;
        panel.OffsetRight=306;
        panel.OffsetTop=104;
        panel.OffsetBottom=-82;

        var stack=new VBoxContainer();
        panel.AddChild(stack);

        var eyebrow=Label("НАВИГАЦИЯ",11);
        eyebrow.Modulate=new Color("#b7c99d");
        stack.AddChild(eyebrow);
        var title=Label("мир и жители",21);
        title.Modulate=new Color("#fff1d2");
        stack.AddChild(title);

        _search=new LineEdit
        {
            PlaceholderText="найти жителя по имени…",
            ClearButtonEnabled=true
        };
        _search.TextChanged+=_=> { _navigatorSignature=""; RefreshNavigator(); };
        stack.AddChild(_search);

        _searchResults=new VBoxContainer();
        _searchResults.AddThemeConstantOverride("separation",5);
        stack.AddChild(_searchResults);

        var settlementTitle=Label("ПОСЕЛЕНИЯ",11);
        settlementTitle.Modulate=new Color("#b7c99d");
        settlementTitle.AddThemeConstantOverride("outline_size",1);
        stack.AddChild(settlementTitle);

        var scroll=new ScrollContainer
        {
            SizeFlagsVertical=Control.SizeFlags.ExpandFill,
            HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled
        };
        stack.AddChild(scroll);
        _settlementList=new VBoxContainer { SizeFlagsHorizontal=Control.SizeFlags.ExpandFill };
        _settlementList.AddThemeConstantOverride("separation",6);
        scroll.AddChild(_settlementList);
    }

    private void BuildInspector()
    {
        var inspector=new PanelContainer();
        _root.AddChild(inspector);
        inspector.AnchorLeft=1;
        inspector.AnchorRight=1;
        inspector.AnchorBottom=1;
        inspector.OffsetLeft=-390;
        inspector.OffsetRight=-18;
        inspector.OffsetTop=104;
        inspector.OffsetBottom=-82;

        var stack=new VBoxContainer();
        inspector.AddChild(stack);

        var tabs=new HBoxContainer();
        stack.AddChild(tabs);
        AddTab(tabs,"житель","npc");
        AddTab(tabs,"поселение","settlement");
        AddTab(tabs,"мир","world");
        AddTab(tabs,"системы","systems");

        _inspectorTitle=Label("выбери жителя",21);
        _inspectorTitle.Modulate=new Color("#fff1d2");
        _inspectorTitle.AutowrapMode=TextServer.AutowrapMode.WordSmart;
        stack.AddChild(_inspectorTitle);

        var scroll=new ScrollContainer
        {
            SizeFlagsVertical=Control.SizeFlags.ExpandFill,
            HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled
        };
        stack.AddChild(scroll);

        _details=new RichTextLabel
        {
            BbcodeEnabled=true,
            FitContent=true,
            ScrollActive=false,
            SelectionEnabled=true,
            SizeFlagsHorizontal=Control.SizeFlags.ExpandFill,
            AutowrapMode=TextServer.AutowrapMode.WordSmart
        };
        scroll.AddChild(_details);
    }

    private void BuildOverlayBar()
    {
        var panel=new PanelContainer();
        _root.AddChild(panel);
        panel.AnchorTop=1;
        panel.AnchorBottom=1;
        panel.OffsetLeft=326;
        panel.OffsetRight=-410;
        panel.OffsetTop=-68;
        panel.OffsetBottom=-16;

        var row=new HBoxContainer();
        panel.AddChild(row);
        var title=Label("слой карты",12);
        title.Modulate=new Color("#91a39e");
        row.AddChild(title);

        AddOverlay(row,"обычный",MapOverlay.None);
        AddOverlay(row,"поселения",MapOverlay.Settlements);
        AddOverlay(row,"тропы",MapOverlay.Traffic);
        AddOverlay(row,"почва",MapOverlay.Fertility);
        AddOverlay(row,"комнаты",MapOverlay.Rooms);

        row.AddChild(Spacer(8));
        var hint=Label("F3",11);
        hint.Modulate=new Color("#71817d");
        row.AddChild(hint);
    }

    private void BuildStatusBar()
    {
        var box=new VBoxContainer();
        _root.AddChild(box);
        box.AnchorTop=1;
        box.AnchorBottom=1;
        box.OffsetLeft=22;
        box.OffsetRight=300;
        box.OffsetTop=-72;
        box.OffsetBottom=-14;
        box.AddThemeConstantOverride("separation",2);

        _status=Label("",12);
        _status.AutowrapMode=TextServer.AutowrapMode.WordSmart;
        box.AddChild(_status);

        var keys=Label("Ctrl+F поиск  ·  F карта  ·  F2 маршрут  ·  F3 слой",10);
        keys.Modulate=new Color("#71817d");
        box.AddChild(keys);
    }

    private void AddTab(HBoxContainer row,string title,string id)
    {
        var button=Button(title,()=>
        {
            _tab=id;
            RefreshTabs();
            Refresh();
        });
        _tabButtons[id]=button;
        row.AddChild(button);
    }

    private void AddOverlay(HBoxContainer row,string title,MapOverlay overlay)
    {
        var button=Button(title,()=>Game.SetOverlay(overlay));
        button.CustomMinimumSize=new Vector2(76,0);
        _overlayButtons[overlay]=button;
        row.AddChild(button);
    }

    private static Label Label(string text,int size=14)
    {
        var label=new Label { Text=text };
        label.AddThemeFontSizeOverride("font_size",size);
        return label;
    }

    private static Button Button(string text,Action pressed,string tip="")
    {
        var button=new Button
        {
            Text=text,
            TooltipText=tip,
            FocusMode=Control.FocusModeEnum.None
        };
        button.Pressed+=pressed;
        return button;
    }

    private static Control Spacer(float width)=>new()
    {
        CustomMinimumSize=new Vector2(width,0),
        MouseFilter=Control.MouseFilterEnum.Ignore
    };

    private static void Accent(Button button)
    {
        button.AddThemeStyleboxOverride("normal",HudTheme.Panel("#61502c",8,10,"#9e8050"));
        button.AddThemeStyleboxOverride("hover",HudTheme.Panel("#7a6334",8,10,"#c19b5d"));
        button.AddThemeStyleboxOverride("pressed",HudTheme.Panel("#4d6a50",8,10,"#91aa89"));
        button.AddThemeColorOverride("font_color",new Color("#fff2d1"));
    }

    public void Status(string message)=>_status.Text=message;

    public void WorldChanged()
    {
        _navigatorSignature="";
        _settlementAnchor=0;
        _tab="npc";
        _search.Text="";
        RefreshTabs();
    }

    public void ShowNpc()
    {
        _tab="npc";
        RefreshTabs();
    }

    public void ShowSettlement(int anchor)
    {
        _settlementAnchor=anchor;
        _tab="settlement";
        RefreshTabs();
    }

    public void FocusSearch()
    {
        _search.GrabFocus();
    }

    public void RefreshOverlayButtons()
    {
        foreach(var (overlay,button) in _overlayButtons)
            button.Modulate=overlay==Game.Overlay?new Color("#ffe0a0"):Colors.White;
    }

    private void RefreshTabs()
    {
        foreach(var (id,button) in _tabButtons)
            button.Modulate=id==_tab?new Color("#ffe0a0"):Colors.White;
    }

    private void RefreshNavigator()
    {
        if (Game.Snapshot is not { } snapshot)return;
        var query=_search.Text.Trim();
        var signature=$"{snapshot.Generation}:{snapshot.People.Count}:{snapshot.Settlements.Count}:{query}:"+
            string.Join("|",snapshot.Settlements.Select(x=>$"{x.Anchor},{x.Members},{x.Homes}"));
        if (signature==_navigatorSignature)return;
        _navigatorSignature=signature;

        Clear(_searchResults);
        if (query.Length>0)
        {
            var matches=snapshot.People
                .Where(x=>x.Name.Contains(query,StringComparison.CurrentCultureIgnoreCase))
                .OrderByDescending(x=>x.Alive)
                .ThenBy(x=>x.Name,StringComparer.CurrentCultureIgnoreCase)
                .Take(6)
                .ToArray();
            if (matches.Length==0)
            {
                var empty=Label("ничего не найдено",12);
                empty.Modulate=new Color("#71817d");
                _searchResults.AddChild(empty);
            }
            foreach(var person in matches)
            {
                var id=person.Id;
                var button=Button(person.Alive?person.Name:person.Name+" · умер",()=>Game.FocusPerson(id),person.Action);
                button.Alignment=HorizontalAlignment.Left;
                _searchResults.AddChild(button);
            }
        }

        Clear(_settlementList);
        if (snapshot.Settlements.Count==0)
        {
            var empty=Label("поселения появятся после постройки первых домов",12);
            empty.AutowrapMode=TextServer.AutowrapMode.WordSmart;
            empty.Modulate=new Color("#71817d");
            _settlementList.AddChild(empty);
        }
        foreach(var settlement in snapshot.Settlements.OrderByDescending(x=>x.Members).ThenBy(x=>x.Name,StringComparer.CurrentCultureIgnoreCase))
        {
            var anchor=settlement.Anchor;
            var text=$"{settlement.Name}\n{settlement.Members} жителей · {settlement.Homes} домов";
            var button=Button(text,()=>Game.FocusSettlement(anchor),"перейти к поселению");
            button.Alignment=HorizontalAlignment.Left;
            button.CustomMinimumSize=new Vector2(0,46);
            if (Game.SelectedSettlement==anchor)button.Modulate=new Color("#ffe0a0");
            _settlementList.AddChild(button);
        }
    }

    private static void Clear(Node parent)
    {
        foreach(var child in parent.GetChildren())
        {
            parent.RemoveChild(child);
            child.QueueFree();
        }
    }

    private void BuildNewWorldDialog()
    {
        _modal=new Control { Visible=false };
        _root.AddChild(_modal);
        _modal.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        var dim=new ColorRect { Color=new Color(0,0,0,.72f) };
        _modal.AddChild(dim);
        dim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        var center=new CenterContainer();
        _modal.AddChild(center);
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        var panel=new PanelContainer { CustomMinimumSize=new Vector2(440,0) };
        panel.AddThemeStyleboxOverride("panel",HudTheme.Panel("#101819fa",14,20,"#536b61"));
        center.AddChild(panel);

        var column=new VBoxContainer();
        column.AddThemeConstantOverride("separation",15);
        panel.AddChild(column);

        var eyebrow=Label("НОВЫЙ МИР",11);
        eyebrow.Modulate=new Color("#b7c99d");
        column.AddChild(eyebrow);
        var title=Label("параметры симуляции",25);
        title.Modulate=new Color("#fff1d2");
        column.AddChild(title);

        _newSeed=new LineEdit { Text="1847",PlaceholderText="числовой сид",MaxLength=11 };
        column.AddChild(_newSeed);

        var sizeRow=new HBoxContainer();
        column.AddChild(sizeRow);
        sizeRow.AddChild(Label("размер карты"));
        _mapSize=new OptionButton { SizeFlagsHorizontal=Control.SizeFlags.ExpandFill };
        foreach(var value in new[]{96,128,192,256})_mapSize.AddItem(value+" × "+value,value);
        _mapSize.Selected=1;
        sizeRow.AddChild(_mapSize);

        var populationRow=new HBoxContainer();
        column.AddChild(populationRow);
        populationRow.AddChild(Label("жители"));
        _people=new SpinBox
        {
            MinValue=1,MaxValue=500,Value=14,Step=1,
            SizeFlagsHorizontal=Control.SizeFlags.ExpandFill
        };
        populationRow.AddChild(_people);

        var note=Label("текущий мир не меняется, пока новый успешно не создан",12);
        note.Modulate=new Color("#91a39e");
        note.AutowrapMode=TextServer.AutowrapMode.WordSmart;
        column.AddChild(note);

        var buttons=new HBoxContainer();
        column.AddChild(buttons);
        buttons.AddChild(Spacer(1));
        var cancel=Button("отмена",()=>_modal.Hide());
        buttons.AddChild(cancel);
        var create=Button("создать",()=>
        {
            if(!int.TryParse(_newSeed.Text,out var seed))
            {
                Status("сид должен быть целым числом");
                return;
            }
            _modal.Hide();
            _=Game.NewWorld(seed,_mapSize.GetSelectedId(),(int)_people.Value);
        });
        Accent(create);
        buttons.AddChild(create);
    }

    private void ShowNewWorld()
    {
        if(Game.Busy)return;
        _newSeed.Text=Game.Snapshot?.Seed.ToString(CultureInfo.InvariantCulture)??"1847";
        _modal.Show();
    }

    public void Refresh()
    {
        if (Game.Snapshot is not { } snapshot)return;

        _clock.Text=snapshot.Date.ToString("d MMMM yyyy  ·  HH:mm",_russian).ToLowerInvariant();
        _weather.Text=$"{snapshot.Season}  ·  {snapshot.Air:F1} °C  ·  {(snapshot.Rain?"осадки":"без осадков")}  ·  сид {snapshot.Seed}";
        _pause.Text=Game.Paused?"▶":"Ⅱ";

        var actual=snapshot.TicksPerSecond/SimulationRunner.BaseTicksPerSecond;
        _population.Text=$"{snapshot.Population} жителей  ·  {snapshot.Settlements.Count} поселений  ·  {snapshot.Homes} домов";
        _performance.Text=Game.Paused?"симуляция на паузе":$"{actual:F1}× фактически · {snapshot.TicksPerSecond:F0} тиков/с";

        RefreshNavigator();
        RefreshTabs();
        RefreshOverlayButtons();

        if (_tab=="systems")
        {
            _inspectorTitle.Text="диагностика";
            _details.Text=$"{Engine.GetFramesPerSecond():F0} FPS\nцель {Game.Speed*SimulationRunner.BaseTicksPerSecond} тиков/с\nфактически {snapshot.TicksPerSecond:F0} тиков/с ({actual:F1}×)\nLOD {Game.View.CurrentLod} · чанков {Game.View.VisibleChunks}\nобъектов на экране {Game.View.VisibleObjects}\n\n"+snapshot.SystemsText;
        }
        else if (_tab=="world")
        {
            _inspectorTitle.Text="мир";
            _details.Text=snapshot.WorldText;
        }
        else if (_tab=="settlement")
        {
            var anchor=_settlementAnchor!=0?_settlementAnchor:Game.SelectedSettlement;
            var settlement=snapshot.Settlements.FirstOrDefault(x=>x.Anchor==anchor)??snapshot.Settlements.FirstOrDefault();
            if (settlement is null)
            {
                _inspectorTitle.Text="поселения";
                _details.Text="пока нет завершённых домов, которые можно объединить в поселение.";
            }
            else
            {
                _settlementAnchor=settlement.Anchor;
                _inspectorTitle.Text=settlement.Name;
                _details.Text=SettlementText(settlement);
            }
        }
        else
        {
            _inspectorTitle.Text=snapshot.Inspector.Title;
            _details.Text=snapshot.Inspector.Text;
        }
    }

    private static string SettlementText(RenderSettlement settlement)
    {
        var text=new StringBuilder();
        text.AppendLine($"центр  {settlement.Center.X}, {settlement.Center.Y}");
        text.AppendLine($"территория  {settlement.MaxX-settlement.MinX+1} × {settlement.MaxY-settlement.MinY+1} клеток");
        Section(text,"население");
        text.AppendLine($"жителей  {settlement.Members}");
        text.AppendLine($"семей  {settlement.Families}");
        Section(text,"застройка");
        text.AppendLine($"домов  {settlement.Homes}");
        text.AppendLine($"строится  {settlement.Projects}");
        Section(text,"запасы");
        text.AppendLine($"доступная еда  {settlement.FoodCalories:F0} ккал");
        if(settlement.Members>0)text.AppendLine($"на жителя  {settlement.FoodCalories/settlement.Members:F0} ккал");
        Section(text,"специализации");
        text.AppendLine(settlement.Specializations.Count==0?"ещё не выделились":string.Join(", ",settlement.Specializations));
        return text.ToString();
    }

    private static void Section(StringBuilder text,string title)=>
        text.AppendLine("\n[color=#b8c7a4]"+title.ToUpperInvariant()+"[/color]");
}
