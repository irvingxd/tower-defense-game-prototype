using Godot;

namespace TowerDefense.Game;

// Fixed-pitch orthographic camera.
// Right- or middle-drag pans (the ground follows the cursor), wheel zooms toward the cursor,
// WASD/arrows pan, optional screen-edge scrolling. Focus is clamped to the play area.
public partial class CameraRig : Camera3D
{
	const float Pitch = 45f, Distance = 40f, PanSpeed = 10f, EdgeMargin = 10f;
	const float DragThreshold = 6f; // pixels; below this a right-click is a click, not a drag

	public Vector3 Focus;
	public float MinSize = 6f, MaxSize = 24f;
	public Rect2 Bounds = new(-2, -2, 26, 18); // x/z extents the focus may move within
	public static bool EdgeScroll;

	// True while the current right/middle press has moved far enough to count as a drag.
	public bool Dragging { get; private set; }

	bool _pressed;
	Vector2 _pressPos;

	public override void _Ready()
	{
		Projection = ProjectionType.Orthogonal;
		Size = 15f;
		Far = 200f;
		Apply();
	}

	Vector3 Offset()
	{
		float p = Mathf.DegToRad(Pitch);
		return new Vector3(0, Mathf.Sin(p), Mathf.Cos(p)) * Distance;
	}

	void Apply()
	{
		Focus = new Vector3(
			Mathf.Clamp(Focus.X, Bounds.Position.X, Bounds.End.X), 0,
			Mathf.Clamp(Focus.Z, Bounds.Position.Y, Bounds.End.Y));
		Position = Focus + Offset();
		RotationDegrees = new Vector3(-Pitch, 0, 0);
	}

	// World units per screen pixel, and how screen-vertical maps onto the tilted ground.
	float UnitsPerPixel => Size / GetViewport().GetVisibleRect().Size.Y;
	static float GroundStretch => 1f / Mathf.Sin(Mathf.DegToRad(Pitch));

	public override void _Process(double delta)
	{
		if (Position != Focus + Offset()) Apply(); // Focus set from outside

		var move = Input.GetVector("ui_left", "ui_right", "ui_up", "ui_down");
		if (Input.IsKeyPressed(Key.A)) move.X -= 1;
		if (Input.IsKeyPressed(Key.D)) move.X += 1;
		if (Input.IsKeyPressed(Key.W)) move.Y -= 1;
		if (Input.IsKeyPressed(Key.S)) move.Y += 1;
		if (EdgeScroll && !_pressed && DisplayServer.WindowIsFocused())
		{
			var m = GetViewport().GetMousePosition();
			var size = GetViewport().GetVisibleRect().Size;
			if (m.X < EdgeMargin) move.X -= 1;
			if (m.X > size.X - EdgeMargin) move.X += 1;
			if (m.Y < EdgeMargin) move.Y -= 1;
			if (m.Y > size.Y - EdgeMargin) move.Y += 1;
		}
		if (move == Vector2.Zero) return;
		// Real time, so game speed doesn't change camera speed.
		float dt = (float)delta / (float)Mathf.Max(Engine.TimeScale, 0.01);
		Focus += new Vector3(move.X, 0, move.Y).LimitLength(1) * PanSpeed * dt * (Size / 15f);
		Apply();
	}

	public override void _UnhandledInput(InputEvent e)
	{
		switch (e)
		{
			case InputEventMouseButton { ButtonIndex: MouseButton.Right or MouseButton.Middle } mb:
				_pressed = mb.Pressed;
				if (mb.Pressed) { _pressPos = mb.Position; Dragging = false; }
				break;

			case InputEventMouseMotion mm when _pressed:
				if (!Dragging && mm.Position.DistanceTo(_pressPos) > DragThreshold) Dragging = true;
				if (!Dragging) break;
				Focus -= new Vector3(mm.Relative.X, 0, mm.Relative.Y * GroundStretch) * UnitsPerPixel;
				Apply();
				GetViewport().SetInputAsHandled();
				break;

			case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelUp or MouseButton.WheelDown } wheel:
				ZoomAt(wheel.Position, wheel.ButtonIndex == MouseButton.WheelUp ? 0.9f : 1.1f);
				break;
		}
	}

	// Zoom so the ground point under the cursor stays under the cursor.
	void ZoomAt(Vector2 screen, float factor)
	{
		var before = GroundAt(screen);
		Size = Mathf.Clamp(Size * factor, MinSize, MaxSize);
		var after = GroundAt(screen);
		if (before is { } b && after is { } a) Focus += b - a;
		Apply();
	}

	Vector3? GroundAt(Vector2 screen)
	{
		var from = ProjectRayOrigin(screen);
		var dir = ProjectRayNormal(screen);
		if (Mathf.Abs(dir.Y) < 1e-4f) return null;
		float t = (Lane.Surface - from.Y) / dir.Y;
		return t < 0 ? null : from + dir * t;
	}
}
