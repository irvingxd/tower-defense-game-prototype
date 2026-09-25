using Godot;

namespace TowerDefense.Dev;

// Dev-only: lays out every tile-* model in a labelled grid, top-down, for checking orientations.
public partial class TilePreview : Node3D
{
	public override void _Ready()
	{
		var dir = DirAccess.Open("res://assets/td");
		int i = 0;
		foreach (var file in dir.GetFiles())
		{
			if (!file.StartsWith("tile") || !file.EndsWith(".glb")) continue;
			var scene = GD.Load<PackedScene>("res://assets/td/" + file);
			var inst = scene.Instantiate<Node3D>();
			var pos = new Vector3((i % 8) * 1.6f, 0, (i / 8) * 1.6f);
			inst.Position = pos;
			AddChild(inst);
			var label = new Label3D { Text = file.Replace(".glb", "").Replace("tile-", ""), FontSize = 24, PixelSize = 0.004f,
				Position = pos + new Vector3(0, 0.5f, 0.65f), RotationDegrees = new Vector3(-90, 0, 0), Modulate = Colors.Black, OutlineSize = 0 };
			AddChild(label);
			// arrow marking +X (red) and +Z (blue)
			AddChild(Marker(pos + new Vector3(0.6f, 0.3f, -0.6f), new Vector3(0.25f, 0.05f, 0.05f), Colors.Red, 0.12f, 0));
			AddChild(Marker(pos + new Vector3(0.6f, 0.3f, -0.6f), new Vector3(0.05f, 0.05f, 0.25f), Colors.Blue, 0, 0.12f));
			i++;
		}
		var cam = new Camera3D { Projection = Camera3D.ProjectionType.Orthogonal, Size = 13f,
			Position = new Vector3(5.6f, 20, 4.8f), RotationDegrees = new Vector3(-90, 0, 0) };
		AddChild(cam);
		AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-60, 30, 0) });
		var env = new WorldEnvironment { Environment = new Godot.Environment { BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = new Color(0.9f, 0.9f, 0.9f), AmbientLightSource = Godot.Environment.AmbientSource.Color, AmbientLightColor = Colors.White, AmbientLightEnergy = 0.6f } };
		AddChild(env);
		AutoShot.Attach(this);
	}

	static MeshInstance3D Marker(Vector3 p, Vector3 size, Color c, float dx, float dz) => new()
	{
		Mesh = new BoxMesh { Size = size }, Position = p + new Vector3(dx, 0, dz),
		MaterialOverride = new StandardMaterial3D { AlbedoColor = c, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded }
	};
}
