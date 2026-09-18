namespace LivingWorld.Presentation;

using System.Threading.Tasks;

public partial class GameRoot : Node2D
{
    public RenderSnapshot? Snapshot { get; private set; }
    public WorldView View { get; private set; } = null!;
    public CameraRig Camera { get; private set; } = null!;
    public SimulationHud Hud { get; private set; } = null!;
    public int Selected { get; private set; }
    public int SelectedSettlement { get; private set; }
    public MapOverlay Overlay { get; private set; }
    public bool DebugView { get; set; }
    public bool Busy { get; private set; }

    private bool _paused = true, _closing;
    private int _speed = 1;
    private double _hudElapsed;
    private SimulationRunner? _runner;
    private long _generation = -1;
    private int _saveSlot=1;
    public int SaveSlot
    {
        get=>_saveSlot;
        set=>_saveSlot=Math.Clamp(value,1,3);
    }
    private string SavePath => ProjectSettings.GlobalizePath("user://"+SaveFileName(SaveSlot));
    public static string SaveFileName(int slot)=>slot switch
    {
        1=>"world.save.json",
        2=>"world-slot-2.save.json",
        3=>"world-slot-3.save.json",
        _=>throw new ArgumentOutOfRangeException(nameof(slot))
    };

    public bool Paused
    {
        get => _paused;
        set { if (_paused == value) return; _paused = value; _ = ApplyControls(); }
    }

    public int Speed
    {
        get => _speed;
        set { if (_speed == value) return; _speed = value; _ = ApplyControls(); }
    }

    private static IEnumerable<string> DefinitionFiles(string group)
    {
        var root="res://Definitions/Data/"+group;
        return Walk(root, group);

        static IEnumerable<string> Walk(string directory, string relative)
        {
            foreach (var file in DirAccess.GetFilesAt(directory)
                         .Where(name=>name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                         .OrderBy(name=>name, StringComparer.Ordinal))
                yield return relative+"/"+file;
            foreach (var child in DirAccess.GetDirectoriesAt(directory).OrderBy(name=>name, StringComparer.Ordinal))
                foreach (var file in Walk(directory+"/"+child, relative+"/"+child))
                    yield return file;
        }
    }

    public override void _Ready()
    {
        TextureFilter = TextureFilterEnum.Nearest;
        Camera = new CameraRig { Game = this };
        AddChild(Camera);
        View = new WorldView { Game = this };
        AddChild(View);
        Hud = new SimulationHud { Game = this };
        AddChild(Hud);
        try
        {
            var definitions = DefinitionLoader.LoadFiles(DefinitionFiles, path =>
            {
                using var file = Godot.FileAccess.Open("res://Definitions/Data/" + path, Godot.FileAccess.ModeFlags.Read);
                return file?.GetAsText() ?? throw new InvalidDataException("Нет файла определений: " + path);
            });
            _runner = new SimulationRunner(definitions);
            _ = NewWorld(1847, 128, 14);
        }
        catch (Exception ex) { Report("Не удалось загрузить определения", ex); }
    }

    private async Task ApplyControls()
    {
        if (_runner is null || _closing) return;
        try { await _runner.SetControlsAsync(_paused, _speed); }
        catch (Exception ex) { if (!_closing) Report("Не удалось изменить скорость", ex); }
    }

    public async Task NewWorld(int seed, int size, int people)
    {
        if (Busy || _runner is null || _closing) return;
        Busy = true;
        var previousPause = _paused;
        Paused = true;
        Hud.Status("создается мир…");
        try
        {
            await _runner.NewWorldAsync(seed, size, people);
            if (_closing) return;
            _paused = false;
            AcceptSnapshot();
            Hud.Status("мир создан · выбери жителя или используй поиск");
        }
        catch (Exception ex)
        {
            if (!_closing) { Paused = previousPause; Report("Ошибка генерации", ex); }
        }
        finally { Busy = false; }
    }

    private void AcceptSnapshot()
    {
        var snapshot = _runner?.Latest;
        if (snapshot is null) return;
        Snapshot = snapshot;
        if (_generation == snapshot.Generation) return;
        _generation = snapshot.Generation;
        Selected = snapshot.People.FirstOrDefault().Id;
        SelectedSettlement = 0;
        Overlay = MapOverlay.None;
        Hud.SelectedTile = null;
        Camera.Focus(snapshot.Start);
        View.ResetTerrain();
        Hud.WorldChanged();
        Hud.Refresh();
    }

    public void FocusPerson(int id)
    {
        if (Snapshot is not { } snapshot)return;
        var person=snapshot.People.FirstOrDefault(x=>x.Id==id);
        if (person.Id==0)return;
        Selected=id;
        SelectedSettlement=0;
        Hud.SelectedTile=person.Tile;
        Camera.Focus(person.Tile);
        Hud.ShowNpc();
        _runner?.SetView(new(Selected,Hud.SelectedTile,DebugView));
        Hud.Refresh();
    }

    public void FocusSettlement(int anchor)
    {
        if (Snapshot is not { } snapshot)return;
        var settlement=snapshot.Settlements.FirstOrDefault(x=>x.Anchor==anchor);
        if (settlement is null)return;
        Selected=0;
        SelectedSettlement=anchor;
        Hud.SelectedTile=settlement.Center;
        SetOverlay(MapOverlay.Settlements);
        Camera.FocusArea(settlement.MinX,settlement.MinY,settlement.MaxX,settlement.MaxY);
        Hud.ShowSettlement(anchor);
        _runner?.SetView(new(0,Hud.SelectedTile,DebugView));
        Hud.Refresh();
    }

    public void SetOverlay(MapOverlay overlay)
    {
        Overlay=overlay;
        View.QueueRedraw();
        Hud.RefreshOverlayButtons();
    }

    private void CycleOverlay()
    {
        var values=Enum.GetValues<MapOverlay>();
        SetOverlay(values[((int)Overlay+1)%values.Length]);
    }

    private void ClearSelection()
    {
        Selected=0;
        SelectedSettlement=0;
        Hud.SelectedTile=null;
        Hud.ShowNpc();
        _runner?.SetView(new(0,null,DebugView));
        Hud.Refresh();
    }

    public override void _Process(double delta)
    {
        if (_closing) return;
        AcceptSnapshot();
        _runner?.SetView(new(Selected, Hud.SelectedTile, DebugView));
        if (_runner is not null && _runner.TryGetError(out var error))
        {
            _paused = true;
            Hud.Status("Симуляция остановлена из-за ошибки. Подробности — в журнале Godot.");
            GD.PushError(error);
        }
        _hudElapsed += delta;
        if (_hudElapsed >= .25) { _hudElapsed = 0; Hud.Refresh(); }
        View.QueueRedraw();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey key && key.Pressed && !key.Echo)
        {
            switch (key.Keycode)
            {
                case Key.Space: if (!Busy) Paused = !Paused; break;
                case Key.Key1: Speed = 1; break;
                case Key.Key2: Speed = 4; break;
                case Key.Key3: Speed = 12; break;
                case Key.Key4: Speed = 32; break;
                case Key.F2: DebugView = !DebugView; break;
                case Key.F3: CycleOverlay(); break;
                case Key.H: Hud.ToggleInterface(); break;
                case Key.F5: Save(); break;
                case Key.F9: Load(); break;
                case Key.F when key.CtrlPressed: Hud.FocusSearch(); break;
                case Key.F: Camera.FitWorld(); break;
                case Key.Home: if (Snapshot is { } world) Camera.Focus(world.Start); break;
                case Key.Escape: ClearSelection(); break;
            }
        }

        if (@event is InputEventMouseButton mouse && mouse.Pressed && mouse.ButtonIndex == MouseButton.Left && Snapshot is { } snapshot)
        {
            var point = View.GetGlobalMousePosition() / WorldView.TileSize;
            var tile = new GridPoint((int)MathF.Floor(point.X), (int)MathF.Floor(point.Y));
            Selected = snapshot.People.Where(p => p.Tile.Distance(tile) <= 2)
                .OrderBy(p => p.Tile.Distance(tile)).Select(p => p.Id).FirstOrDefault();
            SelectedSettlement=0;
            Hud.SelectedTile = snapshot.Contains(tile) ? tile : null;
            if (Selected!=0)Hud.ShowNpc();
            _runner?.SetView(new(Selected, Hud.SelectedTile, DebugView));
            Hud.Refresh();
        }
    }

    public void Save() => _ = SaveOrLoad(load: false);
    public void Load() => _ = SaveOrLoad(load: true);

    private async Task SaveOrLoad(bool load)
    {
        if (Busy || _runner is null || _closing || (!load && Snapshot is null)) return;
        Busy = true;
        try
        {
            var path = SavePath;
            if (load) await _runner.LoadAsync(path);
            else await _runner.SaveAsync(path);
            if (_closing) return;
            if (load) { _paused = true; AcceptSnapshot(); }
            Hud.Status(load
                ? $"слот {SaveSlot} загружен · пробел — продолжить"
                : $"мир сохранен в слот {SaveSlot}");
        }
        catch (Exception ex) { if (!_closing) Report(load ? "Загрузка не удалась" : "Сохранение не удалось", ex); }
        finally { Busy = false; }
    }

    private void Report(string operation, Exception ex)
    {
        Hud.Status(operation + ": " + ex.Message);
        GD.PushError(ex.ToString());
    }

    public override void _ExitTree()
    {
        _closing = true;
        _runner?.Dispose();
        _runner = null;
    }
}
