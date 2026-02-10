using Godot;
using System;
using ZaiyuansVoxelEntity.Base;
using ZaiyuansVoxelEntity.Physics;

namespace ZaiyuansVoxelEntity.Entities
{
	public partial class VoxelPlayer : VoxelCharacterBody
	{
		[Export] public float MouseSensitivity { get; set; } = 0.003f;
		
		// Camera
		private Node3D _head;
		private Camera3D _camera;

		public override void _Ready()
		{
			base._Ready(); // Init physics engine AABB

			// Setup Camera
			if (GetNodeOrNull("Head") is Node3D head)
			{
				_head = head;
				_camera = _head.GetNodeOrNull<Camera3D>("Camera3D");
			}
			if (_head == null)
			{
				_head = new Node3D { Name = "Head", Position = new Vector3(0, 1.6f, 0) };
				AddChild(_head);
				_camera = new Camera3D { Name = "Camera3D" };
				_head.AddChild(_camera);
			}
			
			Input.MouseMode = Input.MouseModeEnum.Captured;
		}

		public override void _PhysicsProcess(double delta)
		{
			float dt = (float)delta;

			// Handle Jump
			if (Input.IsActionJustPressed("ui_accept"))
			{
				JumpRequested = true;
			}

			// Get Input (W/S/A/D: move_forward / move_backward / move_left / move_right)
			Vector2 inputDir = Input.GetVector("move_left", "move_right", "move_forward", "move_backward");
			Vector3 direction = (Transform.Basis * new Vector3(inputDir.X, 0, inputDir.Y)).Normalized();
			
			MoveDirection = direction;

			base._PhysicsProcess(delta);
		}

		public override void _Input(InputEvent @event)
		{
			if (@event is InputEventMouseMotion mm)
			{
				RotateY(-mm.Relative.X * MouseSensitivity);
				if (_head != null)
				{
					_head.RotateX(-mm.Relative.Y * MouseSensitivity);
					_head.Rotation = new Vector3(
						Mathf.Clamp(_head.Rotation.X, Mathf.DegToRad(-90), Mathf.DegToRad(90)),
						_head.Rotation.Y,
						_head.Rotation.Z
					);
				}
			}
			
			if (Input.IsMouseButtonPressed(MouseButton.Left))
			{
				Input.MouseMode = Input.MouseModeEnum.Captured;
			}
			if (Input.IsKeyLabelPressed(Key.Escape))
			{
				Input.MouseMode = Input.MouseModeEnum.Visible;
			}
		}
	}
}
