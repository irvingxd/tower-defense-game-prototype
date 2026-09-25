using System;
using System.Linq;
using Godot;
using TowerDefense.Data;
using TowerDefense.Game;

namespace TowerDefense.Dev;

// `-- --bench <towerId> <wave> <gold> <out.txt>`: spends `gold` on ONLY that tower type in lane 0
// (best-coverage cells first, then the cheapest upgrades), runs that wave's base creeps once with no
// research and no sends, and appends one result line. Used to compare tower types honestly.
public partial class Bench : Node
{
	public Match Match;
	string _tower, _out;
	int _wave, _gold;

	public static bool TryAttach(Node parent, Match match)
	{
		var args = OS.GetCmdlineUserArgs();
		int i = Array.IndexOf(args, "--bench");
		if (i < 0 || i + 4 >= args.Length) return false;
		var bench = new Bench { Match = match, _tower = args[i + 1], _wave = int.Parse(args[i + 2]), _gold = int.Parse(args[i + 3]), _out = args[i + 4] };
		match.FirstWave = bench._wave;
		parent.AddChild(bench);
		return true;
	}

	public override void _Ready()
	{
		Engine.TimeScale = 10;
		FloatingText.Enabled = false;
		Match.BuildStarted += Setup;
		Match.WaveEnded += _ => Finish();
		Match.MatchOver += Finish;
	}

	void Setup()
	{
		var me = Match.Players[0];
		me.Lives = Match.Players[1].Lives = 100_000; // measure leaks, don't lose
		me.Gold = _gold;
		var def = Catalog.Tower(_tower);
		var lane = Match.Lanes[0];
		int bestCoverage = lane.PathCellsInRange(BestCell(lane, def).Value, def.Range);
		while (me.Gold >= def.Cost && BestCell(lane, def) is { } cell && lane.PathCellsInRange(cell, def.Range) >= bestCoverage * 0.7f)
			Match.Submit(0, new PlaceTower(cell, def.Id));
		// Leftover gold: cheapest upgrades first, spreading levels evenly.
		while (true)
		{
			var t = lane.Towers.Values.Where(x => !x.MaxLevel && x.UpgradeCost <= me.Gold).OrderBy(x => x.UpgradeCost).FirstOrDefault();
			if (t == null || !Match.Submit(0, new UpgradeTower(t.Cell))) break;
		}
		Match.Submit(0, new ReadyUp());
		Match.Submit(1, new ReadyUp());
	}

	static Vector2I? BestCell(Lane lane, TowerDef def)
	{
		Vector2I? best = null;
		int bestScore = -1;
		for (int r = 0; r < Lane.H; r++)
		for (int c = 0; c < Lane.W; c++)
		{
			var cell = new Vector2I(c, r);
			if (!lane.IsBuildable(cell)) continue;
			int score = lane.PathCellsInRange(cell, def.Range);
			if (score > bestScore) { bestScore = score; best = cell; }
		}
		return best;
	}

	bool _done;

	void Finish()
	{
		if (_done) return;
		_done = true;
		var me = Match.Players[0];
		var towers = me.Stats.Towers;
		int leaks = me.Stats.Leaks.Values.Sum(v => v.count);
		int kills = towers.Sum(t => t.Kills);
		var theme = Catalog.ThemeFor(_wave).Name;
		var line = $"{_tower,-9} wave {_wave,2} ({theme,-14}) gold {_gold,6}: towers {towers.Count,2} levels {string.Join(",", towers.Select(t => t.Level)),-24} " +
			$"damage {towers.Sum(t => t.Damage),9:0} kills {kills,3} leaks {leaks,3}  killed {100.0 * kills / Math.Max(1, kills + leaks),5:0.0}%";
		using (var f = FileAccess.Open(_out, FileAccess.FileExists(_out) ? FileAccess.ModeFlags.ReadWrite : FileAccess.ModeFlags.Write))
		{
			f.SeekEnd();
			f.StoreLine(line);
		}
		GD.Print(line);
		GetTree().Quit();
	}
}
