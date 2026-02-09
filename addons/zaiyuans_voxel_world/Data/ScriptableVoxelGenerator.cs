using Godot;
using ZaiyuansVoxelWorld.ECS.Components;

namespace ZaiyuansVoxelWorld.Data;

/// <summary>
/// Placeholder chunk generator that forwards to a user script (GDScript) via VoxelBufferWrapper.
/// Phase 1: Currently delegates to DefaultTerrainGenerator; will be replaced with full GDScript
/// bridge (VoxelBufferWrapper + calling _generate on GeneratorResource) when the bridge is implemented.
/// </summary>
public sealed class ScriptableVoxelGenerator : IChunkGenerator
{
    /// <summary>User-provided Resource (e.g. GDScript extending VoxelGeneratorResource). Not yet used; bridge pending.</summary>
    public Resource GeneratorResource { get; set; }

    private readonly DefaultTerrainGenerator _fallback = new DefaultTerrainGenerator();

    public void Generate(Vector3I chunkPos, VoxelData data, int seed, BlockLibrary blockLibrary = null)
    {
        // TODO Phase 1: Call GeneratorResource._generate(VoxelBufferWrapper, chunkPos) when VoxelBufferWrapper exists.
        _fallback.Generate(chunkPos, data, seed, blockLibrary);
    }
}
