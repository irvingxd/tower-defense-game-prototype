using System.Linq;
using Godot;
using TowerDefense.Data;
using TowerDefense.Game;

namespace TowerDefense.Dev;

// `-- --bakeicons`: renders a transparent 160px portrait of every tower (level 1 and 10) and every creep
// into res://assets/ui/portraits/, then quits. Re-run whenever models or the roster change.
public partial class IconBaker : Node
{
	const int Size = 160;
	const string OutDir = "res://assets/ui/portraits";

	public static bool Requested => System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--bakeicons") >= 0;

	SubViewport _vp;
	Node3D _stage;
	Camera3D _cam;

	public override async void _Ready()
	{
		_vp = new SubViewport
		{
			Size = new Vector2I(Size, Size), TransparentBg = true, OwnWorld3D = true,
			RenderTargetUpdateMode = SubViewport.UpdateMode.Always, Msaa3D = Viewport.Msaa.Msaa4X,
		};
		AddChild(_vp);
		_vp.AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-45, -30, 0), LightEnergy = 1.2f });
		_vp.AddChild(new WorldEnvironment
		{
			Environment = new Godot.Environment
			{
				BackgroundMode = Godot.Environment.BGMode.ClearColor,
				AmbientLightSource = Godot.Environment.AmbientSource.Color,
				AmbientLightColor = Colors.White, AmbientLightEnergy = 0.7f,
			},
		});
		_cam = new Camera3D { Projection = Camera3D.ProjectionType.Orthogonal };
		_vp.AddChild(_cam);
		_stage = new Node3D();
		_vp.AddChild(_stage);
		DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(OutDir));

		foreach (var def in Catalog.Towers)
		{
			foreach (int level in new[] { 1, 10 })
			{
				var tower = new Tower { Def = def, Lane = null, Cell = Vector2I.Zero, Preview = true, PreviewLevel = level };
				await Shoot(tower, $"tower-{def.Id}-{level}", towerShot: true);
			}
		}
		foreach (var unit in Catalog.Units)
		{
			var model = Models.Monster(unit.Model);
			var box = Models.Measure(model);
			model.Position = -box.Position - new Vector3(box.Size.X / 2, 0, box.Size.Z / 2);
			var holder = new Node3D();
			holder.AddChild(model);
			var anim = Models.FindOfType<AnimationPlayer>(model);
			var idle = anim == null ? null : new[] { "Idle", "Flying_Idle", "idle" }.FirstOrDefault(n => anim.HasAnimation(n));
			if (idle != null) anim.Play(idle);
			await Shoot(holder, $"unit-{unit.Id}", towerShot: false);
		}
		GD.Print("icons baked");
		GetTree().Quit();
	}

	async System.Threading.Tasks.Task Shoot(Node3D subject, string name, bool towerShot)
	{
		foreach (var c in _stage.GetChildren()) c.QueueFree();
		_stage.AddChild(subject);
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

		var box = Models.Measure(subject);
		var center = box.GetCenter();
		float extent = Mathf.Max(box.Size.Y, Mathf.Max(box.Size.X, box.Size.Z));
		_cam.Size = extent * (towerShot ? 1.15f : 1.25f);
		// Three-quarter view from the front-right, like the in-game camera but closer.
		var dir = new Vector3(0.55f, 0.55f, 1f).Normalized();
		_cam.Position = center + dir * (extent * 4 + 5);
		_cam.LookAt(center, Vector3.Up);

		for (int i = 0; i < 4; i++) await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		_vp.GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath($"{OutDir}/{name}.png"));
	}
}
