using System.Text;
using LivingWorld.Simulation;
public static class MapSvg
{
    public static string Render(SimulationSession session)
    {
        var s=session.State;
        const int size=6;
        var text=new StringBuilder();
        text.Append($"<svg xmlns='http://www.w3.org/2000/svg' width='{s.Map.Width*size}' height='{s.Map.Height*size}' viewBox='0 0 {s.Map.Width*size} {s.Map.Height*size}' shape-rendering='crispEdges'>");
        for (var i=0; i<s.Map.Tiles.Length; i++)
        {
            var t=s.Map.Tiles[i];
            var p=s.Map.Point(i);
            var color=t.Water!=WaterKind.None?"#477b86":t.Biome switch
            {
                Biome.Forest=>"#66784b", Biome.Mountain=>"#8c8b7f", Biome.Beach=>"#c6b486", _=>"#8d9d63"
            };
            if (t.Ice>.15f)color="#b8d2d5";
            text.Append($"<rect x='{p.X*size}' y='{p.Y*size}' width='6' height='6' fill='{color}'/>");
        }
        foreach (var (id, plant) in s.Entities.Store<PlantComponent>().All)
        {
            var p=s.Entities.Get<PositionComponent>(id).Tile;
            var d=session.Definitions.Plants[plant.Definition];
            text.Append($"<rect x='{p.X*size+1}' y='{p.Y*size+1}' width='4' height='4' fill='{d.Color}'/>");
        }
        foreach (var (id, element) in s.Entities.Store<BuildingElementComponent>().All)
        {
            var p=s.Entities.Get<PositionComponent>(id).Tile;
            text.Append($"<rect x='{p.X*size}' y='{p.Y*size}' width='6' height='6' fill='#ae9269'/>");
        }
        foreach (var (id, health) in s.Entities.Store<HealthComponent>().All.Where(x=>x.Value.Alive))
        {
            var p=s.Entities.Get<PositionComponent>(id).Tile;
            text.Append($"<rect x='{p.X*size+1}' y='{p.Y*size+1}' width='4' height='4' fill='#fff2d0'/>");
        }
        return text.Append("</svg>").ToString();
    }
}
