using System;
using System.Linq;
using System.Text;
using Godot;
using TowerDefense.Dev;
using TowerDefense.Game;
using TowerDefense.UI;

namespace TowerDefense;

// Scene root: builds the world, the match, the controllers and the HUD.
// Args (after `--`): --autotest <dir> runs AI vs AI fast and writes report.txt + screenshots.
public partial class Main : Node3D
{
	string _autotestDir;
	readonly StringBuilder _report = new();

	public override void _Ready()
	{
		System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
		var args = OS.GetCmdlineUserArgs();
		int at = Array.IndexOf(args, "--autotest");
		if (at >= 0 && at + 1 < args.Length) _autotestDir = args[at + 1];

		if (SheetRenderer.TryAttach(this)) return;

		if (IconBaker.Requested)
		{
			AddChild(new IconBaker());
			return;
		}

		BuildWorld();

		var match = new Match { Name = "Match" };
		AddChild(match);

		var cam = new CameraRig { Focus = new Vector3((Match.LaneSpacing + Lane.W - 1) / 2f, 0, (Lane.H - 1) / 2f + 1.2f) };
		AddChild(cam);

		if (Bench.TryAttach(this, match))
		{
			AddChild(new Hud { Match = match, Input = null, Player = 0 });
			return;
		}

		var rng = new RandomNumberGenerator();
		rng.Randomize();
		var aiStyle = ArgStyle(args, "--ai1") ?? (AiController.Personality)(rng.Randi() % 4);
		match.Players[1].Name = $"AI ({aiStyle})";
		AddChild(new AiController { Match = match, Player = 1, Style = aiStyle });

		InputController input = null;
		if (_autotestDir == null)
		{
			input = new InputController { Match = match, Player = 0 };
			AddChild(input);
		}
		else
		{
			var style0 = ArgStyle(args, "--ai0") ?? AiController.Personality.Balanced;
			match.Players[0].Name = $"AI ({style0})";
			AddChild(new AiController { Match = match, Player = 0, Style = style0 });
			Engine.TimeScale = 10;
			FloatingText.Enabled = false;
			GetViewport().GuiDisableInput = true; // clicks on the test window must not act for player 0
			match.WaveStarting += () => OnAutotestWave(match);
			match.WaveEnded += w => LogWave(match, w);
			match.Leaked += (lane, e) => _report.AppendLine($"    leak lane {lane} wave {match.Wave}: {e.Def.Id} hp {e.Hp:0}/{e.MaxHp:0} ({e.Hp / e.MaxHp:P0})");
			match.MatchOver += () => FinishAutotest(match);
		}

		AddChild(new Hud { Match = match, Input = input, Player = 0 });
		InputProbe.Attach(this, match);
		UiProbe.Attach(this, match);
		AutoShot.Attach(this);
	}

	// `--ai0 Hoarder` / `--ai1 Greedy` pick personalities for autotests.
	static AiController.Personality? ArgStyle(string[] args, string flag)
	{
		int i = Array.IndexOf(args, flag);
		return i >= 0 && i + 1 < args.Length && Enum.TryParse<AiController.Personality>(args[i + 1], true, out var p) ? p : null;
	}

	void BuildWorld()
	{
		var env = new Godot.Environment
		{
			BackgroundMode = Godot.Environment.BGMode.Color,
			BackgroundColor = new Color(0.52f, 0.72f, 0.86f),
			AmbientLightSource = Godot.Environment.AmbientSource.Color,
			AmbientLightColor = new Color(0.85f, 0.9f, 1f),
			AmbientLightEnergy = 0.55f,
			TonemapMode = Godot.Environment.ToneMapper.Filmic,
			SsaoEnabled = true,
		};
		AddChild(new WorldEnvironment { Environment = env });
		AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-55, -35, 0), ShadowEnabled = true, LightEnergy = 1.1f });

		// Ground under and around both lanes, a shade darker than the grass tiles.
		var ground = new MeshInstance3D
		{
			Mesh = new PlaneMesh { Size = new Vector2(80, 60) },
			MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.33f, 0.62f, 0.42f) },
			Position = new Vector3(Match.LaneSpacing, -0.01f, Lane.H / 2f),
		};
		AddChild(ground);

		// River between the lanes.
		float riverX = Lane.W - 1 + (Match.LaneSpacing - Lane.W + 1) / 2f;
		for (int r = -2; r < Lane.H + 2; r++)
		{
			var tile = Models.Td("tile-river-straight");
			tile.Position = new Vector3(riverX, 0, r);
			AddChild(tile);
		}
	}

	async void OnAutotestWave(Match match)
	{
		if (match.Wave is not (1 or 5 or 17 or 30 or 45)) return;
		await ToSignal(GetTree().CreateTimer(14.0), SceneTreeTimer.SignalName.Timeout);
		Shot($"wave{match.Wave}");
	}

	void LogWave(Match match, int wave)
	{
		var line = $"wave {wave,2}: ";
		for (int i = 0; i < 2; i++)
		{
			var p = match.Players[i];
			var lane = match.Lanes[i];
			line += $"| {p.Name,-15} lives {p.Lives,2} gold {p.Gold,5} income {match.Income(p),4} towers {lane.Towers.Count,2} levels {lane.Towers.Values.Sum(t => t.Level),3} " +
				$"R[{string.Join(",", p.Research.Select(r => $"{r.Key[..3]}{r.Value}"))}] int {p.LastInterest,3} ";
		}
		_report.AppendLine(line);
		GD.Print(line);
	}

	async void FinishAutotest(Match match)
	{
		foreach (var p in match.Players)
			_report.AppendLine($"gold {p.Name}: bounty {p.Stats.BountyEarned} income {p.Stats.IncomeEarned} interest {p.Stats.InterestEarned} sells {p.Stats.SellRefunds} | types {string.Join(",", p.Stats.Towers.GroupBy(t => t.Def.Id).Select(g => $"{g.Key} {g.Count()}"))} | towers {p.Stats.Towers.Count} maxed {p.Stats.Towers.Count(t => t.Level >= 10 && !t.Sold)}");
		_report.AppendLine($"result: winner={(match.Winner < 0 ? "none" : match.Players[match.Winner].Name)} at wave {match.Wave}");
		DirAccess.MakeDirRecursiveAbsolute(_autotestDir);
		using (var f = FileAccess.Open($"{_autotestDir}/report.txt", FileAccess.ModeFlags.Write)) f.StoreString(_report.ToString());
		// Let the HUD build and draw the end-of-match screen before capturing it.
		for (int i = 0; i < 3; i++) await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		Shot("final");
		GetTree().Quit();
	}

	void Shot(string name)
	{
		DirAccess.MakeDirRecursiveAbsolute(_autotestDir);
		GetViewport().GetTexture().GetImage().SavePng($"{_autotestDir}/{name}.png");
	}
}
