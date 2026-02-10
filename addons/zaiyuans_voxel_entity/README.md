# Zaiyuan's Voxel Entity Plugin

## Overview
This plugin provides an entity system for the voxel world, including:
*   **VoxelPhysicsEngine**: Custom physics handling AABB vs Voxel World collision.
*   **VoxelCharacterBody**: Base class for voxel entities.
*   **VoxelPlayer**: A preset FPS/TPS player controller interaction with voxels.
*   **VoxelMob**: An AI base class with FSM (Idle/Wander/Chase) and A* pathfinding.

## Setup
1.  Enable the plugin in **Project Settings > Plugins**.
2.  Ensure `VoxelWorld` is present in the scene (autoload or singleton access).

## Components

### VoxelPlayer
A character controller ready to use.
1.  Add a `VoxelPlayer` node to your scene (via C# script or instantiation).
2.  Assign `Camera3D` if you want a custom camera setup, otherwise it creates one.
3.  Controls:
    *   **WASD / Arrow Keys**: Move
    *   **Space**: Jump
    *   **Mouse**: Look

### VoxelMob
An AI entity with built-in behavior.
1.  Inherit from `VoxelMob` or add a script inheriting from it.
2.  **States**:
    *   **Idle**: Waits for a random duration (Configurable `IdleTimeMin/Max`).
    *   **Wander**: Picks a random spot within `WanderRange` and walks there.
    *   **Chase**: If a `Target` (Node3D) is set via `SetTarget()` and is within `DetectionRange`, it will chase.

### VoxelPhysicsEngine
Can be used manually for custom entities.
```csharp
var engine = new VoxelPhysicsEngine();
var result = engine.Move(globalPos, velocity, aabb, delta);
GlobalPosition = result.Position;
Velocity = result.Velocity;
```
