using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;
using SharpDX;

namespace ExileRitualEj;

public class ExileRitualEjSettings : ISettings
{
    public ToggleNode Enable { get; set; } = new ToggleNode(false);
    
    public ToggleNode RenderGiganticSpawnPositions { get; set; } = new ToggleNode(true);
    public ColorNode GiganticSpawnColor { get; set; } = new ColorNode(Color.White);
    
    public ToggleNode RenderRitualRadius { get; set; } = new ToggleNode(true);
    public ColorNode RitualRadiusColor { get; set; } = new ColorNode(Color.Orange);
    
    public ToggleNode RenderGiganticName { get; set; } = new ToggleNode(true);
}