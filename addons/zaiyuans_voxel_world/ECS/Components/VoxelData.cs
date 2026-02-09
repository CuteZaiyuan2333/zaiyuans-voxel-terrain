using System;
using Godot;
using ZaiyuansVoxelWorld.Core;

namespace ZaiyuansVoxelWorld.ECS.Components;

/// <summary>
/// 32×32×32 block IDs for one chunk. One byte per voxel.
/// </summary>
public sealed class VoxelData
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

    public byte GetByIndex(int index)
    {
        return _blocks[index];
    }

    public void SetByIndex(int index, byte blockId)
    {
        _blocks[index] = blockId;
    }

    public ReadOnlySpan<byte> AsSpan() => _blocks;

    /// <summary>Copy chunk block data from source (at least Size bytes). Used when loading from disk.</summary>
    public void SetFrom(ReadOnlySpan<byte> source)
    {
        int copy = Math.Min(Size, source.Length);
        source.Slice(0, copy).CopyTo(_blocks.AsSpan(0, copy));
    }
}
