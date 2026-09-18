namespace LivingWorld.Presentation;

using System.Threading.Tasks;

public partial class GameRoot : Node2D
{
    public RenderSnapshot? Snapshot { get; private set; }
    public WorldView View { get; private set; } = null!;
    public CameraRig Camera { get; private set; } = null!;
    public SimulationHud Hud { get; private set; } = null!;
    public int Selected { get; set; }
    public bool DebugView { get; set; }
    public bool Busy { get; private set; }
    private bool _paused = true, _closing;
    private int _speed = 1;
    private double _hudElapsed;
    private SimulationRunner? _runner;
    private long _generation = -1;
    private string SavePath => ProjectSettings.GlobalizePath("user://world.save.json");

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
            var definitions = DefinitionLoader.LoadText(name =>
            {
                using var file = Godot.FileAccess.Open("res://Definitions/Data/" + name, Godot.FileAccess.ModeFlags.Read);
                return file?.GetAsText() ?? throw new InvalidDataException("Нет файла определений: " + name);
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
            Hud.Status("мир создан · выбери жителя кликом");
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
        Hud.SelectedTile = null;
        Camera.Focus(snapshot.Start);
        View.ResetTerrain();
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
                case Key.F5: Save(); break;
                case Key.F9: Load(); break;
                case Key.F: Camera.FitWorld(); break;
                case Key.Home: if (Snapshot is { } world) Camera.Focus(world.Start); break;
                case Key.Escape: Selected = 0; break;
            }
        }
        if (@event is InputEventMouseButton mouse && mouse.Pressed && mouse.ButtonIndex == MouseButton.Left && Snapshot is { } snapshot)
        {
            var point = View.GetGlobalMousePosition() / WorldView.TileSize;
            var tile = new GridPoint((int)MathF.Floor(point.X), (int)MathF.Floor(point.Y));
            Selected = snapshot.People.Where(p => p.Tile.Distance(tile) <= 2).OrderBy(p => p.Tile.Distance(tile)).Select(p => p.Id).FirstOrDefault();
            Hud.SelectedTile = snapshot.Contains(tile) ? tile : null;
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
            Hud.Status(load ? "мир загружен · пробел — продолжить" : "мир сохранен · F9 — загрузить");
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
