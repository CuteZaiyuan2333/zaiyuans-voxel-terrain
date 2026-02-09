# Voxel Entity Plugin - Detailed Design

> **Goal**: A high-performance, modular entity system for voxel worlds, handling physics, collision, and basic AI behaviors.

---

## 1. Physics Engine (`VoxelPhysicsEngine`)

Godot's Jolt/PhysX is great for general rigid bodies, but voxel games often need specific, tight control over character movement (e.g., auto-stepping up distinct blocks, discrete sliding, fluid drag). We will implement a dedicated **swept AABB** physics engine.

### 1.1 Core Concept: Swept AABB
Instead of full mesh collision, characters are represented by axis-aligned bounding boxes (AABB).

**Update Loop (per physics tick):**
1.  **Velocity Integration**: `pos += velocity * delta`.
2.  **Broadphase**: Query terrain for potential colliding blocks in the AABB's path.
3.  **Narrowphase**: Perform swept AABB vs AABB tests.
    *   Find earliest time of impact (TOI).
    *   Resolve collision: slide along the collision normal (remove velocity component).
    *   Repeat until `delta` is consumed or max iterations reached.
4.  **Special Mechanics**:
    *   **Auto-Step**: If horizontal collision occurs at lower 0.5m, check if top is clear. If so, teleport up.
    *   **Fluid Drag**: Check if center is in water block -> apply drag and buoyancy.

### 1.2 The `VoxelCharacterBody3D` Node
A custom node inheriting `Node3D` (not `CharacterBody3D` to avoid fighting Godot physics).
*   **Properties**: `AABB Size`, `Mass`, `AutoStepHeight`, `MaxSlope`.
*   **Methods**: `MoveAndSlide()`, `Jump()`, `IsOnFloor()`.

---

## 2. Artificial Intelligence (`VoxelMob`)

AI agents need to navigate the voxel grid efficiently.

### 2.1 Navigation Mesh vs Grid Pathfinding
*   **NavMesh (Godot Standard)**: Baking NavMesh on dynamic voxel terrain is slow and complex.
*   **Grid Pathfinding (A*)**: Better for blocky worlds.
    *   Use `AStar3D` or implement JPS (Jump Point Search) in C#.
    *   **Node**: Center of block above solid ground.
    *   **Solid**: Terrain.GetBlock(pos) is a walkable type (e.g. not Air, not fluid source); define via BlockId or a configurable table.
    *   **Movement / step height**: Edges to 4 horizontal neighbors; optionally 4 diagonals. Max step height 1 block (configurable). Jump edges: from current node to node at +1 block height. Keeps A* edge costs consistent.
    *   **Edge**: Connect to 4 horizontal neighbors + 4 diagonal neighbors (optional) + Jump connections (up/down).

### 2.2 Behavior Architecture
Use a **Behavior Tree** or **GOAP** (Goal Oriented Action Planning).
*   **Actions**:
    *   `MoveTo(target)`: Request path from A* system.
    *   `Attack(target)`: Raycast check + cooldown.
    *   `Wander()`: Pick random valid block nearby.

---

## 3. Player Controller (`VoxelPlayer`)
A high-level wrapper around `VoxelCharacterBody3D` + Input handling.
*   **Camera**: FPS/TPS toggle.
*   **Interaction**: Left-click (Break), Right-click (Place).
*   **Selected block for Place**: The "currently selected block" is provided by the **Gameplay** module—e.g. via an interface such as `ISelectedBlockProvider` or by Gameplay scripts that read the hotbar and call Terrain.SetBlock on place. Entity should not depend on Gameplay's concrete Inventory class; use an interface or signal so VoxelPlayer only requests "block to place" and Gameplay supplies it.

---

## 4. Implementation Plan

### Phase 1: Physics Core
1.  Create `VoxelPhysicsEngine.cs` (static helper or singleton).
2.  Implement `SweptAABB(box, velocity, world)` algorithm.
3.  Create `VoxelCharacterBody3D` node.

### Phase 2: Player & Interaction
1.  Implement `VoxelPlayer.cs`.
2.  Connect Input -> Velocity -> MoveAndSlide.

### Phase 3: AI & Pathfinding
1.  Implement `VoxelAStar.cs`.
2.  Create `VoxelMob` base class.
3.  Implement basic Zombie/Skeleton behavior.
