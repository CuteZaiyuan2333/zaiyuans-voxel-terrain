using Godot;
using ZaiyuansVoxelWorld.Core;
using ZaiyuansVoxelWorld.ECS.Components;

namespace ZaiyuansVoxelWorld.Data;

public sealed class DefaultTerrainGenerator : IChunkGenerator
{
    private const int GroundLevel = 8;
    private const float NoiseScale = 0.02f;
    private const float NoiseHeightScale = 4f;
    private FastNoiseLite _noise;
    private int _lastSeed = int.MinValue;

    public void Generate(Vector3I chunkPos, VoxelData data, int seed)
    {
        if (_noise == null || _lastSeed != seed)
        {
            _noise = new FastNoiseLite { Seed = seed };
            _noise.SetNoiseType(FastNoiseLite.NoiseTypeEnum.Simplex);
            _noise.SetFrequency(0.03f);
            _lastSeed = seed;
        }
        var noise = _noise;

        int ox = chunkPos.X * VoxelConstants.ChunkSize;
        int oy = chunkPos.Y * VoxelConstants.ChunkSize;
        int oz = chunkPos.Z * VoxelConstants.ChunkSize;

        for (int lz = 0; lz < VoxelConstants.ChunkSize; lz++)
        for (int ly = 0; ly < VoxelConstants.ChunkSize; ly++)
        for (int lx = 0; lx < VoxelConstants.ChunkSize; lx++)
        {
            int wx = ox + lx;
            int wy = oy + ly;
            int wz = oz + lz;

            float n = noise.GetNoise3D(wx, 0, wz);
            int height = GroundLevel + (int)(n * NoiseHeightScale);

            byte blockId;
            if (wy > height)
                blockId = (byte)BlockId.Air;
            else if (wy == height)
                blockId = (byte)BlockId.Grass;
            else if (wy >= height - 3)
                blockId = (byte)BlockId.Dirt;
            else
                blockId = (byte)BlockId.Stone;

            data.Set(lx, ly, lz, blockId);
        }
    }
}
