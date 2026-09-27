using System.Collections.Generic;
using System.Linq;
using Godot;
using TowerDefense.Game;
using static TowerDefense.Dev.SheetRenderer;

namespace TowerDefense.Dev;

// Design previews: `--sheet concepts` = new tower ideas assembled from kit parts,
// `--sheet palettes` = every current tower (L1 and L10) in every TowerPalette.
public static class ConceptSheet
{
	static readonly Color Gold = new(1.3f, 1.1f, 0.45f);
	static readonly TowerPalette GoldOre = new("Gold", Gem: new(1f, 0.8f, 0.2f));

	static Part Td(string m, PartMode mode = PartMode.Stack, float scale = 1, Vector3 offset = default, float rot = 0, Color? tint = null, TowerPalette pal = null) =>
		new("td", m, mode, offset, rot, scale, tint, pal);
	static Part Castle(string m, PartMode mode = PartMode.Top, float scale = 1, Vector3 offset = default, float rot = 0) =>
		new("castle", m, mode, offset, rot, scale);
	static Entry E(string caption, string sub, params Part[] parts) => new(caption, new List<Part>(parts), sub);

	public static List<Entry> Concepts() => new()
	{
		// Support / economy – towers that don't shoot.
		E("Watchtower", "+1 range to towers nearby",
			Td("tower-round-bottom-a"), Td("tower-round-middle-a"), Td("tower-round-middle-b"), Td("tower-round-roof-a"),
			Castle("flag-pennant", PartMode.Top, 1f, new Vector3(0, 0, 0))),
		E("War Drum", "+25% fire rate aura",
			Td("tower-square-bottom-a"), Td("tower-square-middle-a"), Td("tower-square-roof-b"),
			Castle("flag-banner-long", PartMode.Free, 0.9f, new Vector3(0.42f, 0.3f, 0.42f), 45), Castle("flag-banner-long", PartMode.Free, 0.9f, new Vector3(-0.42f, 0.3f, 0.42f), -45)),
		E("Gold Mine", "+gold each wave, no attack",
			Td("tower-round-base"), Td("wood-structure-high", PartMode.Top, 0.7f),
			Td("detail-crystal-large", PartMode.Free, 1.1f, new Vector3(-0.4f, 0, 0.38f), pal: GoldOre),
			Td("detail-crystal", PartMode.Free, 1f, new Vector3(0.42f, 0, 0.42f), pal: GoldOre), Td("detail-rocks", PartMode.Free, 0.9f, new Vector3(0.1f, 0, 0.55f))),
		E("Beacon", "reveals + marks: +15% dmg taken",
			Td("tower-round-bottom-b"), Td("tower-round-middle-c"), Td("tower-round-top-b"), Td("detail-crystal", PartMode.Top, 1.1f, tint: Gold)),

		// Hybrids – mixing round and square parts, or two weapons on one body.
		E("Twin Ballista", "2 bolts, alternating",
			Td("tower-square-bottom-a"), Td("tower-round-middle-a"), Td("tower-round-top-a"),
			Td("weapon-ballista", PartMode.Top, 0.85f, new Vector3(-0.2f, 0, 0)), Td("weapon-ballista", PartMode.Top, 0.85f, new Vector3(0.2f, 0, 0))),
		E("Gatling", "tiny darts, huge fire rate",
			Td("tower-round-bottom-b"), Td("tower-square-middle-b"), Td("tower-square-top-a"), Td("weapon-turret", PartMode.Top, 1.2f)),
		E("Battery", "cannon + ballista, picks by target",
			Td("tower-square-bottom-c"), Td("tower-square-middle-c"), Td("tower-square-top-c"),
			Td("weapon-cannon", PartMode.Top, 0.9f, new Vector3(-0.2f, 0, 0)), Td("weapon-ballista", PartMode.Top, 0.8f, new Vector3(0.22f, 0, 0))),
		E("Prism", "crystal bolt splits to 3",
			Td("tower-square-bottom-a"), Td("tower-round-middle-b"), Td("tower-round-crystals"),
			Td("detail-crystal", PartMode.Free, 0.7f, new Vector3(-0.42f, 0, 0.38f)), Td("detail-crystal", PartMode.Free, 0.7f, new Vector3(0.42f, 0, 0.38f))),

		// Castle Kit bodies – heavier silhouettes for late-game or wall-like towers.
		E("Bastion", "hex keep, cannon, +HP to lane",
			Castle("tower-hexagon-base", PartMode.Stack, 0.9f), Castle("tower-hexagon-mid", PartMode.Stack, 0.9f),
			Castle("tower-hexagon-top", PartMode.Stack, 0.9f), Td("weapon-cannon", PartMode.Top, 1.3f)),
		E("Siege Tower", "spawns a blocker on the path",
			Td("tower-square-bottom-b"), Castle("siege-tower", PartMode.Top, 0.55f)),
		E("Gatehouse", "built ON path: slows 30%",
			Castle("wall-narrow-gate", PartMode.Stack, 0.8f, rot: 90), Castle("flag", PartMode.Top, 0.8f)),
		E("Arc Spire", "chains lightning 4x",
			Td("tower-round-bottom-c"), Td("tower-round-crystals", PartMode.Stack, 0.8f), Td("tower-round-middle-b", PartMode.Stack, 0.8f),
			Td("tower-round-crystals", PartMode.Stack, 1f)),
	};

	// One row per current tower look: its L1 and L10 stacks in each palette.
	public static List<Entry> Palettes()
	{
		(string name, string[] l1, string[] l10, string weapon)[] towers =
		{
			("Ballista", new[] { "tower-round-bottom-a", "tower-round-top-a" },
				new[] { "tower-round-bottom-a", "tower-round-middle-a", "tower-round-middle-b", "tower-round-top-a" }, "weapon-ballista"),
			("Cannon", new[] { "tower-square-bottom-b", "tower-square-top-b" },
				new[] { "tower-square-bottom-b", "tower-square-middle-a", "tower-square-middle-b", "tower-square-top-b" }, "weapon-cannon"),
			("Catapult", new[] { "tower-round-bottom-c", "tower-round-top-c" },
				new[] { "tower-round-bottom-c", "tower-round-middle-a", "tower-round-middle-b", "tower-round-top-c" }, "weapon-catapult"),
			("Crystal", new[] { "tower-round-bottom-b", "tower-round-crystals" },
				new[] { "tower-round-bottom-b", "tower-round-middle-a", "tower-round-middle-c", "tower-round-crystals" }, null),
		};
		var list = new List<Entry>();
		foreach (var (name, l1, l10, weapon) in towers)
		foreach (var pal in TowerPalette.All)
		{
			var parts = l10.Select(p => Td(p, pal: pal)).ToList();
			if (weapon != null) parts.Add(Td(weapon, PartMode.Top, 1.3f, pal: pal));
			// L1 stands in front-left of the L10 so both fit one cell.
			float y = 0;
			foreach (var p in l1)
			{
				parts.Add(Td(p, PartMode.Free, 0.6f, new Vector3(-0.75f, y, 0.55f), pal: pal));
				var piece = Models.Td(p);
				y += Models.Measure(piece).End.Y * 0.6f;
				piece.Free();
			}
			if (weapon != null) parts.Add(Td(weapon, PartMode.Free, 0.6f, new Vector3(-0.75f, y - 0.03f, 0.55f), pal: pal));
			list.Add(new Entry(pal.Name, parts, name));
		}
		return list;
	}
}
