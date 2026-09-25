using Godot;
using TowerDefense.Game;

namespace TowerDefense.Dev;

// `-- --inputprobe` pushes real mouse events at lane cells (place towers, then hover one) to check picking.
// `-- --researchprobe`: buys upgrades through commands (checking the exclusivity and wave locks), opens the menu.
public partial class ResearchProbe : Node
{
	public Match Match;
	int _frame;

	public override void _Process(double delta)
	{
		if (++_frame != 10) return;
		var me = Match.Players[0];
		me.Gold = 10_000;
		GD.Print($"research: slowing {Match.Submit(0, new BuyResearch("slowing"))}, splitting (allowed alongside slowing) {Match.Submit(0, new BuyResearch("splitting"))}, " +
			$"slowing 2 at wave 1 (should be false) {Match.Submit(0, new BuyResearch("slowing"))}, piercing at wave 1 (should be false) {Match.Submit(0, new BuyResearch("piercing"))}, " +
			$"warchest {Match.Submit(0, new BuyResearch("warchest"))}, gold {me.Gold}, interest {Match.Interest(me)}");
		Input.ParseInputEvent(new InputEventKey { Keycode = Key.R, Pressed = true });
	}
}

// `-- --showcase`: every tower type at level 1, 5 and 10 in the player lane, for checking the tiers.
public partial class Showcase : Node
{
	public Match Match;
	int _frame;

	public override void _Process(double delta)
	{
		if (++_frame != 10) return;
		Match.Players[0].Gold = 1_000_000;
		int[] levels = { 1, 5, 10 };
		for (int t = 0; t < Data.Catalog.Towers.Length; t++)
		for (int l = 0; l < levels.Length; l++)
		{
			var cell = new Vector2I(6 + l, 1 + t * 3);
			Match.Submit(0, new PlaceTower(cell, Data.Catalog.Towers[t].Id));
			for (int k = 1; k < levels[l]; k++) Match.Submit(0, new UpgradeTower(cell));
		}
		var cam = (CameraRig)GetViewport().GetCamera3D();
		cam.Size = 8f;
		cam.Focus = new Vector3(7, 0, 5.5f);
	}
}

public partial class InputProbe : Node
{
	public Match Match;
	int _frame;

	public static void Attach(Node parent, Match match)
	{
		if (System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--researchprobe") >= 0)
			parent.AddChild(new ResearchProbe { Match = match });
		if (System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--showcase") >= 0)
			parent.AddChild(new Showcase { Match = match });
		if (System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--inputprobe") >= 0)
			parent.AddChild(new InputProbe { Match = match });
	}

	public override void _Process(double delta)
	{
		_frame++;
		if (_frame == 20) Click(new Vector2I(3, 3));
		if (_frame == 22) Click(new Vector2I(3, 6));
		if (_frame == 23) GD.Print($"probe result: towers {Match.Lanes[0].Towers.Count}, gold {Match.Players[0].Gold}");
		if (_frame == 24) { Click(new Vector2I(3, 3)); Press(Key.U); }
		if (_frame == 26) GD.Print($"probe upgrade: level {Match.Lanes[0].Towers[new Vector2I(3, 3)].Level}, gold {Match.Players[0].Gold}");
	}

	Vector2 ScreenOf(Vector2I cell)
	{
		var cam = GetViewport().GetCamera3D();
		return cam.UnprojectPosition(Match.Lanes[0].GlobalPosition + Lane.CellCenter(cell));
	}

	void Move(Vector2I cell) => GetViewport().PushInput(new InputEventMouseMotion { Position = ScreenOf(cell), GlobalPosition = ScreenOf(cell) });

	void Click(Vector2I cell)
	{
		Move(cell);
		var p = ScreenOf(cell);
		GetViewport().PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = p, GlobalPosition = p });
		GetViewport().PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = p, GlobalPosition = p });
		GD.Print($"probe click {cell} -> towers {Match.Lanes[0].Towers.Count}, gold {Match.Players[0].Gold}");
	}

	void Press(Key k) => GetViewport().PushInput(new InputEventKey { Keycode = k, Pressed = true });
}
