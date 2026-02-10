using Godot;
using System;
using ZaiyuansVoxelWorld;
using ZaiyuansVoxelWorld.Core;

namespace ZaiyuansVoxelEntity.Physics
{
	public struct MoveResult
	{
		public Vector3 Position;
		public Vector3 Velocity;
		public bool IsGrounded;
		public bool CollidedX;
		public bool CollidedY;
		public bool CollidedZ;
	}

	public class VoxelPhysicsEngine
	{
		private IVoxelQuery _query;

		public float AutoJumpHeight { get; set; } = 1.1f;
		public bool EnableAutoJump { get; set; } = true;

		public VoxelPhysicsEngine(IVoxelQuery query = null)
		{
			_query = query ?? VoxelWorld.Singleton;
		}

		public MoveResult Move(Vector3 position, Vector3 velocity, Aabb box, float delta)
		{
			if (_query == null) _query = VoxelWorld.Singleton;
			// If query is still null, just move without collision
			if (_query == null) 
			{
				return new MoveResult { Position = position + velocity * delta, Velocity = velocity };
			}

			Vector3 finalPos = position;
			Vector3 motion = velocity * delta;
			
			bool collidedY = false;
			bool collidedX = false;
			bool collidedZ = false;
			bool grounded = false;

			// 1. Y Axis (Vertical)
			// Attempt to move Y
			float my = motion.Y;
			var resY = ResolveAxis(finalPos, new Vector3(0, my, 0), box);
			finalPos = resY.Position;
			collidedY = resY.Collided;

			if (collidedY)
			{
				velocity.Y = 0;
				if (my < 0) grounded = true;
			}

			// 2. X Axis (Horizontal)
			float mx = motion.X;
			var resX = ResolveAxis(finalPos, new Vector3(mx, 0, 0), box);
			
			if (resX.Collided && EnableAutoJump && grounded)
			{
				// Try Auto Jump
				// Check if we can step up
				// Steps: Move Up -> Move X -> Move Down (to snap) or just move X at height
				// Simplified: Check if target position + 1 block up is valid
				
				// Position after stepping up
				Vector3 stepUpPos = finalPos;
				stepUpPos.Y += 1.01f; // Step slightly more than 1 block to clear edge? Or just 1.0 + epsilon
				
				// Start from 'finalPos' (which is blocked in X).
				// Check if we can fit at 'finalPos + Up'
				if (!CheckCollision(new Aabb(stepUpPos + box.Position, box.Size)))
				{
					// Check if we can move X at this new height
					var stepResX = ResolveAxis(stepUpPos, new Vector3(mx, 0, 0), box);
					if (!stepResX.Collided)
					{
						// Successful step!
						finalPos = stepResX.Position;
						collidedX = false; 
						 // Note: We don't snap down here because gravity will handle it in next frame/iteration
						 // But usually auto-jump feels snappy.
					}
					else
					{
						finalPos = resX.Position;
						collidedX = true;
						velocity.X = 0;
					}
				}
				else
				{
					finalPos = resX.Position;
					collidedX = true;
					velocity.X = 0;
				}
			}
			else
			{
				finalPos = resX.Position;
				collidedX = resX.Collided;
				if (collidedX) velocity.X = 0;
			}

			// 3. Z Axis (Horizontal)
			float mz = motion.Z;
			var resZ = ResolveAxis(finalPos, new Vector3(0, 0, mz), box);
			
			if (resZ.Collided && EnableAutoJump && grounded)
			{
				// Try Auto Jump Z
				Vector3 stepUpPos = finalPos;
				stepUpPos.Y += 1.01f;
				
				if (!CheckCollision(new Aabb(stepUpPos + box.Position, box.Size)))
				{
					var stepResZ = ResolveAxis(stepUpPos, new Vector3(0, 0, mz), box);
					if (!stepResZ.Collided)
					{
						finalPos = stepResZ.Position;
						collidedZ = false;
					}
					else
					{
						finalPos = resZ.Position;
						collidedZ = true;
						velocity.Z = 0;
					}
				}
				 else
				{
					finalPos = resZ.Position;
					collidedZ = true;
					velocity.Z = 0;
				}
			}
			else
			{
				finalPos = resZ.Position;
				collidedZ = resZ.Collided;
				if (collidedZ) velocity.Z = 0;
			}

			return new MoveResult
			{
				Position = finalPos,
				Velocity = velocity,
				IsGrounded = grounded,
				CollidedX = collidedX,
				CollidedY = collidedY,
				CollidedZ = collidedZ
			};
		}

		private struct AxisResult { public Vector3 Position; public bool Collided; }

		private AxisResult ResolveAxis(Vector3 pos, Vector3 motion, Aabb box)
		{
			if (motion.LengthSquared() < 1e-6) return new AxisResult { Position = pos, Collided = false };

			// Iterative stepping for accuracy
			// For a single frame motion, typically small (< 0.5 units).
			// A simple trace is: Try full move. If collide, binary search or step back?
			// Voxel approach: Step by fractional block size.
			
			float dist = motion.Length();
			Vector3 dir = motion.Normalized();
			float step = 0.2f; // Check every 0.2 units
			int steps = Mathf.CeilToInt(dist / step);

			// We want to move 'motion'.
			// Check incrementally.
			for (int i = 1; i <= steps; i++)
			{
				float d = Mathf.Min(i * step, dist);
				Vector3 testPos = pos + dir * d;
				if (CheckCollision(new Aabb(testPos + box.Position, box.Size)))
				{
					// Collision at 'testPos'. 
					// To be precise, we should binary search between (i-1) and i.
					// Or just return (i-1) position (previous valid).
					float validD = Mathf.Max(0, (i - 1) * step);
					
					// Let's refine validD with a small binary search (3 iterations) to get close to wall
					float minD = validD;
					float maxD = d;
					for(int j=0; j<3; j++)
					{
						float mid = (minD + maxD) * 0.5f;
						if(CheckCollision(new Aabb(pos + dir * mid + box.Position, box.Size)))
						{
							maxD = mid;
						}
						else
						{
							minD = mid;
						}
					}
					
					return new AxisResult { Position = pos + dir * minD, Collided = true };
				}
			}
			
			return new AxisResult { Position = pos + motion, Collided = false };
		}

		public bool CheckCollision(Aabb globalBox)
		{
			if (_query == null) return false;
			
			// Expand slightly to avoid floating point misses?
			// Usually shrinking box by epsilon is better to avoid snagging on exact edges,
			// but here we want robust collision. 
			// `Mathf.FloorToInt` handles edge inclusion logic. 
			// If Position is 1.0, Floor is 1. We include block 1.
			// If End is 1.9, Floor is 1. We include block 1.
			// If End is 2.0, Floor is 2. We include block 2? No, End is exclusive conceptually for AABB?
			// AABB.CheckCollision vs Voxel grid.
			
			// To be safe: shrink AABB by a tiny epsilon for the check loops
			Vector3 startObj = globalBox.Position + new Vector3(0.001f, 0.001f, 0.001f);
			Vector3 endObj = globalBox.End - new Vector3(0.001f, 0.001f, 0.001f);

			int minX = Mathf.FloorToInt(startObj.X);
			int minY = Mathf.FloorToInt(startObj.Y);
			int minZ = Mathf.FloorToInt(startObj.Z);
			
			int maxX = Mathf.FloorToInt(endObj.X);
			int maxY = Mathf.FloorToInt(endObj.Y);
			int maxZ = Mathf.FloorToInt(endObj.Z);

			for (int x = minX; x <= maxX; x++)
			{
				for (int y = minY; y <= maxY; y++)
				{
					for (int z = minZ; z <= maxZ; z++)
					{
						if (_query.GetBlock(new Vector3I(x, y, z)) != BlockId.Air)
						{
							return true;
						}
					}
				}
			}
			return false;
		}
	}
}
