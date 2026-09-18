namespace LivingWorld.Simulation;
public sealed class Relationship
{
    public float Familiarity { get; set; }
    public float Trust { get; set; } = .3f;
    public float Affection { get; set; } = .3f;
    public float Respect { get; set; } = .3f;
    public float Attachment { get; set; }
    public float Irritation { get; set; }
    public float Fear { get; set; }
    public float RomanticInterest { get; set; }
    public float Debt { get; set; }
    public long LastInteraction { get; set; }
}
