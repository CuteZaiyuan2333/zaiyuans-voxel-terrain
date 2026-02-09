# Voxel Terrain Plugin - Detailed Design

> **Goal**: A high-performance, threading-aware voxel storage and meshing engine that exposes safe, efficient generation hooks to GDScript.

---

## 1. Architecture Overview

### 1.1 Core Components (C#)
1.  **VoxelData**: Flattened 1D array `BlockId[]` representing a 32x32x32 chunk.
2.  **ChunkMesher**: Greedy mesher generating optimized `ArrayMesh` with UVs and normals.
3.  **VoxelWorld**: ECS World managing chunk loading/unloading based on player position.
4.  **job System**: `System.Threading.Tasks` or `Thread` pool for heavy operations.

### 1.2 The GDScript Bridge (Scriptable Generator)

The biggest challenge is allowing users to write terrain logic in GDScript without killing performance. We solve this with the **Command Buffer** pattern.

#### Architecture:
```mermaid
graph TD
    A[VoxelWorld (C#)] -->|Request Chunk| B[ScriptableGenerator (C#)]
    B -->|Create| C[VoxelBufferWrapper (C# / Godot Object)]
    B -->|Call _generate| D[User Script (GDScript)]
    D -->|Calls helper methods| C
    C -->|Writing to Pointer/Unsafe| E[Raw Voxel Data]
    D -->|Return| B
```

#### `VoxelBufferWrapper` API Design
This wrapper exposes high-level *bulk operations* to GDScript.

```csharp
// C# Pseudo-code
public partial class VoxelBufferWrapper : RefCounted
{
    private VoxelData _data; // Internal reference to the chunk data being built

    // 1. Basic Set (Slow, use sparingly)
    public void SetBlock(int x, int y, int z, int blockId) { ... }

    // 2. Bulk Fill (Fast - use this!)
    public void Fill(int blockId) { ... }
    
    // 3. Noise Fill (Very Fast - C++ level optimized)
    // Uses a connected FastNoiseLite instance to fill the buffer based on threshold
    public void FillNoise(FastNoiseLite noise, float threshold, int solidBlockId, int airBlockId) { ... }
    
    // 4. Heightmap Fill (Standard Terrain)
    // Iterates x/z, samples noise for height, loops y to set blocks
    public void FillHeightmap(FastNoiseLite noise, int baseHeight, float strength, int groundBlockId, int airBlockId) { ... }
}
```

---

## 2. API for Other Plugins (`IVoxelQuery`)

To decouple physics and AI, the terrain plugin must provide a clean query interface.

```csharp
public interface IVoxelQuery 
{
    // Basic Query
    int GetBlock(Vector3 globalPos);
    
    // Raycast
    bool Raycast(Vector3 origin, Vector3 direction, float maxDist, out Vector3 hitPos, out Vector3 normal, out int blockId);
    
    // AABB Check (for Physics)
    // Returns a list of AABBs that intersect the given box
    void GetCollidingBoxes(AABB Box, List<AABB> output);
}
```

---

## 3. Implementation Plan

### Phase 1: The Bridge
1.  Create `VoxelBufferWrapper.cs` inheriting `Godot.RefCounted`.
2.  Implement `ScriptableVoxelGenerator.cs` inheriting `IChunkGenerator`.
    *   Expose `Generate(VoxelData data, Vector3i position)` to C#.
    *   Internally call `UserScript.Call("_generate", new VoxelBufferWrapper(data), position)`.

### Phase 2: Optimization
1.  Implement `FillHeightmap` in C# using `unsafe` pointers for maximum iteration speed.
2.  Implement `FillNoise` using SIMD if possible (or standard loops).

### Phase 3: editor Tools
1.  Add `[Export]` `GeneratorResource` to `VoxelTerrain` node.
2.  Allow hot-reloading of the generator script (checking if `UserScript` changed).
