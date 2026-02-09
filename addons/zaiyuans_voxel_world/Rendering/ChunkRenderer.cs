using Godot;

namespace ZaiyuansVoxelWorld.Rendering;

/// <summary>
/// Creates and configures MeshInstance3D for chunks (e.g. default material).
/// </summary>
public static class ChunkRenderer
{
    private static StandardMaterial3D _defaultMaterial;

    public static StandardMaterial3D GetDefaultMaterial()
    {
        if (_defaultMaterial == null)
        {
            _defaultMaterial = new StandardMaterial3D();
            _defaultMaterial.AlbedoColor = new Color(0.6f, 0.5f, 0.4f);
            _defaultMaterial.VertexColorUseAsAlbedo = false;
        }
        return _defaultMaterial;
    }

    public static MeshInstance3D CreateMeshInstance()
    {
        var mi = new MeshInstance3D();
        mi.MaterialOverride = GetDefaultMaterial();
        return mi;
    }
}
