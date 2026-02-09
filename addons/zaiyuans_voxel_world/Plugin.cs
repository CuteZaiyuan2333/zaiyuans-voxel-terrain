#if TOOLS
using Godot;

namespace ZaiyuansVoxelWorld;

[Tool]
public partial class Plugin : EditorPlugin
{
    private Script _voxelTerrainScript;

    public override void _EnterTree()
    {
        _voxelTerrainScript = GD.Load<Script>("res://addons/zaiyuans_voxel_world/VoxelTerrain.cs");
        AddCustomType("VoxelTerrain", "Node3D", _voxelTerrainScript, null);
    }

    public override void _ExitTree()
    {
        RemoveCustomType("VoxelTerrain");
    }

    public override string _GetPluginName()
    {
        return "zaiyuan's voxel world";
    }
}
#endif
