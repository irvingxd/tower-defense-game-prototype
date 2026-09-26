using Godot;
using TowerDefense.Data;

namespace TowerDefense.Game;

// Turns mouse/keyboard into Commands for the local player's lane.
// Click an empty tile to build the chosen tower; click a tower to select it (U upgrade, Del sell).
public partial class InputController : Node3D
{
	public Match Match;
	public int Player;
	public string SelectedTower = "ballista";
	public Vector2I? SelectedCell { get; private set; }

	Node3D _cursor;
	MeshInstance3D _rangeRing;

	public Tower Selected =>
		SelectedCell is { } c && Match.Lanes[Player].Towers.TryGetValue(c, out var t) ? t : null;

	public override void _Ready()
	{
		_cursor = Models.Td("selection-a");
		_cursor.Visible = false;
		AddChild(_cursor);
		_rangeRing = new MeshInstance3D
		{
			Mesh = new CylinderMesh { TopRadius = 1, BottomRadius = 1, Height = 0.02f, RadialSegments = 48 },
			MaterialOverride = Models.Flat(new Color(1, 1, 1, 0.18f), transparent: true),
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			Visible = false,
		};
		AddChild(_rangeRing);
	}

	public override void _Process(double delta)
	{
		var lane = Match.Lanes[Player];
		if (SelectedCell != null && Selected == null) SelectedCell = null; // sold

		_cursor.Visible = _rangeRing.Visible = false;
		Vector2I cell;
		float range;
		if (Selected is { } sel)
		{
			cell = sel.Cell;
			range = sel.Range;
		}
		else
		{
			if (GetViewport().GuiGetHoveredControl() != null) return; // pointer is over the HUD
			if (CellAt(GetViewport().GetMousePosition()) is not { } hover || Lane.CellAt(hover) == '\0') return;
			cell = hover;
			if (lane.Towers.TryGetValue(cell, out var tower)) range = tower.Range;
			else if (SelectedTower != null && lane.IsBuildable(cell)) range = Catalog.Range(Catalog.Tower(SelectedTower), 1);
			else return;
		}

		var world = lane.GlobalPosition + Lane.CellCenter(cell, 0.21f);
		_cursor.Visible = _rangeRing.Visible = true;
		_cursor.GlobalPosition = _rangeRing.GlobalPosition = world;
		_rangeRing.Scale = new Vector3(range, 1, range);
	}

	Vector2I? CellAt(Vector2 mouse)
	{
		var cam = GetViewport().GetCamera3D();
		var from = cam.ProjectRayOrigin(mouse);
		var dir = cam.ProjectRayNormal(mouse);
		if (Mathf.Abs(dir.Y) < 1e-4f) return null;
		float t = (Lane.Surface - from.Y) / dir.Y;
		if (t < 0) return null;
		var local = from + dir * t - Match.Lanes[Player].GlobalPosition;
		return new Vector2I(Mathf.RoundToInt(local.X), Mathf.RoundToInt(local.Z));
	}

	public void SelectBuild(string towerId)
	{
		SelectedTower = towerId;
		SelectedCell = null;
	}

	public void ClearSelection()
	{
		SelectedTower = null;
		SelectedCell = null;
	}

	public void SetTargeting(TargetMode mode)
	{
		if (SelectedCell is { } c) Match.Submit(Player, new SetTargeting(c, mode));
	}

	public void CycleTargeting()
	{
		if (Selected is { } t) SetTargeting((TargetMode)(((int)t.Targeting + 1) % System.Enum.GetValues<TargetMode>().Length));
	}

	// branch: which form to take when the next level is a choice (the tower card offers the buttons).
	public void UpgradeSelected(string branch = null)
	{
		if (SelectedCell is { } c) Match.Submit(Player, new UpgradeTower(c, branch));
	}

	public void SellSelected()
	{
		if (SelectedCell is { } c && Match.Submit(Player, new SellTower(c))) SelectedCell = null;
	}

	public override void _UnhandledInput(InputEvent e)
	{
		// Right-click (without dragging the camera) cancels; it fires on release so drags don't cancel.
		if (e is InputEventMouseButton { Pressed: false, ButtonIndex: MouseButton.Right } && !((CameraRig)GetViewport().GetCamera3D()).Dragging)
		{
			SelectedCell = null;
			SelectedTower = null;
			return;
		}
		if (e is InputEventMouseButton { Pressed: true } mb)
		{
			if (mb.ButtonIndex != MouseButton.Left || CellAt(mb.Position) is not { } cell) return;
			var lane = Match.Lanes[Player];
			if (lane.Towers.ContainsKey(cell)) SelectedCell = cell;
			else
			{
				SelectedCell = null;
				if (SelectedTower != null) Match.Submit(Player, new PlaceTower(cell, SelectedTower));
			}
		}
		else if (e is InputEventKey { Pressed: true, Echo: false } key)
		{
			switch (key.Keycode)
			{
				case Key.Key1: SelectBuild(Catalog.Towers[0].Id); break;
				case Key.Key2: SelectBuild(Catalog.Towers[1].Id); break;
				case Key.Key3: SelectBuild(Catalog.Towers[2].Id); break;
				case Key.Key4: SelectBuild(Catalog.Towers[3].Id); break;
				case Key.U: UpgradeSelected(); break;
				case Key.G: CycleTargeting(); break;
				case Key.Delete: case Key.Backspace: SellSelected(); break;
				case Key.Space: Match.Submit(Player, new ReadyUp()); break;
				default:
					foreach (var s in Catalog.Sends)
						if (OS.GetKeycodeString(key.Keycode) == s.Key) Match.Submit(Player, new SendUnit(s.UnitId));
					break;
			}
		}
	}
}
