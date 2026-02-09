# Voxel Atmosphere Plugin - Detailed Design

> **Goal**: Aesthetic enhancements (sky, clouds) and dynamic fluid simulation for voxel worlds.

---

## 1. Volumetric Clouds

Godot's standard environment is great, but voxel games benefit from stylized, blocky or soft volumetric clouds that match the terrain scale.

### 1.1 Technique: Raymarching
Render clouds using a **ShaderMaterial** on a large box or fullscreen quad.
*   **Raymarch Loop**: Cast ray from camera. Step through volume.
*   **Density Sampling**: Sample a 3D Noise Texture.
    *   Map noise value to density.
    *   Apply coverage threshold (weather system).
*   **Lighting**: Simple directional lighting (Beer's Law for absorption).
*   **Optimization**:
    *   Render at quarter resolution and upscale.
    *   Use reprojection/temporal accumulation if possible.

### 1.2 Integration
*   `VoxelSky` node: Wraps the `WorldEnvironment` and cloud shader.
*   **Day/Night Cycle**:
    *   Rotate `DirectionalLight3D`.
    *   Update Sky shader module (sun position, horizon color).
    *   Emit signals: `Signal DayStarted`, `Signal NightStarted`.
    *   Expose **DayPhase** (0–1) or **TimeOfDay** property for sky shaders and game logic (e.g. mob spawn, sleep).

---

## 2. Fluid Simulation

Water and lava need to flow. We prioritize **gameplay logic** over physical accuracy.

### 2.1 Cellular Automata model
Each fluid block has a "level" (1-8).
*   **Source block**: Level 8.
*   **Flow Rules**:
    1.  **Down**: If block below is Air/Fluid, flow down (reset to Level 8 if falling).
    2.  **Side**: If blocked below, flow to 4 horizontal neighbors with Level - 1.
    3.  **Mix**: Water + Lava source = Obsidian; Water + Flowing Lava = Cobblestone.

### 2.2 The Simulation Loop
*   **Tick Rate**: Fluids update slower than physics (e.g., 5-10 ticks/sec).
*   **Active List**: Maintain a list of "Active Fluid Blocks".
    *   Only update blocks that changed or have neighbors that changed.
    *   Sleep chunks with no active fluids.
*   **Meshing**:
    *   **Separate Mesh**: Don't merge with terrain mesh (prevents rebuilding huge terrain chunks for small water updates).
    *   **Face Culling**: Don't render faces between two fluid blocks.

### 2.3 Fluid Data and Terrain

Where fluid *data* lives must be decided so flow rules ("block below is Air/Fluid") have a single source of truth.

*   **Option A — Fluid in Terrain**: Fluid lives inside terrain (e.g. extend VoxelData with FluidType + Level). Terrain owns storage and chunk save/load; Atmosphere only runs flow logic and rendering.
*   **Option B — Fluid in Atmosphere**: Atmosphere keeps a separate fluid grid; Terrain exposes GetBlock, Atmosphere exposes GetFluid(pos). Flow logic in Atmosphere queries both.

**Choice for now**: Option A — fluid as part of VoxelData. Simpler save/load and one query API (Terrain) for "block + fluid" at a position. Implement Terrain extension first, then FluidSimulator + FluidMesher in Atmosphere.

---

## 3. Implementation Plan

### Phase 1: Sky & Clouds
1.  Create `VoxelSky` node.
2.  Write `volumetric_clouds.gdshader`.
3.  Implement Day/Night cycle script.

### Phase 2: Fluid Core
1.  Define `FluidBlock` struct (Type, Level).
2.  Implement `FluidSimulator` class (C#).
3.  Create `FluidMesher` (specialized for liquids - simpler greedy meshing).

### Phase 3: Interactions
1.  Add fluid drag to `VoxelPhysicsEngine`.
2.  Add underwater post-processing (fog, blue tint).
