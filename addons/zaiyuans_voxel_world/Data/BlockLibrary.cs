using System;
using Godot;

namespace ZaiyuansVoxelWorld.Data;

/// <summary>
/// Configurable list of block types (id + name) for world generators and game code.
/// Air (0) is always valid; entries may or may not include it.
/// </summary>
[GlobalClass]
public partial class BlockLibrary : Resource
{
    [Export] public Godot.Collections.Array<BlockLibraryEntry> Blocks { get; set; } = new();

    /// <summary>Get block id by name (case-sensitive). Returns 0 (Air) if not found.</summary>
    public byte GetIdByName(string name)
    {
        if (string.IsNullOrEmpty(name) || Blocks == null) return 0;
        for (int i = 0; i < Blocks.Count; i++)
        {
            var entry = Blocks[i];
            if (entry != null && entry.Name == name)
                return entry.Id;
        }
        return 0;
    }

    /// <summary>Get block name by id. Returns empty string if not found.</summary>
    public string GetNameById(byte id)
    {
        if (Blocks == null) return "";
        for (int i = 0; i < Blocks.Count; i++)
        {
            var entry = Blocks[i];
            if (entry != null && entry.Id == id)
                return entry.Name;
        }
        return "";
    }

    /// <summary>True if the library contains an entry with this id.</summary>
    public bool HasId(byte id)
    {
        if (Blocks == null) return false;
        for (int i = 0; i < Blocks.Count; i++)
        {
            var entry = Blocks[i];
            if (entry != null && entry.Id == id)
                return true;
        }
        return false;
    }
}
