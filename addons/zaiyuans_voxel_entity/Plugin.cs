#if TOOLS
using Godot;

namespace ZaiyuansVoxelEntity;

[Tool]
public partial class Plugin : EditorPlugin
{
    public override void _EnterTree()
    {
        // Register VoxelPlayer
        var playerScript = GD.Load<Script>("res://addons/zaiyuans_voxel_entity/Entities/VoxelPlayer.cs");
        AddCustomType("VoxelPlayer", "CharacterBody3D", playerScript, null);

        // Register VoxelMob
        var mobScript = GD.Load<Script>("res://addons/zaiyuans_voxel_entity/Entities/VoxelMob.cs");
        AddCustomType("VoxelMob", "CharacterBody3D", mobScript, null);
    }

    public override void _ExitTree()
    {
        // Clean-up
        RemoveCustomType("VoxelPlayer");
        RemoveCustomType("VoxelMob");
    }
}
#endif
