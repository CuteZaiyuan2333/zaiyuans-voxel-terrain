# Minecraft-Maker Project Overview

> **Goal**: Build a modular, plug-and-play **Minecraft-Maker** toolkit for Godot 4.6+ (C# & GDScript).

This project aims to provide a suite of high-performance C# plugins that expose easy-to-use GDScript APIs, allowing users to build voxel games purely by dragging nodes and writing simple scripts.

---

## 1. Plugin Suite Architecture

The current implementation is a single addon **`zaiyuans_voxel_world`** (in `addons/zaiyuans_voxel_world`), which contains the core terrain logic. The system is divided into four major independent *logical* modules below; these may be split into separate addons in the future. Click the links for detailed technical design documents.

### [1. Core Terrain System (terrain module, in `zaiyuans_voxel_world`)](./DESIGN_VOXEL_TERRAIN.md)
*   **Role**: The engine. Handles data storage, meshing, threading, and save/load.
*   **Key Feature**: **Polyglot Generation**. Allows users to write terrain generation logic in GDScript via a high-performance C# bridge (`ScriptableVoxelGenerator`).
*   **Tech Stack**: C# ECS, Job System, Greedy Meshing.

### [2. Entity & Physics (`zaiyuans_voxel_entity`)](./DESIGN_VOXEL_ENTITY.md)
*   **Role**: The actors. Handles AABB physics, collision response, and basic AI.
*   **Key Feature**: **VoxelAABB**. A dedicated physics engine for voxel worlds that supports auto-stepping (stairs), sliding, and fluid drag, independent of Godot's Jolt/PhysX for maximum performance and control.
*   **AI**: Behavior Tree support with A* pathfinding on the voxel grid.

### [3. Atmosphere & Environment (`zaiyuans_voxel_atmosphere`)](./DESIGN_VOXEL_ATMOSPHERE.md)
*   **Role**: The aesthetics. Handles sky, weather, and fluids.
*   **Key Feature**: **Volumetric Clouds & Fluid Sim**.
    *   Clouds: Raymarched noise-based clouds.
    *   Fluids: Cellular automata simulation for water/lava flow.

### [4. Gameplay & Interaction (`zaiyuans_voxel_gameplay`)](./DESIGN_VOXEL_GAMEPLAY.md)
*   **Role**: The rules. Handles player interaction, inventory, and structures.
*   **Key Feature**: **VoxelRaycast & Structure Blocks**.
    *   Fast DDA raycasting for block selection.
    *   Copy/Paste functionality for saving buildings as `.structure` files.

---

## 2. Development Philosophy

1.  **Combination over Inheritance**: Use Godot Nodes and Resources.
2.  **Performance First**: Heavy lifting (meshing, physics, pathfinding) in C#.
3.  **Accessibility**: Logical control (generation rules, game rules) in GDScript.
4.  **Zero Boilerplate**: Drag `VoxelTerrain` -> Play. Drag `VoxelPlayer` -> Move.

---

## 3. Roadmap

See [MINECRAFT_MAKER_PLAN.md](./MINECRAFT_MAKER_PLAN.md) for roadmap and [VOXEL_TERRAIN_PLAN.md](./VOXEL_TERRAIN_PLAN.md) for terrain implementation phases.
