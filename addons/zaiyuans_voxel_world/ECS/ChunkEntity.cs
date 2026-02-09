using System;
using Godot;

namespace ZaiyuansVoxelWorld.ECS;

/// <summary>
/// Chunk entity identified by chunk coordinates. Used as key in ECS world.
/// </summary>
public readonly struct ChunkEntity : IEquatable<ChunkEntity>
{
	public Vector3I ChunkPos { get; }

	public ChunkEntity(Vector3I chunkPos) => ChunkPos = chunkPos;

	public long ToLongKey()
	{
		int cx = ChunkPos.X, cy = ChunkPos.Y, cz = ChunkPos.Z;
		return ((long)(uint)cx << 42) | ((long)(uint)cy << 21) | (long)(uint)cz;
	}

	public static ChunkEntity FromLongKey(long key)
	{
		int cx = (int)((key >> 42) & 0x1FFFFF);
		int cy = (int)((key >> 21) & 0x1FFFFF);
		int cz = (int)(key & 0x1FFFFF);
		if (cx >= 0x100000) cx -= 0x200000;
		if (cy >= 0x100000) cy -= 0x200000;
		if (cz >= 0x100000) cz -= 0x200000;
		return new ChunkEntity(new Vector3I(cx, cy, cz));
	}

	public bool Equals(ChunkEntity other) => ChunkPos == other.ChunkPos;
	public override bool Equals(object obj) => obj is ChunkEntity e && Equals(e);
	public override int GetHashCode() => ChunkPos.GetHashCode();
}
