using System.Collections.Generic;
using Godot;
using static TowerDefense.Dev.SheetRenderer;

namespace TowerDefense.Dev;

// Proposed looks for the tower milestone branches: level 5 picks a branch, level 10 a sub-branch.
// One row per tower: today's L1 | branch A (L5) | A1 | A2 (L10) | today's L10 | branch B (L5) | B1 | B2 (L10).
public static class BranchSheet
{
	static readonly Color Frost = new(0.45f, 0.8f, 1.45f);
	static readonly Color Sky = new(0.8f, 0.9f, 1.2f);
	static readonly Color Ember = new(1.25f, 0.85f, 0.7f);

	static Part Td(string m, PartMode mode = PartMode.Stack, float scale = 1, Vector3 offset = default, float rot = 0, Color? tint = null) =>
		new("td", m, mode, offset, rot, scale, tint);
	static Part Castle(string m, PartMode mode = PartMode.Top, float scale = 1, Vector3 offset = default, float rot = 0, Color? tint = null) =>
		new("castle", m, mode, offset, rot, scale, tint);
	static Entry E(string caption, string sub, params Part[] parts) => new(caption, new List<Part>(parts), sub);

	static readonly Color Fire = new(1.35f, 0.55f, 0.35f);

	public static List<Entry> Entries() => new()
	{
		// ------------------------------------------------------------------ BALLISTA (round a)
		E("Ballista L1", "today",
			Td("tower-round-bottom-a"), Td("tower-round-top-a"), Td("weapon-ballista", PartMode.Top)),
		E("SNIPER (L5)", "range + crits",
			Td("tower-round-bottom-a"), Td("tower-round-middle-a"), Td("tower-round-middle-b"), Td("tower-round-top-a"),
			Td("weapon-ballista", PartMode.Top, 1.2f), Castle("flag-pennant", PartMode.Top, 0.9f, new Vector3(0.32f, 0, 0.32f))),
		E("Deadeye (L10)", "execute low HP",
			Td("tower-round-bottom-a"), Td("tower-round-middle-a"), Td("tower-round-middle-b"), Td("tower-round-middle-c"), Td("tower-round-top-a"),
			Td("weapon-ballista", PartMode.Top, 1.55f, tint: Ember), Castle("flag-wide", PartMode.Top, 0.9f, new Vector3(0.34f, 0, 0.34f)),
			Castle("flag-wide", PartMode.Top, 0.9f, new Vector3(-0.34f, 0, 0.34f))),
		E("Piercer (L10)", "bolt hits a line",
			Td("tower-round-bottom-a"), Td("tower-round-middle-a"), Td("tower-round-middle-b"), Td("tower-round-top-a"),
			Castle("siege-ballista", PartMode.Top, 0.75f)),

		E("Ballista L10", "today",
			Td("tower-round-bottom-a"), Td("tower-round-middle-a"), Td("tower-round-middle-b"), Td("tower-round-top-a"),
			Td("weapon-ballista", PartMode.Top, 1.3f)),
		E("REPEATER (L5)", "3x fire rate",
			Td("tower-round-bottom-b"), Td("tower-round-middle-a"), Td("tower-round-top-b"), Td("weapon-turret", PartMode.Top, 1.2f)),
		E("Stormbringer (L10)", "fires at 2 targets",
			Td("tower-square-bottom-a"), Td("tower-square-middle-a"), Td("tower-square-top-a"),
			Td("weapon-turret", PartMode.Top, 1.1f, new Vector3(-0.2f, 0, 0)), Td("weapon-turret", PartMode.Top, 1.1f, new Vector3(0.2f, 0, 0))),
		E("Skyhunter (L10)", "+100% vs flyers",
			Td("tower-round-bottom-b"), Td("tower-round-middle-a"), Td("tower-round-middle-b"), Td("tower-round-top-b"),
			Td("weapon-turret", PartMode.Top, 1.35f, tint: Sky), Castle("flag-pennant", PartMode.Top, 0.9f, new Vector3(-0.32f, 0, 0.32f)),
			Castle("flag-pennant", PartMode.Top, 0.9f, new Vector3(0.32f, 0, 0.32f))),

		// ------------------------------------------------------------------ CANNON (square b)
		E("Cannon L1", "today",
			Td("tower-square-bottom-b"), Td("tower-square-top-b"), Td("weapon-cannon", PartMode.Top)),
		E("MORTAR (L5)", "huge splash, slow",
			Td("tower-square-bottom-c"), Td("tower-square-top-c"), Td("weapon-cannon", PartMode.Top, 1.5f)),
		E("Bombard (L10)", "splash + burn",
			Td("tower-square-bottom-c"), Td("tower-square-middle-c"), Td("tower-square-top-c"),
			Td("weapon-cannon", PartMode.Top, 1.8f, tint: Ember), Castle("flag-banner-short", PartMode.Top, 1f, new Vector3(0.36f, 0, 0.36f))),
		E("Earthshaker (L10)", "stuns on impact",
			Td("tower-square-bottom-c"), Td("tower-square-middle-c"), Td("tower-square-top-c"), Td("weapon-cannon", PartMode.Top, 1.7f),
			Td("detail-rocks-large", PartMode.Free, 0.8f, new Vector3(-0.45f, 0, 0.35f)), Td("detail-rocks", PartMode.Free, 0.8f, new Vector3(0.45f, 0, 0.4f))),

		E("Cannon L10", "today",
			Td("tower-square-bottom-b"), Td("tower-square-middle-a"), Td("tower-square-middle-b"), Td("tower-square-top-b"),
			Td("weapon-cannon", PartMode.Top, 1.3f)),
		E("FLAK (L5)", "strong vs air",
			Td("tower-square-bottom-b"), Td("tower-square-middle-b"), Td("tower-square-top-b"),
			Td("weapon-cannon", PartMode.Top, 1f, new Vector3(-0.18f, 0, 0), tint: Sky), Td("weapon-cannon", PartMode.Top, 1f, new Vector3(0.18f, 0, 0), tint: Sky)),
		E("Skyburst (L10)", "air splash x2",
			Td("tower-square-bottom-b"), Td("tower-square-middle-a"), Td("tower-square-middle-b"), Td("tower-square-top-b"),
			Td("weapon-turret", PartMode.Top, 1.3f, tint: Sky), Castle("flag-wide", PartMode.Top, 0.9f, new Vector3(0.36f, 0, 0.36f))),
		E("Hailstorm (L10)", "3 shells per volley",
			Td("tower-square-bottom-b"), Td("tower-square-middle-b"), Td("tower-square-top-b"),
			Td("weapon-cannon", PartMode.Top, 0.9f, new Vector3(0, 0, -0.2f)), Td("weapon-cannon", PartMode.Top, 0.9f, new Vector3(-0.2f, 0, 0.15f), 120),
			Td("weapon-cannon", PartMode.Top, 0.9f, new Vector3(0.2f, 0, 0.15f), 240)),

		// ------------------------------------------------------------------ CATAPULT (round c)
		E("Catapult L1", "today",
			Td("tower-round-bottom-c"), Td("tower-round-top-c"), Td("weapon-catapult", PartMode.Top)),
		E("TREBUCHET (L5)", "siege x2.5, range",
			Td("tower-round-base"), Castle("siege-trebuchet", PartMode.Top, 0.6f)),
		E("Titan (L10)", "% max-HP vs bosses",
			Td("wood-structure-high", PartMode.Stack, 0.8f), Castle("siege-trebuchet", PartMode.Top, 0.72f), Castle("flag-wide", PartMode.Top, 1f, new Vector3(0.4f, 0, 0.4f))),
		E("Wallbreaker (L10)", "shreds armour",
			Td("tower-round-base"), Castle("siege-trebuchet", PartMode.Top, 0.72f, tint: Ember),
			Castle("siege-ram", PartMode.Free, 0.5f, new Vector3(0.1f, 0, 0.55f), 90), Td("detail-rocks-large", PartMode.Free, 0.7f, new Vector3(-0.5f, 0, 0.4f))),

		E("Catapult L10", "today",
			Td("tower-round-bottom-c"), Td("tower-round-middle-a"), Td("tower-round-middle-b"), Td("tower-round-top-c"),
			Td("weapon-catapult", PartMode.Top, 1.3f)),
		E("ONAGER (L5)", "3-boulder barrage",
			Td("tower-round-base"), Castle("siege-catapult", PartMode.Top, 0.6f)),
		E("Barrage (L10)", "5 boulders per volley",
			Td("wood-structure-high", PartMode.Stack, 0.8f), Castle("siege-catapult", PartMode.Top, 0.72f),
			Td("weapon-ammo-boulder", PartMode.Free, 1.6f, new Vector3(-0.45f, 0, 0.4f)), Td("weapon-ammo-boulder", PartMode.Free, 1.6f, new Vector3(-0.25f, 0, 0.55f)),
			Td("weapon-ammo-boulder", PartMode.Free, 1.6f, new Vector3(0.45f, 0, 0.45f))),
		E("Avalanche (L10)", "boulders roll along path",
			Td("tower-round-base"), Castle("siege-catapult", PartMode.Top, 0.72f, tint: Ember),
			Td("detail-rocks-large", PartMode.Free, 0.8f, new Vector3(-0.45f, 0, 0.4f)), Td("detail-rocks-large", PartMode.Free, 0.7f, new Vector3(0.45f, 0, 0.45f), 90)),

		// ------------------------------------------------------------------ CRYSTAL (round b + crystal top) — Fire / Frost
		E("Crystal L1", "magic bolt",
			Td("tower-round-bottom-b"), Td("tower-round-crystals")),
		E("FIRE (L5)", "burn, stops regen",
			Td("tower-round-bottom-b"), Td("tower-round-middle-a"), Td("tower-round-crystals", tint: Fire)),
		E("Inferno (L10)", "burn spreads on death",
			Td("tower-round-bottom-b"), Td("tower-round-middle-a"), Td("tower-round-middle-c"), Td("tower-round-crystals", tint: Fire),
			Td("detail-crystal-large", PartMode.Free, 0.8f, new Vector3(-0.45f, 0, 0.35f), tint: Fire), Td("detail-crystal", PartMode.Free, 0.8f, new Vector3(0.45f, 0, 0.4f), tint: Fire)),
		E("Sunfire (L10)", "beam ramps on one target",
			Td("tower-round-bottom-b"), Td("tower-round-middle-a"), Td("tower-round-middle-b"), Td("tower-round-middle-c"), Td("tower-round-crystals", tint: Fire)),
		E("Crystal L10", "unattuned, for scale",
			Td("tower-round-bottom-b"), Td("tower-round-middle-a"), Td("tower-round-middle-c"), Td("tower-round-crystals")),
		E("FROST (L5)", "slow, hits air",
			Td("tower-round-bottom-b"), Td("tower-round-middle-a"), Td("tower-round-crystals", tint: Frost)),
		E("Glacier (L10)", "every 3rd hit freezes",
			Td("tower-round-bottom-b"), Td("tower-round-middle-a"), Td("tower-round-middle-c"), Td("tower-round-crystals", tint: Frost),
			Td("detail-crystal-large", PartMode.Free, 0.8f, new Vector3(-0.45f, 0, 0.35f), tint: Frost), Td("detail-crystal", PartMode.Free, 0.8f, new Vector3(0.45f, 0, 0.4f), tint: Frost)),
		E("Blizzard (L10)", "slow aura, chilled +10%",
			Td("snow-tile", PartMode.Free, 1.4f), Td("tower-round-bottom-b", PartMode.Stack, 1f, new Vector3(0, 0.2f, 0)), Td("tower-round-middle-a"), Td("tower-round-middle-c"),
			Td("tower-round-crystals", tint: Frost), Td("snow-detail-crystal-large", PartMode.Free, 0.8f, new Vector3(-0.45f, 0.2f, 0.35f))),
	};
}
