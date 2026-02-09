# Voxel Gameplay Plugin - Detailed Design

> **Goal**: Common gameplay mechanics (interaction, inventory, saving structures) required by almost all voxel games.

---

## 1. Interaction System (`VoxelRaycast`)

Selecting a specific block in a mesh is non-trivial without a physics mesh collider (which is slow). We use **DDA (Digital Differential Analyzer)** traversal on the voxel grid.

### 1.1 Algorithm
*   **Input**: Ray Origin, Ray Direction, Max Distance.
*   **Output**: `HitBlockPosition` (int3), `HitNormal` (int3), `Distance` (float).
*   **Process**:
    *   Step through grid cells along the ray.
    *   Check `VoxelWorld.GetBlock(pos)`.
    *   If solid -> Return hit.
    *   If air -> Continue.

### 1.2 Placement Logic
*   **Break**: Remove block at `HitBlockPosition`.
*   **Place**: Add block at `HitBlockPosition + HitNormal`.
    *   Check AABB collision: Don't place if player is inside.

---

## 2. Structure System (`StructureBlock`)

Copy/Paste functionality for user creations.

### 2.1 Data Format
JSON or Binary file (`.structure`).
*   **Header**: Size (X, Y, Z), Author, Version.
*   **Palette**: List of unique Block IDs used (mapping local ID -> global name).
*   **Data**: RLE (Run-Length Encoded) voxel array.

### 2.2 Tools
*   **Selection Box**: Visual gizmo to select a region.
*   **Save/Load**: Serialize region to file.
*   **Preview**: Show ghost of structure before placing.

---

## 3. Inventory System (Basic)

A lightweight inventory backend.

*   `ItemStack`: Struct { ItemID, Count, Metadata }.
*   `Inventory`: Array of ItemStacks.
*   `Hotbar`: Reference to specific slots in Inventory.
*   **UI**: Simple `GridContainer` based UI (customizable).
*   **BlockId vs ItemID**: Use a single ID space for placeable blocks (BlockId from Terrain = ItemID for blocks), or a BlockLibrary/ItemTable mapping; on Place, resolve ItemID to BlockId and call Terrain.SetBlock.
*   **Selected block for VoxelPlayer**: Expose **ISelectedBlockProvider** or Hotbar.GetSelectedBlockId() so Entity's VoxelPlayer can request the block to place without depending on Gameplay's concrete types.

---

## 4. Implementation Plan

### Phase 1: Raycast & Interaction
1.  Implement `VoxelRaycast.Raycast()` in C#.
2.  Create `BlockHighlighter` node (wireframe box at selection).

### Phase 2: Structure System
1.  Define `.structure` file format.
2.  Implement `StructureSerializer` (Save/Load).
3.  Create `StructureBlock` node (visual selection tool).

### Phase 3: Inventory
1.  Create `Inventory` resource/class.
2.  Implement `Hotbar` logic.
3.  Create basic UI.
