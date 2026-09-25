using Godot;
using TowerDefense.Game;

namespace TowerDefense.Dev;

// Dev-only: every monster normalised to the same height, walking toward the camera, labelled.
public partial class MonsterPreview : Node3D
{
	public override void _Ready()
	{
		int i = 0;
		foreach (var file in DirAccess.Open("res://assets/monsters").GetFiles())
		{
			if (!file.EndsWith(".glb")) continue;
			var name = file.Replace(".glb", "");
			var pos = new Vector3((i % 9) * 1.3f, 0, (i / 9) * 1.5f);
			var model = Models.Monster(name);
			var box = Models.Measure(model);
			GD.Print($"{name}: raw box {box.Position} size {box.Size}");
			model.Scale = Vector3.One * (0.8f / Mathf.Max(box.Size.Y, 0.001f));
			model.Position = pos;
			AddChild(model);
			var anim = Models.FindOfType<AnimationPlayer>(model);
			var clip = Enemy.PickMoveAnimation(anim);
			if (clip != null) { anim.GetAnimation(clip).LoopMode = Animation.LoopModeEnum.Linear; anim.Play(clip); }
			AddChild(new Label3D { Text = name, FontSize = 28, PixelSize = 0.004f, Position = pos + new Vector3(0, 0.02f, 0.5f), RotationDegrees = new Vector3(-50, 0, 0), Modulate = Colors.Black, OutlineSize = 0 });
			i++;
		}
		var cam = new Camera3D { Projection = Camera3D.ProjectionType.Orthogonal, Size = 8.5f };
		AddChild(cam);
		cam.Position = new Vector3(5.2f, 12, 3.6f + 10);
		cam.LookAt(new Vector3(5.2f, 0, 3.6f));
		AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-55, -35, 0) });
		AddChild(new WorldEnvironment { Environment = new Godot.Environment { BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = new Color(0.85f, 0.9f, 0.85f), AmbientLightSource = Godot.Environment.AmbientSource.Color, AmbientLightColor = Colors.White, AmbientLightEnergy = 0.6f } });
		AutoShot.Attach(this);
	}
}
