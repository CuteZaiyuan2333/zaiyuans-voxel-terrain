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
    /// <summary>User-provided Resource (e.g. GDScript extending VoxelGeneratorResource).</summary>
    public Resource GeneratorResource { get; set; }

    private readonly DefaultTerrainGenerator _fallback = new DefaultTerrainGenerator();

    public void Generate(Vector3I chunkPos, VoxelData data, int seed, BlockLibrary blockLibrary = null)
    {
        if (GeneratorResource != null && GeneratorResource.HasMethod("_generate_chunk"))
        {
            var wrapper = new VoxelBufferWrapper();
            wrapper.SetData(data);
            
            // Call GDScript: _generate_chunk(buffer, chunk_pos)
            // Note: blockLibrary and seed are not passed yet, could be added to wrapper or args.
            // For now specific plan says: _generate_chunk(buffer: VoxelBufferWrapper, chunk_pos: Vector3i)
            GeneratorResource.Call("_generate_chunk", wrapper, chunkPos);
        }
        else
        {
            _fallback.Generate(chunkPos, data, seed, blockLibrary);
        }
    }
}
