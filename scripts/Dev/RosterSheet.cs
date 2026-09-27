using System.Collections.Generic;
using Godot;
using TowerDefense.Game;
using static TowerDefense.Dev.SheetRenderer;

namespace TowerDefense.Dev;

// Proposed rework: 3 base towers, each with 3 upgrade paths chosen at level 5 (L10 is the path's capstone).
// `--sheet roster`   one row per base tower: L1 | path 1 L5, L10 | path 2 L5, L10 | path 3 L5, L10
// `--sheet variants` one row per base tower: three alternative L10 looks for each path (pick a favourite)
// Everything is drawn in the player's blue team palette, as it would look in a match.
public static class RosterSheet
{
	static readonly TowerPalette Blue = TowerPalette.Teams[0];
	static readonly TowerPalette Frost = Blue with { Name = "Blue Frost", Gem = new Color(0.55f, 0.9f, 1f) };
	static readonly TowerPalette Fire = Blue with { Name = "Blue Fire", Gem = new Color(1f, 0.3f, 0.1f) };
	static readonly TowerPalette Storm = Blue with { Name = "Blue Storm", Gem = new Color(1f, 0.9f, 0.2f) };

	static Part Td(string m, PartMode mode = PartMode.Stack, float scale = 1, Vector3 offset = default, float rot = 0, TowerPalette pal = null) =>
		new("td", m, mode, offset, rot, scale, null, pal ?? Blue);
	static Part Top(string m, float scale = 1, float x = 0, float z = 0, float rot = 0, TowerPalette pal = null) =>
		Td(m, PartMode.Top, scale, new Vector3(x, 0, z), rot, pal);
	static Part Free(string m, float scale, float x, float z, float rot = 0, TowerPalette pal = null) =>
		Td(m, PartMode.Free, scale, new Vector3(x, 0, z), rot, pal);
	static Part Castle(string m, float scale = 1, float x = 0, float z = 0, float rot = 0, PartMode mode = PartMode.Top) =>
		new("castle", m, mode, new Vector3(x, 0, z), rot, scale);
	static Entry E(string caption, string sub, params Part[] parts) => new(caption, new List<Part>(parts), sub);

	// Round tower pieces with the given variant letters, bottom to top.
	static Part[] Round(string bottom, string top, params string[] middles)
	{
		var list = new List<Part> { Td($"tower-round-bottom-{bottom}") };
		foreach (var m in middles) list.Add(Td($"tower-round-middle-{m}"));
		list.Add(Td(top.StartsWith("tower") ? top : $"tower-round-top-{top}"));
		return list.ToArray();
	}
	static Part[] Square(string bottom, string top, params string[] middles)
	{
		var list = new List<Part> { Td($"tower-square-bottom-{bottom}") };
		foreach (var m in middles) list.Add(Td($"tower-square-middle-{m}"));
		list.Add(Td($"tower-square-top-{top}"));
		return list.ToArray();
	}
	static Part[] Crystal(TowerPalette gem, string bottom, params string[] middles)
	{
		var list = new List<Part> { Td($"tower-round-bottom-{bottom}") };
		foreach (var m in middles) list.Add(Td($"tower-round-middle-{m}"));
		list.Add(Td("tower-round-crystals", pal: gem));
		return list.ToArray();
	}
	static Part[] With(Part[] body, params Part[] extra) => [.. body, .. extra];

	public static List<Entry> Roster() => new()
	{
		// ---------------------------------------------------------------- BALLISTA: single target, hits air
		E("BALLISTA", "L1 · 1000g", With(Round("a", "a"), Top("weapon-ballista"))),
		E("Sniper L5", "range, crits vs bosses", With(Round("a", "a", "a", "b"), Top("weapon-ballista", 1.15f), Castle("flag-pennant", 0.9f, 0.32f, 0.32f))),
		E("Sniper L10", "executes low-HP", With(Round("a", "a", "a", "b", "c"), Top("weapon-ballista", 1.55f),
			Castle("flag-wide", 0.9f, 0.34f, 0.34f), Castle("flag-wide", 0.9f, -0.34f, 0.34f))),
		E("Repeater L5", "2 bolts, fast", With(Round("a", "a", "a"), Top("weapon-ballista", 0.85f, -0.2f), Top("weapon-ballista", 0.85f, 0.2f))),
		E("Repeater L10", "3 targets at once", With(Round("a", "b", "a", "b"), Top("weapon-ballista", 0.8f, 0, -0.2f),
			Top("weapon-ballista", 0.8f, -0.2f, 0.15f, 120), Top("weapon-ballista", 0.8f, 0.2f, 0.15f, 240))),
		E("Skyhunter L5", "x2 vs flyers", With(Round("b", "b", "a"), Top("weapon-turret", 1.2f))),
		E("Skyhunter L10", "air splash, range", With(Round("b", "b", "a", "b"), Top("weapon-turret", 1.4f),
			Castle("flag-pennant", 0.9f, -0.32f, 0.32f), Castle("flag-pennant", 0.9f, 0.32f, 0.32f))),

		// ---------------------------------------------------------------- CANNON: splash (absorbs the catapult)
		E("CANNON", "L1 · 1400g", With(Square("b", "b"), Top("weapon-cannon"))),
		E("Mortar L5", "huge splash, ground", With(Square("c", "c"), Top("weapon-cannon", 1.5f))),
		E("Mortar L10", "burning craters", With(Square("c", "c", "c"), Top("weapon-cannon", 1.9f),
			Free("detail-rocks-large", 0.8f, -0.45f, 0.35f), Free("detail-rocks", 0.8f, 0.45f, 0.4f))),
		E("Flak L5", "splash vs air", With(Square("b", "b", "b"), Top("weapon-cannon", 0.9f, -0.18f), Top("weapon-cannon", 0.9f, 0.18f))),
		E("Flak L10", "3 shells per volley", With(Square("b", "b", "a", "b"), Top("weapon-cannon", 0.9f, 0, -0.2f),
			Top("weapon-cannon", 0.9f, -0.2f, 0.15f, 120), Top("weapon-cannon", 0.9f, 0.2f, 0.15f, 240))),
		E("Siege L5", "x2 vs bosses, tanks", With(Square("b", "b"), Top("weapon-catapult", 1.4f))),
		E("Siege L10", "% max-HP boulders", With([Td("tower-square-bottom-b"), Td("tower-square-middle-a")], Castle("siege-trebuchet", 0.62f),
			Castle("flag-wide", 1f, 0.4f, 0.4f))),

		// ---------------------------------------------------------------- CRYSTAL: magic, ignores armour
		E("CRYSTAL", "L1 · 1300g", Crystal(Blue, "b")),
		E("Frost L5", "slows 25%", Crystal(Frost, "b", "a")),
		E("Frost L10", "slow 45%, freezes", With(Crystal(Frost, "b", "a", "c"),
			Free("snow-detail-crystal-large", 0.8f, -0.45f, 0.35f, pal: Frost), Free("snow-detail-crystal", 0.8f, 0.45f, 0.4f, pal: Frost))),
		E("Fire L5", "burn, +dmg taken", Crystal(Fire, "b", "a")),
		E("Fire L10", "burn spreads on death", With(Crystal(Fire, "b", "a", "c"),
			Free("detail-crystal-large", 0.8f, -0.45f, 0.35f, pal: Fire), Free("detail-crystal", 0.8f, 0.45f, 0.4f, pal: Fire))),
		E("Storm L5", "lightning chains x3", Crystal(Storm, "c", "b")),
		E("Storm L10", "chains x6, stuns", With([Td("tower-round-bottom-c"), Td("tower-round-crystals", PartMode.Stack, 0.8f, pal: Storm),
			Td("tower-round-middle-b", PartMode.Stack, 0.8f)], Td("tower-round-crystals", pal: Storm))),
	};

	// Three L10 looks per path: A is the roster's, B and C are alternatives.
	public static List<Entry> Variants() => new()
	{
		E("Sniper A", "as in roster", With(Round("a", "a", "a", "b", "c"), Top("weapon-ballista", 1.55f), Castle("flag-wide", 0.9f, 0.34f, 0.34f))),
		E("Sniper B", "slim spire", With(Round("a", "a", "a", "b", "a", "c"), Top("weapon-ballista", 1.3f))),
		E("Sniper C", "siege ballista", With([Td("tower-round-bottom-a"), Td("tower-round-middle-a")], Castle("siege-ballista", 0.7f))),
		E("Repeater A", "as in roster", With(Round("a", "b", "a", "b"), Top("weapon-ballista", 0.8f, 0, -0.2f),
			Top("weapon-ballista", 0.8f, -0.2f, 0.15f, 120), Top("weapon-ballista", 0.8f, 0.2f, 0.15f, 240))),
		E("Repeater B", "square body", With(Square("a", "a", "a", "b"), Top("weapon-ballista", 0.85f, -0.2f), Top("weapon-ballista", 0.85f, 0.2f))),
		E("Repeater C", "four-way", With(Round("a", "a", "a", "b"), Top("weapon-ballista", 0.65f, -0.18f, -0.18f), Top("weapon-ballista", 0.65f, 0.18f, -0.18f, 90),
			Top("weapon-ballista", 0.65f, -0.18f, 0.18f, 270), Top("weapon-ballista", 0.65f, 0.18f, 0.18f, 180))),
		E("Skyhunter A", "as in roster", With(Round("b", "b", "a", "b"), Top("weapon-turret", 1.4f), Castle("flag-pennant", 0.9f, 0.32f, 0.32f))),
		E("Skyhunter B", "scaffold mast", With([Td("tower-round-base"), Td("wood-structure-high", PartMode.Stack, 0.75f)], Top("weapon-turret", 1.3f))),
		E("Skyhunter C", "twin turrets", With(Round("b", "b", "a", "b"), Top("weapon-turret", 1f, -0.2f), Top("weapon-turret", 1f, 0.2f))),

		E("Mortar A", "as in roster", With(Square("c", "c", "c"), Top("weapon-cannon", 1.9f), Free("detail-rocks-large", 0.8f, -0.45f, 0.35f))),
		E("Mortar B", "squat bunker", With([Td("tower-round-base")], Top("weapon-cannon", 2.1f), Free("weapon-ammo-cannonball", 2f, 0.45f, 0.4f),
			Free("weapon-ammo-cannonball", 2f, 0.3f, 0.52f))),
		E("Mortar C", "tall battery", With(Square("c", "c", "b", "c"), Top("weapon-cannon", 1.6f))),
		E("Flak A", "as in roster", With(Square("b", "b", "a", "b"), Top("weapon-cannon", 0.9f, 0, -0.2f),
			Top("weapon-cannon", 0.9f, -0.2f, 0.15f, 120), Top("weapon-cannon", 0.9f, 0.2f, 0.15f, 240))),
		E("Flak B", "turret + pennants", With(Square("b", "b", "a", "b"), Top("weapon-turret", 1.4f),
			Castle("flag-pennant", 0.9f, 0.34f, 0.34f), Castle("flag-pennant", 0.9f, -0.34f, 0.34f))),
		E("Flak C", "four-way", With(Square("b", "b", "a", "b"), Top("weapon-cannon", 0.7f, -0.18f, -0.18f), Top("weapon-cannon", 0.7f, 0.18f, -0.18f, 90),
			Top("weapon-cannon", 0.7f, -0.18f, 0.18f, 270), Top("weapon-cannon", 0.7f, 0.18f, 0.18f, 180))),
		E("Siege A", "as in roster", With([Td("tower-square-bottom-b"), Td("tower-square-middle-a")], Castle("siege-trebuchet", 0.62f))),
		E("Siege B", "scaffold trebuchet", With([Td("wood-structure-high", PartMode.Stack, 0.8f)], Castle("siege-trebuchet", 0.72f))),
		E("Siege C", "big catapult", With(Square("b", "c", "c"), Top("weapon-catapult", 1.7f), Free("weapon-ammo-boulder", 1.6f, -0.45f, 0.4f),
			Free("weapon-ammo-boulder", 1.6f, 0.45f, 0.45f))),

		E("Frost A", "as in roster", With(Crystal(Frost, "b", "a", "c"), Free("snow-detail-crystal-large", 0.8f, -0.45f, 0.35f, pal: Frost))),
		E("Frost B", "snow plinth", With([Td("snow-tile", PartMode.Free, 1.4f)], [Td("tower-round-bottom-b", PartMode.Stack, 1f, new Vector3(0, 0.2f, 0)),
			Td("tower-round-middle-a"), Td("tower-round-middle-c"), Td("tower-round-crystals", pal: Frost)])),
		E("Frost C", "square shrine", With([Td("tower-square-bottom-a"), Td("tower-square-middle-a")], Td("tower-round-crystals", pal: Frost),
			Free("snow-detail-crystal", 0.8f, -0.45f, 0.4f, pal: Frost), Free("snow-detail-crystal", 0.8f, 0.45f, 0.4f, pal: Frost))),
		E("Fire A", "as in roster", With(Crystal(Fire, "b", "a", "c"), Free("detail-crystal-large", 0.8f, -0.45f, 0.35f, pal: Fire))),
		E("Fire B", "volcanic", With(Crystal(Fire, "c", "c", "b"), Free("detail-rocks-large", 0.8f, -0.45f, 0.35f), Free("detail-rocks", 0.8f, 0.45f, 0.4f))),
		E("Fire C", "square brazier", With([Td("tower-square-bottom-c"), Td("tower-square-middle-c")], Td("tower-round-crystals", pal: Fire))),
		E("Storm A", "as in roster", With([Td("tower-round-bottom-c"), Td("tower-round-crystals", PartMode.Stack, 0.8f, pal: Storm),
			Td("tower-round-middle-b", PartMode.Stack, 0.8f)], Td("tower-round-crystals", pal: Storm))),
		E("Storm B", "lightning rod", Crystal(Storm, "b", "a", "b", "c")),
		E("Storm C", "pylon ring", With(Crystal(Storm, "c", "a"), Free("detail-crystal", 0.7f, -0.45f, 0.35f, pal: Storm),
			Free("detail-crystal", 0.7f, 0.45f, 0.35f, pal: Storm), Free("detail-crystal", 0.7f, 0, -0.5f, pal: Storm))),
	};
}
