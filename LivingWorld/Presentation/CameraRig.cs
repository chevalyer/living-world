namespace LivingWorld.Presentation;
public partial class CameraRig : Camera2D
{
    public GameRoot Game { get; set; } = null!;
    private bool _dragging;
    public override void _Ready()
    {
        Enabled=true;
        Zoom=Vector2.One*1.3f;
        PositionSmoothingEnabled=false;
    }
    public void Focus(GridPoint point)
    {
        Position=new Vector2(point.X+.5f, point.Y+.5f)*WorldView.TileSize;
        Zoom=Vector2.One*1.3f;
    }
    public void FitWorld()
    {
        if (Game.Snapshot is not { } map)return;
        var viewport=GetViewportRect().Size;
        var scale=MathF.Min((viewport.X-370)/(map.Width*WorldView.TileSize), (viewport.Y-180)/(map.Height*WorldView.TileSize));
        Zoom=Vector2.One*Math.Clamp(scale, .2f, 4);
        Position=new Vector2(map.Width*.5f+9, map.Height*.5f)*WorldView.TileSize;
    }
    public override void _Process(double delta)
    {
        if (!Input.IsMouseButtonPressed(MouseButton.Middle)) _dragging=false;
        if (GetViewport().GuiGetFocusOwner() is LineEdit or SpinBox)return;
        var direction=Vector2.Zero;
        if (Input.IsPhysicalKeyPressed(Key.W)||Input.IsPhysicalKeyPressed(Key.Up))direction.Y--;
        if (Input.IsPhysicalKeyPressed(Key.S)||Input.IsPhysicalKeyPressed(Key.Down))direction.Y++;
        if (Input.IsPhysicalKeyPressed(Key.A)||Input.IsPhysicalKeyPressed(Key.Left))direction.X--;
        if (Input.IsPhysicalKeyPressed(Key.D)||Input.IsPhysicalKeyPressed(Key.Right))direction.X++;
        Position+=direction.Normalized()*500*(float)delta/Zoom.X;
    }
    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton button)
        {
            if (button.ButtonIndex==MouseButton.Middle)_dragging=button.Pressed;
            if (button.Pressed&&button.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown)
            {
                var before=GetGlobalMousePosition();
                Zoom=Vector2.One*Math.Clamp(Zoom.X*(button.ButtonIndex==MouseButton.WheelUp?1.15f:1/1.15f), .2f, 5);
                var after=GetGlobalMousePosition();
                Position+=before-after;
            }
        }
        if (@event is InputEventMouseMotion motion&&_dragging)Position-=motion.Relative/Zoom;
    }
}
