using Godot;
using System;
using System.Collections.Generic;
using ZaiyuansVoxelWorld;
using ZaiyuansVoxelWorld.Core;

namespace ZaiyuansVoxelEntity.AI
{
	public class VoxelPathfinder
	{
		private readonly IVoxelQuery _query;

		public VoxelPathfinder(IVoxelQuery query)
		{
			_query = query;
		}

		public List<Vector3I> FindPath(Vector3I start, Vector3I end, int maxIterations = 5000)
		{
			if (_query == null) return new List<Vector3I>();

			var openSet = new PriorityQueue<Vector3I, float>();
			openSet.Enqueue(start, 0);

			var cameFrom = new Dictionary<Vector3I, Vector3I>();
			var gScore = new Dictionary<Vector3I, float>();
			gScore[start] = 0;

			// Tie-breaking heuristic scaling
			float hScale = 1.001f;

			int iterations = 0;
			while (openSet.Count > 0)
			{
				if (iterations++ > maxIterations) break;

				var current = openSet.Dequeue();

				if (current == end)
				{
					return ReconstructPath(cameFrom, current);
				}

				foreach (var neighbor in GetNeighbors(current))
				{
					float dist = (1.0f); // neighbor distance is approx 1
					// Improve: Diagonal steps cost sqrt(2). But we only do cardinal.
					// Vertical steps cost more?
					if (neighbor.Y != current.Y) dist = 1.5f;

					float tentativeG = gScore[current] + dist;

					if (!gScore.ContainsKey(neighbor) || tentativeG < gScore[neighbor])
					{
						cameFrom[neighbor] = current;
						gScore[neighbor] = tentativeG;
						float fScore = tentativeG + Heuristic(neighbor, end) * hScale;
						openSet.Enqueue(neighbor, fScore);
					}
				}
			}

			return new List<Vector3I>(); // No path found
		}

		private float Heuristic(Vector3I a, Vector3I b)
		{
			return Mathf.Abs(a.X - b.X) + Mathf.Abs(a.Y - b.Y) + Mathf.Abs(a.Z - b.Z);
		}

		private List<Vector3I> ReconstructPath(Dictionary<Vector3I, Vector3I> cameFrom, Vector3I current)
		{
			var totalPath = new List<Vector3I> { current };
			while (cameFrom.ContainsKey(current))
			{
				current = cameFrom[current];
				totalPath.Insert(0, current);
			}
			return totalPath;
		}

		private IEnumerable<Vector3I> GetNeighbors(Vector3I pos)
		{
			// Cardinal directions
			var dirs = new Vector3I[]
			{
				new Vector3I(1, 0, 0),
				new Vector3I(-1, 0, 0),
				new Vector3I(0, 0, 1),
				new Vector3I(0, 0, -1)
			};

			foreach (var d in dirs)
			{
				// check level
				var target = pos + d;
				
				// 1. Walk Flat
				if (IsStandingSpace(target) && IsSolid(target + Vector3I.Down))
				{
					yield return target;
					continue;
				}

				// 2. Step Down (Drop 1 to 3 blocks)
				if (IsStandingSpace(target) && !IsSolid(target + Vector3I.Down))
				{
					// Fall down check
					bool landFound = false;
					for (int i = 1; i <= 3; i++)
					{
						var below = target + new Vector3I(0, -i, 0);
						if (IsSolid(below)) // hit something?
						{
							// we can stand on 'below'? 
							// Wait, 'below' is the block we hit. 
							// So we stand at 'below + Up'.
							var landPos = below + Vector3I.Up;
							// Check clearance at landPos
							if (IsStandingSpace(landPos))
							{
								yield return landPos;
								landFound = true;
							}
							break; // Hit ground
						}
					}
					if (landFound) continue;
				}

				// 3. Step Up
				// To step up, 'target' (pos+dir) must be solid (the step).
				// And 'target + Up' (pos+dir+Up) must be air.
				// And 'target + Up + Up' (pos+dir+Up+Up) must be air.
				// Also 'pos + Up + Up' is traversed? Yes, head moves.
				if (IsSolid(target) && IsStandingSpace(target + Vector3I.Up))
				{
					// Also check head clearance above current pos to jump up?
					if (!IsSolid(pos + new Vector3I(0, 2, 0)))
					{
						yield return target + Vector3I.Up;
					}
				}
			}
		}

		private bool IsStandingSpace(Vector3I pos)
		{
			// Needs two blocks of air: pos and pos+up
			return !IsSolid(pos) && !IsSolid(pos + Vector3I.Up);
		}

		private bool IsSolid(Vector3I pos)
		{
			return _query.GetBlock(pos) != BlockId.Air;
		}
	}
}
