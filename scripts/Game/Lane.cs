using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using TowerDefense.Data;

namespace TowerDefense.Game;

// One player's board: the tile grid, the creep path, their towers and the creeps walking it.
// Everything inside is in lane-local space: cell (c, r) sits at (c, 0, r).
public partial class Lane : Node3D
{
	// S spawn, E castle, # path, . buildable, T/R/C decoration (tree, rock, crystal)
	static readonly string[] Layout =
	{
		"....S....",
		".T..#....",
		".####..R.",
		".#.......",
		".#####...",
		"C....#.T.",
		".....#...",
		".#####...",
		".#.....T.",
		".#####...",
		".T...#...",
		".....#.R.",
		"..T..#...",
		".....E...",
	};

	public const int W = 9, H = 14;
	public const float Surface = 0.2f; // top of the grass tiles

	public int Index;
	public readonly List<Vector3> PathPoints = new();
	public readonly List<Vector2I> PathCells = new();
	public readonly Dictionary<Vector2I, Tower> Towers = new();
	public readonly List<Enemy> Enemies = new();
	public Match Match;

	readonly Queue<(string unit, bool sent)> _spawnQueue = new();
	float _hpMul = 1f, _armorMul = 1f;
	float _spawnTimer;
	const float SpawnInterval = 0.85f;

	public bool Busy => _spawnQueue.Count > 0 || Enemies.Count > 0;

	public static char CellAt(Vector2I c) =>
		c.X < 0 || c.Y < 0 || c.X >= W || c.Y >= H ? '\0' : Layout[c.Y][c.X];

	public static bool IsPathChar(char ch) => ch is 'S' or 'E' or '#';

	public bool IsBuildable(Vector2I c) => CellAt(c) == '.' && !Towers.ContainsKey(c);

	public static Vector3 CellCenter(Vector2I c, float y = Surface) => new(c.X, y, c.Y);

	public override void _Ready()
	{
		BuildPath();
		BuildTiles();
	}

	void BuildPath()
	{
		var cur = Find('S');
		var seen = new HashSet<Vector2I>();
		while (true)
		{
			seen.Add(cur);
			PathCells.Add(cur);
			PathPoints.Add(CellCenter(cur, 0.1f));
			if (CellAt(cur) == 'E') break;
			var options = Dirs.Select(d => cur + d).Where(n => IsPathChar(CellAt(n)) && !seen.Contains(n)).ToList();
			if (options.Count == 0) throw new InvalidOperationException($"Path dead-ends at {cur}");
			cur = options[0];
		}
	}

	static Vector2I Find(char ch)
	{
		for (int r = 0; r < H; r++)
		for (int c = 0; c < W; c++)
			if (Layout[r][c] == ch) return new Vector2I(c, r);
		throw new InvalidOperationException($"No '{ch}' in lane layout");
	}

	static readonly Vector2I[] Dirs = { new(1, 0), new(-1, 0), new(0, 1), new(0, -1) };

	void BuildTiles()
	{
		var rng = new RandomNumberGenerator { Seed = 1234 + (ulong)Index };
		for (int r = 0; r < H; r++)
		for (int c = 0; c < W; c++)
		{
			var cell = new Vector2I(c, r);
			var ch = CellAt(cell);
			Node3D tile;
			if (IsPathChar(ch)) tile = PathTile(cell);
			else
			{
				tile = Models.Td(ch switch
				{
					'T' => rng.Randf() < 0.5f ? "tile-tree" : "tile-tree-double",
					'R' => "tile-rock",
					'C' => "tile-crystal",
					_ => "tile",
				});
			}
			tile.Position = new Vector3(c, 0, r);
			AddChild(tile);
		}

		// Crypt behind the spawn, a keep behind the castle end.
		var crypt = Models.Graveyard("crypt");
		crypt.Position = CellCenter(PathCells[0] + new Vector2I(0, -1), 0f);
		AddChild(crypt);

		var keep = Tower.Stack("tower-square-bottom-a", "tower-square-middle-a", "tower-square-roof-a");
		keep.Position = CellCenter(PathCells[^1] + new Vector2I(0, 1), 0f);
		keep.Scale = Vector3.One * 1.2f;
		AddChild(keep);
	}

	// Picks straight / corner / end and rotates it to match this cell's path neighbours.
	Node3D PathTile(Vector2I cell)
	{
		var links = Dirs.Where(d => IsPathChar(CellAt(cell + d))).ToList();
		string model;
		Vector2I[] baseLinks;
		if (links.Count == 1) { model = "tile-end-round"; baseLinks = new[] { new Vector2I(0, 1) }; }
		else if (links.Count == 2 && links[0] + links[1] == Vector2I.Zero) { model = "tile-straight"; baseLinks = new[] { new Vector2I(0, 1), new Vector2I(0, -1) }; }
		else if (links.Count == 2) { model = "tile-corner-round"; baseLinks = new[] { new Vector2I(1, 0), new Vector2I(0, 1) }; }
		else { model = "tile-crossing"; baseLinks = Array.Empty<Vector2I>(); }

		var tile = Models.Td(model);
		for (int k = 0; k < 4 && baseLinks.Length > 0; k++)
		{
			var rotated = baseLinks.Select(d => RotateY(d, k)).ToHashSet();
			if (rotated.SetEquals(links))
			{
				tile.RotationDegrees = new Vector3(0, 90 * k, 0);
				break;
			}
		}
		return tile;
	}

	// Grid direction (x, z) rotated k * 90 degrees about +Y, matching Node3D.RotationDegrees.Y.
	static Vector2I RotateY(Vector2I d, int k)
	{
		for (int i = 0; i < k; i++) d = new Vector2I(d.Y, -d.X);
		return d;
	}

	public int PathCellsInRange(Vector2I cell, float range) =>
		PathCells.Count(p => (p - cell).LengthSquared() <= range * range);

	public void QueueWave(IEnumerable<(string unit, bool sent)> units, float hpMul, float armorMul)
	{
		_hpMul = hpMul;
		_armorMul = armorMul;
		foreach (var u in units) _spawnQueue.Enqueue(u);
		_spawnTimer = 0;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_spawnQueue.Count == 0) return;
		_spawnTimer -= (float)delta;
		if (_spawnTimer > 0) return;
		_spawnTimer = SpawnInterval;
		var (unit, sent) = _spawnQueue.Dequeue();
		var enemy = new Enemy { Def = Catalog.Unit(unit), Sent = sent, Lane = this, HpMultiplier = _hpMul, ArmorMultiplier = _armorMul };
		Enemies.Add(enemy);
		AddChild(enemy);
	}

	public void SpawnSplit(string unit, bool sent, float hpMul, float armorMul, int waypoint, Vector3 position, float distance)
	{
		var enemy = new Enemy { Def = Catalog.Unit(unit), Sent = sent, Lane = this, HpMultiplier = hpMul, ArmorMultiplier = armorMul, StartWaypoint = waypoint, StartPosition = position, Distance = distance };
		Enemies.Add(enemy);
		CallDeferred(Node.MethodName.AddChild, enemy);
	}

	public void OnEnemyLeaked(Enemy e)
	{
		Enemies.Remove(e);
		Match.OnLeak(Index, e);
	}

	public void OnEnemyKilled(Enemy e)
	{
		Enemies.Remove(e);
		Match.OnKill(Index, e);
	}

	public Tower AddTower(Vector2I cell, TowerDef def)
	{
		var t = new Tower { Def = def, Lane = this, Cell = cell, Position = CellCenter(cell) };
		Towers[cell] = t;
		AddChild(t);
		return t;
	}

	public void RemoveTower(Vector2I cell)
	{
		if (Towers.Remove(cell, out var t)) t.QueueFree();
	}
}
