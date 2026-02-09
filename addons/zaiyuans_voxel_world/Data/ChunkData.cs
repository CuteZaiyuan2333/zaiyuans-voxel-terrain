using System;
using ZaiyuansVoxelWorld.Core;

namespace ZaiyuansVoxelWorld.Data;

/// <summary>
/// 32×32×32 block storage for one chunk. Used by ECS VoxelData component.
/// </summary>
public sealed class ChunkData
{
    public const int Size = VoxelConstants.ChunkVolume;
    private readonly byte[] _blocks = new byte[Size];

    public byte Get(int lx, int ly, int lz)
    {
        return _blocks[VoxelConstants.LocalToIndex(lx, ly, lz)];
    }

    public void Set(int lx, int ly, int lz, byte blockId)
    {
        _blocks[VoxelConstants.LocalToIndex(lx, ly, lz)] = blockId;
    }

    public byte GetByIndex(int index) => _blocks[index];
    public void SetByIndex(int index, byte blockId) => _blocks[index] = blockId;

    public ReadOnlySpan<byte> AsSpan() => _blocks;
}
