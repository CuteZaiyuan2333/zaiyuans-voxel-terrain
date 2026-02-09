#if TOOLS
using Godot;
using ZaiyuansVoxelWorld.Editor;

namespace ZaiyuansVoxelWorld;

[Tool]
public partial class Plugin : EditorPlugin
{
    private Script _voxelTerrainScript;
    private EditorInspectorPlugin _inspectorPlugin;

    public override void _EnterTree()
    {
        _voxelTerrainScript = GD.Load<Script>("res://addons/zaiyuans_voxel_world/VoxelTerrain.cs");
        AddCustomType("VoxelTerrain", "Node3D", _voxelTerrainScript, null);
        _inspectorPlugin = new VoxelTerrainInspectorPlugin();
        AddInspectorPlugin(_inspectorPlugin);
    }

    public override void _ExitTree()
    {
        RemoveInspectorPlugin(_inspectorPlugin);
        RemoveCustomType("VoxelTerrain");
    }

    public override string _GetPluginName()
    {
        return "zaiyuan's voxel world";
    }
}
#endif
