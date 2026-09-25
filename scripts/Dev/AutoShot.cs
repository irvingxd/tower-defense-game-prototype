using Godot;

namespace TowerDefense.Dev;

// `-- --autoshot <dir> [frames]` saves a screenshot after N frames and quits. Used for headless-ish verification.
public partial class AutoShot : Node
{
	string _dir;
	int _frames, _count, _shot;

	public static void Attach(Node parent)
	{
		var args = OS.GetCmdlineUserArgs();
		for (int i = 0; i < args.Length; i++)
		{
			if (args[i] != "--autoshot" || i + 1 >= args.Length) continue;
			var shot = new AutoShot { _dir = args[i + 1], _frames = i + 2 < args.Length && int.TryParse(args[i + 2], out var f) ? f : 60 };
			parent.AddChild(shot);
		}
	}

	public override void _Process(double delta)
	{
		if (++_count < _frames) return;
		DirAccess.MakeDirRecursiveAbsolute(_dir);
		GetViewport().GetTexture().GetImage().SavePng($"{_dir}/shot{_shot++}.png");
		GetTree().Quit();
	}
}
