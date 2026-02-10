using Godot;
using System;
using ZaiyuansVoxelEntity.Physics;

namespace ZaiyuansVoxelEntity.Base
{
	/// <summary>
	/// Base class for all Voxel-based characters (Player, Mobs).
	/// Handles integration with VoxelPhysicsEngine.
	/// </summary>
	public abstract partial class VoxelCharacterBody : CharacterBody3D
	{
		[Export] public float Speed { get; set; } = 5.0f;
		[Export] public float JumpVelocity { get; set; } = 4.5f;
		[Export] public float Gravity { get; set; } = 9.8f;
		
		// Configuration for physics
		[Export] public Vector3 AabbSize { get; set; } = new Vector3(0.6f, 1.8f, 0.6f);
		[Export] public Vector3 AabbOffset { get; set; } = new Vector3(-0.3f, 0, -0.3f);

		protected VoxelPhysicsEngine PhysicsEngine;
		protected Aabb BodyAabb;
		
		// Input state (to be set by subclasses)
		protected Vector3 MoveDirection; // Normalized direction of movement (X, Z)
		protected bool JumpRequested;
		
		protected bool IsGroundedInternal;
		public new bool IsOnFloor() => IsGroundedInternal;

		public override void _Ready()
		{
			PhysicsEngine = new VoxelPhysicsEngine();
			BodyAabb = new Aabb(AabbOffset, AabbSize);
		}

		public override void _PhysicsProcess(double delta)
		{
			float dt = (float)delta;
			Vector3 velocity = Velocity;

			// Apply Gravity
			if (!IsGroundedInternal)
			{
				velocity.Y -= Gravity * dt;
			}

			// Handle Jump logic
			if (JumpRequested && IsGroundedInternal)
			{
				velocity.Y = JumpVelocity;
				JumpRequested = false; // Consume jump request
			}

			// Apply Movement
			if (MoveDirection != Vector3.Zero)
			{
				// Smooth acceleration? For now direct velocity set akin to Quake/Source "frictionless" air/ground control for simplicity
				velocity.X = MoveDirection.X * Speed;
				velocity.Z = MoveDirection.Z * Speed;
			}
			else
			{
				velocity.X = Mathf.MoveToward(velocity.X, 0, Speed * dt * 5.0f); // Fast stop
				velocity.Z = Mathf.MoveToward(velocity.Z, 0, Speed * dt * 5.0f);
			}

			// Physics Step
			var result = PhysicsEngine.Move(GlobalPosition, velocity, BodyAabb, dt);
			
			GlobalPosition = result.Position;
			Velocity = result.Velocity;
			IsGroundedInternal = result.IsGrounded;
			
			// Allow subclasses to do extra logic (e.g. rotation)
			PostPhysicsProcess(dt);
		}

		protected virtual void PostPhysicsProcess(float delta) { }
	}
}
