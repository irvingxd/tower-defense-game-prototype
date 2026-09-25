using Godot;

namespace TowerDefense.Game;

// Flat bar that always faces the camera, independent of the creep's facing.
public partial class HealthBar : Node3D
{
	const float BarHeight = 0.06f;
	public float Width = 0.42f;
	public Vector3 Offset;

	MeshInstance3D _fill;

	public override void _Ready()
	{
		TopLevel = true;
		Visible = false;
		var bg = new MeshInstance3D { Mesh = new QuadMesh { Size = new Vector2(Width + 0.03f, BarHeight + 0.03f) }, MaterialOverride = Mat(new Color(0.1f, 0.1f, 0.1f), 0) };
		_fill = new MeshInstance3D { Mesh = new QuadMesh { Size = new Vector2(Width, BarHeight) }, MaterialOverride = Mat(new Color(0.35f, 0.9f, 0.3f), 1) };
		AddChild(bg);
		AddChild(_fill);
	}

	static StandardMaterial3D Mat(Color c, int priority)
	{
		var m = Models.Flat(c);
		m.NoDepthTest = true;
		m.RenderPriority = priority;
		return m;
	}

	public void Set(float frac)
	{
		frac = Mathf.Clamp(frac, 0, 1);
		Visible = frac < 1f;
		_fill.Scale = new Vector3(Mathf.Max(frac, 0.001f), 1, 1);
		_fill.Position = new Vector3(-(1 - frac) * Width / 2, 0, 0);
		((StandardMaterial3D)_fill.MaterialOverride).AlbedoColor =
			frac > 0.5f ? new Color(0.35f, 0.9f, 0.3f) : frac > 0.25f ? new Color(0.95f, 0.75f, 0.2f) : new Color(0.9f, 0.25f, 0.2f);
	}

	public override void _Process(double delta)
	{
		var cam = GetViewport().GetCamera3D();
		if (cam == null) return;
		GlobalTransform = new Transform3D(cam.GlobalBasis, GetParent<Node3D>().GlobalPosition + Offset);
	}
}
