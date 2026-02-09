using Godot;
using System;
using ZaiyuansVoxelWorld.Core;
using ZaiyuansVoxelWorld.ECS.Components;

namespace ZaiyuansVoxelWorld.Data;

/// <summary>
/// A wrapper around VoxelData exposed to GDScript for high-performance generation.
/// Instance passed to VoxelGeneratorResource._generate_chunk().
/// </summary>
[GlobalClass]
public partial class VoxelBufferWrapper : RefCounted
{
    private VoxelData _data;

    // Required for Godot object creation
    public VoxelBufferWrapper() { }

    public void SetData(VoxelData data)
    {
        _data = data;
    }

    /// <summary>
    /// Set a block at local chunk coordinates (0-31).
    /// </summary>
    public void SetBlock(int x, int y, int z, int blockId)
    {
        if (_data == null) return;
        if (x < 0 || x >= VoxelConstants.ChunkSize ||
            y < 0 || y >= VoxelConstants.ChunkSize ||
            z < 0 || z >= VoxelConstants.ChunkSize) return;
        
        _data.Set(x, y, z, (byte)blockId);
    }

    /// <summary>
    /// Get a block at local chunk coordinates (0-31).
    /// </summary>
    public int GetBlock(int x, int y, int z)
    {
        if (_data == null) return 0;
        if (x < 0 || x >= VoxelConstants.ChunkSize ||
            y < 0 || y >= VoxelConstants.ChunkSize ||
            z < 0 || z >= VoxelConstants.ChunkSize) return 0;

        return _data.Get(x, y, z);
    }

    /// <summary>
    /// Set a block using a Vector3I local coordinate.
    /// </summary>
    public void SetBlockV(Vector3I localPos, int blockId)
    {
        SetBlock(localPos.X, localPos.Y, localPos.Z, blockId);
    }

    /// <summary>
    /// Get a block using a Vector3I local coordinate.
    /// </summary>
    public int GetBlockV(Vector3I localPos)
    {
        return GetBlock(localPos.X, localPos.Y, localPos.Z);
    }

    /// <summary>
    /// Fill the entire chunk with a single block ID.
    /// Fast operation.
    /// </summary>
    public void FillSolid(int blockId)
    {
        if (_data == null) return;
        // Optimization: loop is fast enough for 32k items
        for (int i = 0; i < VoxelData.Size; i++)
        {
            _data.SetByIndex(i, (byte)blockId);
        }
    }

    /// <summary>
    /// Fill using a FastNoiseLite for simple terrain.
    /// Values < baseHeight + noise are filled. Top layer is surfaceBlock, below is fillBlock.
    /// </summary>
    public void FillWithNoise(FastNoiseLite noise, float baseHeight, float amplitude, int surfaceBlock, int fillBlock, Vector3I chunkPos)
    {
        if (_data == null || noise == null) return;

        // Global position of the chunk's origin
        float globalXStart = chunkPos.X * VoxelConstants.ChunkSize;
        float globalYStart = chunkPos.Y * VoxelConstants.ChunkSize;
        float globalZStart = chunkPos.Z * VoxelConstants.ChunkSize;

        for (int lx = 0; lx < VoxelConstants.ChunkSize; lx++)
        {
            for (int lz = 0; lz < VoxelConstants.ChunkSize; lz++)
            {
                // Calculate global X, Z for noise lookup
                float gx = globalXStart + lx;
                float gz = globalZStart + lz;

                // Get noise value (-1..1 usually)
                float n = noise.GetNoise2D(gx, gz);
                
                // Calculate terrain height at this column
                float terrainHeight = baseHeight + (n * amplitude);

                // Iterate vertical column within this chunk
                for (int ly = 0; ly < VoxelConstants.ChunkSize; ly++)
                {
                    float gy = globalYStart + ly;
                    
                    if (gy <= terrainHeight)
                    {
                        // Use surface block for the top layer, fill block for below
                        // Simple logic: if next voxel is air (above terrainHeight), this is surface? 
                        // Actually since we process chunk by chunk, we can't easily know "next" if it's in another chunk 
                        // without neighbor checks. 
                        // Simplified: absolute height check. 
                        // If gy is close to terrainHeight (e.g. within 1 unit), surface.
                        
                        byte blockToSet = (byte)fillBlock;
                        if (gy >= terrainHeight - 1.0f) 
                        {
                            blockToSet = (byte)surfaceBlock;
                        }
                        
                        _data.Set(lx, ly, lz, blockToSet);
                    }
                }
            }
        }
    }
}
