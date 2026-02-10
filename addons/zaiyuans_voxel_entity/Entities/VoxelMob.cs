using Godot;
using System;
using System.Collections.Generic;
using ZaiyuansVoxelEntity.Base;
using ZaiyuansVoxelEntity.AI;

namespace ZaiyuansVoxelEntity.Entities
{
    public enum MobState
    {
        Idle,
        Wander,
        Chase
    }

    public partial class VoxelMob : VoxelCharacterBody
    {
        [Export] public float PathUpdateInterval { get; set; } = 1.0f;
        [Export] public float NodeReachedDistance { get; set; } = 0.5f;

        // AI Params
        [Export] public float DetectionRange { get; set; } = 15.0f;
        [Export] public float WanderRange { get; set; } = 10.0f;
        [Export] public float IdleTimeMin { get; set; } = 2.0f;
        [Export] public float IdleTimeMax { get; set; } = 5.0f;

        private VoxelPathfinder _pathfinder;
        private List<Vector3I> _currentPath = new List<Vector3I>();
        private int _pathIndex = 0;
        private double _timeSincePathUpdate = 0;
        private Node3D _targetNode;

        // FSM State
        private MobState _currentState = MobState.Idle;
        private double _stateTimer = 0;
        private Vector3 _wanderTarget;

        public void SetTarget(Node3D target)
        {
            _targetNode = target;
        }

        public override void _Ready()
        {
            base._Ready();
            EnterState(MobState.Idle);
        }

        public override void _PhysicsProcess(double delta)
        {
            if (_pathfinder == null)
            {
                var query = GetVoxelQuery();
                if (query != null)
                    _pathfinder = new VoxelPathfinder(query);
            }

            // Update AI Logic
            UpdateAI(delta);

            // Path Following & Movement
            UpdateMovement(delta);
            
            // Execute Base Physics
            base._PhysicsProcess(delta);
        }

        private void EnterState(MobState newState)
        {
            _currentState = newState;
            _stateTimer = 0;
            MoveDirection = Vector3.Zero;
            _currentPath.Clear(); // Clear path on state change

            switch (newState)
            {
                case MobState.Idle:
                    _stateTimer = GD.RandRange(IdleTimeMin, IdleTimeMax);
                    break;
                case MobState.Wander:
                    PickWanderTarget();
                    break;
                case MobState.Chase:
                    _timeSincePathUpdate = PathUpdateInterval + 1.0f; // Force immediate update
                    break;
            }
        }

        private void UpdateAI(double delta)
        {
            if (_targetNode != null)
            {
                float distToTarget = GlobalPosition.DistanceTo(_targetNode.GlobalPosition);
                if (distToTarget < DetectionRange)
                {
                    if (_currentState != MobState.Chase) EnterState(MobState.Chase);
                }
                else if (_currentState == MobState.Chase)
                {
                    // Target out of range, go back to Idle
                    EnterState(MobState.Idle);
                }
            }

            switch (_currentState)
            {
                case MobState.Idle:
                    _stateTimer -= delta;
                    if (_stateTimer <= 0)
                    {
                        EnterState(MobState.Wander);
                    }
                    break;
                case MobState.Wander:
                    // Check if stuck or reached goal
                    if (_currentPath == null || _pathIndex >= _currentPath.Count)
                    {
                        // Path finished or invalid
                        EnterState(MobState.Idle);
                    }
                    break;
                case MobState.Chase:
                    // Logic handled in UpdateMovement pathing
                    break;
            }
        }

        private void PickWanderTarget()
        {
            if (_pathfinder == null) 
            {
                EnterState(MobState.Idle);
                return;
            }
            
            // Random point in circle
            float angle = (float)GD.RandRange(0, Mathf.Tau);
            float dist = (float)GD.RandRange(2, WanderRange);
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * dist;
            Vector3 potentialTarget = GlobalPosition + offset;
            
            Vector3I start = new Vector3I(Mathf.FloorToInt(GlobalPosition.X), Mathf.FloorToInt(GlobalPosition.Y), Mathf.FloorToInt(GlobalPosition.Z));
            Vector3I end = new Vector3I(Mathf.FloorToInt(potentialTarget.X), Mathf.FloorToInt(potentialTarget.Y), Mathf.FloorToInt(potentialTarget.Z));
            
            // Find path
            _currentPath = _pathfinder.FindPath(start, end);
            _pathIndex = 0;
            
            if (_currentPath.Count == 0)
            {
                // Failed to find path, go back to idle
                EnterState(MobState.Idle);
            }
        }

        private void UpdateMovement(double delta)
        {
            if (_pathfinder == null) return;
            
            // Path Re-planning for Chase
            if (_currentState == MobState.Chase && _targetNode != null)
            {
                _timeSincePathUpdate += delta;
                if (_timeSincePathUpdate > PathUpdateInterval)
                {
                    Vector3I start = new Vector3I(Mathf.FloorToInt(GlobalPosition.X), Mathf.FloorToInt(GlobalPosition.Y), Mathf.FloorToInt(GlobalPosition.Z));
                    Vector3I end = new Vector3I(Mathf.FloorToInt(_targetNode.GlobalPosition.X), Mathf.FloorToInt(_targetNode.GlobalPosition.Y), Mathf.FloorToInt(_targetNode.GlobalPosition.Z));
                    
                    var newPath = _pathfinder.FindPath(start, end);
                    if (newPath.Count > 0)
                    {
                        _currentPath = newPath;
                        _pathIndex = 0;
                    }
                    _timeSincePathUpdate = 0;
                }
            }

            // Follow Path
            if (_currentPath != null && _pathIndex < _currentPath.Count)
            {
                Vector3I targetNode = _currentPath[_pathIndex];
                Vector3 targetPos = new Vector3(targetNode.X + 0.5f, targetNode.Y, targetNode.Z + 0.5f);

                Vector3 diff = targetPos - GlobalPosition;
                Vector3 horizontalDiff = new Vector3(diff.X, 0, diff.Z);
                float dist = horizontalDiff.Length();

                if (dist < NodeReachedDistance)
                {
                    _pathIndex++;
                    // Check next node for jump
                    if (_pathIndex < _currentPath.Count)
                    {
                        Vector3I nextNode = _currentPath[_pathIndex];
                        if (nextNode.Y > targetNode.Y && IsOnFloor())
                        {
                            JumpRequested = true;
                        }
                    }
                }
                else
                {
                    MoveDirection = horizontalDiff.Normalized();

                    // Face movement
                    if (MoveDirection.LengthSquared() > 0.01f)
                    {
                        float targetAngle = Mathf.Atan2(MoveDirection.X, MoveDirection.Z);
                        Vector3 rot = Rotation;
                        rot.Y = Mathf.LerpAngle(rot.Y, targetAngle, (float)delta * 10f);
                        Rotation = rot;
                    }
                }
            }
            else
            {
                MoveDirection = Vector3.Zero;
            }
        }
    }
}
