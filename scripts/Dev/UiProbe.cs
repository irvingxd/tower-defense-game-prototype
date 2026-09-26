using System;
using Godot;
using TowerDefense.Game;

namespace TowerDefense.Dev;

// `-- --uiprobe <dir>`: scripted walk through the HUD (every flyout, tower card, pause, a live wave with
// slowed + burning creeps), saving a screenshot at each step, then quits. Uses dev cheats for gold/research.
public partial class UiProbe : Node
{
	public Match Match;
	string _dir;
	int _frame, _goldFrame;

	public static void Attach(Node parent, Match match)
	{
		var args = OS.GetCmdlineUserArgs();
		int i = Array.IndexOf(args, "--uiprobe");
		if (i >= 0 && i + 1 < args.Length)
			parent.AddChild(new UiProbe { Match = match, _dir = args[i + 1], ProcessMode = ProcessModeEnum.Always });
	}

	public override void _Process(double delta)
	{
		_frame++;
		switch (_frame)
		{
			case 10: Setup(); break;
			case 14: DragTest(); break;
			case 30: Shot("1-default"); Key(Godot.Key.Q); break;
			case 40: Shot("2-buildings"); Key(Godot.Key.R); break;
			case 50: Shot("3-upgrades"); Key(Godot.Key.T); break;
			case 60: Shot("4-army"); Key(Godot.Key.I); break;
			case 70: Shot("5-intel"); Key(Godot.Key.Escape); break;   // close the flyout
			case 74: Click(new Vector2I(3, 3)); break;
			case 76: Key(Godot.Key.G); Key(Godot.Key.G); break;          // targeting: First -> Last -> Strongest
			case 80: Shot("6-towercard"); Key(Godot.Key.Escape); break; // deselect
			case 84: Key(Godot.Key.Escape); break;                       // pause
			case 94: Shot("7-pause"); Key(Godot.Key.Escape); break;     // resume
			case 100: Key(Godot.Key.Space); break;
			case 130: Shot("8-banner"); break;
			case 400: HoverFirstCreep(); break;
			case 404: Shot("9b-creepcard"); break;
			case 520: Shot("9-wave"); Key(Godot.Key.F); Key(Godot.Key.F); break; // 3x speed until wave 2
		}
		if (_frame > 520 && _goldFrame == 0 && Match.Wave >= 2) _goldFrame = _frame;
		if (_goldFrame > 0 && _frame == _goldFrame + 12) { Shot("10-gold"); GetTree().Quit(); }
		if (_frame > 6000) GetTree().Quit();
	}

	void Setup()
	{
		var me = Match.Players[0];
		me.Gold = 500_000;
		Place(new Vector2I(3, 3), "ballista", 4);
		Place(new Vector2I(3, 6), "cannon", 5);
		Place(new Vector2I(4, 8), "ballista", 2);
		Place(new Vector2I(6, 3), "catapult", 10);
		me.Research["slowing"] = 2;     // dev cheat: skip the wave locks
		me.Research["incendiary"] = 2;
		me.Research["warchest"] = 1;
		me.Gold = 6400;
		Match.Submit(0, new SendUnit("green_blob"));
		Match.Submit(0, new SendUnit("chicken"));
	}

	// Right-drag should pan the camera without cancelling build mode; a plain right-click should cancel.
	void DragTest()
	{
		var cam = (CameraRig)GetViewport().GetCamera3D();
		var input = GetParent().GetNodeOrNull<InputController>(nameof(InputController)) ?? FindInput();
		var before = cam.Focus;
		var vp = GetViewport();
		var a = new Vector2(800, 450);
		vp.PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Right, Pressed = true, Position = a, GlobalPosition = a });
		for (int i = 1; i <= 10; i++)
		{
			var p = a + new Vector2(-20 * i, 0);
			vp.PushInput(new InputEventMouseMotion { Position = p, GlobalPosition = p, Relative = new Vector2(-20, 0) });
		}
		vp.PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Right, Pressed = false, Position = a - new Vector2(200, 0), GlobalPosition = a - new Vector2(200, 0) });
		GD.Print($"drag: focus {before} -> {cam.Focus} (moved {cam.Focus.X - before.X:0.00} x), build mode kept: {input.SelectedTower != null}");
		cam.Focus = before;
		vp.PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Right, Pressed = true, Position = a, GlobalPosition = a });
		vp.PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Right, Pressed = false, Position = a, GlobalPosition = a });
		GD.Print($"right-click: build mode cancelled: {input.SelectedTower == null}");
		input.SelectBuild("ballista");
	}

	InputController FindInput()
	{
		foreach (var c in GetParent().GetChildren()) if (c is InputController ic) return ic;
		return null;
	}

	void HoverFirstCreep()
	{
		var hud = FindHud(GetParent());
		var lane = Match.Lanes[0];
		if (hud == null || lane.Enemies.Count == 0) return;
		var e = lane.Enemies[0];
		hud.DebugMouse = GetViewport().GetCamera3D().UnprojectPosition(e.GlobalPosition + new Vector3(0, e.Def.Height * 0.5f, 0));
	}

	static UI.Hud FindHud(Node n)
	{
		foreach (var c in n.GetChildren()) if (c is UI.Hud h) return h;
		return null;
	}

	void Place(Vector2I cell, string id, int level)
	{
		Match.Submit(0, new PlaceTower(cell, id));
		for (int i = 1; i < level; i++) Match.Submit(0, new UpgradeTower(cell));
	}

	static void Key(Key k)
	{
		Input.ParseInputEvent(new InputEventKey { Keycode = k, Pressed = true });
		Input.ParseInputEvent(new InputEventKey { Keycode = k, Pressed = false });
	}

	void Click(Vector2I cell)
	{
		var cam = GetViewport().GetCamera3D();
		var p = cam.UnprojectPosition(Match.Lanes[0].GlobalPosition + Lane.CellCenter(cell));
		GetViewport().PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = p, GlobalPosition = p });
		GetViewport().PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = p, GlobalPosition = p });
	}

	void Shot(string name)
	{
		DirAccess.MakeDirRecursiveAbsolute(_dir);
		GetViewport().GetTexture().GetImage().SavePng($"{_dir}/{name}.png");
	}
}
