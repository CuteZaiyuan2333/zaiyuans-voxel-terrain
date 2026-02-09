using Godot;

namespace ZaiyuansVoxelWorld.Data;

/// <summary>
/// Single block definition in a BlockLibrary. Id = 0 is reserved for Air.
/// </summary>
[GlobalClass]
public partial class BlockLibraryEntry : Resource
{
    [Export] public byte Id { get; set; }
    [Export] public string Name { get; set; } = "";
}
