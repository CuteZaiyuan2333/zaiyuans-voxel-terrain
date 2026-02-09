using System;
using System.IO;
using System.IO.Compression;
using Godot;
using ZaiyuansVoxelWorld.Core;

namespace ZaiyuansVoxelWorld.Data;

/// <summary>
/// Saves and loads chunk data under a world save directory.
/// Format: {SaveDir}/chunks/cx_cy_cz.chunk — 4-byte magic, 4-byte version, then deflate-compressed 32³ block bytes.
/// </summary>
public static class ChunkStorage
{
    public const uint Magic = 0x5658434B; // "VXCK"
    public const int Version = 1;

    public static string GetChunkPath(string saveDir, int cx, int cy, int cz)
    {
        if (string.IsNullOrEmpty(saveDir)) return string.Empty;
        string chunksDir = Path.Combine(saveDir, "chunks");
        return Path.Combine(chunksDir, $"{cx}_{cy}_{cz}.chunk");
    }

    /// <summary>Save chunk block data to disk. Creates directory if needed. Returns true on success.</summary>
    public static bool Save(string saveDir, Vector3I chunkPos, ReadOnlySpan<byte> data)
    {
        if (string.IsNullOrEmpty(saveDir)) return false;
        if (data.Length < VoxelConstants.ChunkVolume) return false;
        string path = GetChunkPath(saveDir, chunkPos.X, chunkPos.Y, chunkPos.Z);
        try
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            using var fs = new FileStream(path, System.IO.FileMode.Create, System.IO.FileAccess.Write, System.IO.FileShare.None);
            var bw = new BinaryWriter(fs);
            bw.Write(Magic);
            bw.Write(Version);
            bw.Flush();
            using (var deflate = new DeflateStream(fs, CompressionLevel.Fastest, true))
            {
                deflate.Write(data.Slice(0, VoxelConstants.ChunkVolume));
            }
            return true;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[ChunkStorage] Save failed {path}: {ex.Message}");
            return false;
        }
    }

    /// <summary>Load chunk from disk into data. Returns true if file existed and was valid.</summary>
    public static bool TryLoad(string saveDir, Vector3I chunkPos, byte[] buffer)
    {
        if (string.IsNullOrEmpty(saveDir) || buffer == null || buffer.Length < VoxelConstants.ChunkVolume)
            return false;
        string path = GetChunkPath(saveDir, chunkPos.X, chunkPos.Y, chunkPos.Z);
        if (!File.Exists(path)) return false;
        try
        {
            using var fs = new FileStream(path, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read);
            var br = new BinaryReader(fs);
            uint magic = br.ReadUInt32();
            int version = br.ReadInt32();
            if (magic != Magic || version != Version)
            {
                GD.PrintErr($"[ChunkStorage] Invalid format at {path} (magic/version)");
                return false;
            }
            using var deflate = new DeflateStream(fs, CompressionMode.Decompress, true);
            int read = 0;
            while (read < VoxelConstants.ChunkVolume)
            {
                int n = deflate.Read(buffer, read, VoxelConstants.ChunkVolume - read);
                if (n <= 0) break;
                read += n;
            }
            return read == VoxelConstants.ChunkVolume;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[ChunkStorage] Load failed {path}: {ex.Message}");
            return false;
        }
    }
}
