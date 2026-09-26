using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace TowerDefense.Data;

public sealed record TowerDef(
	string Id, string Name, int Cost,
	float Damage, float Range, float Cooldown, float Splash, bool HitsAir,
	string Shape, char Variant, string Weapon, string Ammo,
	float ProjectileSpeed, float Arc);

public enum Role { Basic, Swarm, Fast, Tank, Armored, Flyer, Regen, Splitter, Boss }

public sealed record UnitDef
{
	public string Id { get; init; }
	public string Name { get; init; }
	public string Model { get; init; }
	public Role Role { get; init; }
	public float Hp { get; init; }
	public float Speed { get; init; }
	public float Height { get; init; }
	public int Lives { get; init; } = 1;
	public bool Flying { get; init; }
	public float Armor { get; init; }         // flat reduction per hit, scales with sqrt of wave HP multiplier
	public float Regen { get; init; }         // fraction of max HP per second
	public string SplitInto { get; init; }
	public int SplitCount { get; init; }
}

public sealed record SendDef(string UnitId, int Cost, int Income, int UnlockWave, string Key);

public sealed record Theme(string Name, string[] Units, string Boss);

// A player-wide upgrade bought from the Upgrades menu. Costs/UnlockWaves are per level (index = level - 1).
public sealed record ResearchDef(
	string Id, string Name, string Target, int[] Costs, int[] UnlockWaves, string ExclusiveWith,
	Func<int, string> Describe)
{
	public int MaxLevel => Costs.Length;
}

public static class Catalog
{
	// ---------------------------------------------------------------- towers

	public static readonly TowerDef[] Towers =
	{
		new("ballista", "Ballista", 1000, 28, 2.6f, 0.8f, 0f, true, "round", 'a', "weapon-ballista", "weapon-ammo-arrow", 10f, 0.15f),
		new("cannon", "Cannon", 1400, 48, 3.0f, 1.3f, 1.0f, true, "square", 'b', "weapon-cannon", "weapon-ammo-cannonball", 7f, 0.5f),
		new("catapult", "Catapult", 2000, 90, 3.8f, 2.6f, 1.3f, false, "round", 'c', "weapon-catapult", "weapon-ammo-boulder", 5f, 1.6f),
	};

	// Ten levels. Each adds +25% damage and a little attack speed; levels 5 and 10 are milestones
	// that also extend range and change the building (see Tower.VisualTier).
	public const int MaxTowerLevel = 10;

	// Tower roles: the Ballista is the all-rounder (hits air, no bonus); the Catapult is the siege tower
	// (bosses and tanks), the Cannon cracks armour. Multiplies the hit before armour.
	public const float SiegeBonus = 1.75f, ArmourBonus = 1.5f;

	public static bool IsArmoured(UnitDef u) => u.Armor >= 4 && u.Role != Role.Boss;
	public static bool IsSiegeTarget(UnitDef u) => u.Role is Role.Boss or Role.Tank;

	public static float RoleBonus(TowerDef d, UnitDef u) => d.Id switch
	{
		"catapult" when IsSiegeTarget(u) => SiegeBonus,
		"cannon" when IsArmoured(u) => ArmourBonus,
		_ => 1f,
	};

	public static string RoleText(TowerDef d) => d.Id switch
	{
		"catapult" => $"×{SiegeBonus} vs bosses & tanks",
		"cannon" => $"×{ArmourBonus} vs armoured",
		_ => "All-rounder",
	};

	// Normal levels: +25% damage for 30% of what the tower cost so far. Milestones are proportional too:
	// 4 -> 5 doubles damage and costs everything invested so far; 9 -> 10 triples it for twice that.
	// So damage per gold stays flat — upgrades trade gold for board space, never for efficiency.
	static float DamageStep(int toLevel) => toLevel == 5 ? 2f : toLevel == 10 ? 3f : 1.25f;
	static float CostStep(int toLevel) => toLevel == 5 ? 1f : toLevel == 10 ? 2f : 0.3f;

	public static float Damage(TowerDef d, int level)
	{
		float m = 1f;
		for (int l = 2; l <= level; l++) m *= DamageStep(l);
		return d.Damage * m;
	}

	// Total gold a tower at this level represents (rounded to 10 so displayed prices stay tidy).
	public static int TotalCost(TowerDef d, int level)
	{
		float c = d.Cost;
		for (int l = 2; l <= level; l++) c *= 1f + CostStep(l);
		return Mathf.RoundToInt(c / 10f) * 10;
	}

	public static bool IsMilestone(int toLevel) => toLevel is 5 or 10;
	public static float MilestoneDamage(int toLevel) => DamageStep(toLevel);
	public static float Range(TowerDef d, int level) =>
		d.Range + 0.08f * (level - 1) + (level >= 5 ? 0.3f : 0f) + (level >= 10 ? 0.3f : 0f);
	public static float Cooldown(TowerDef d, int level) => d.Cooldown * Mathf.Pow(0.96f, level - 1);
	public static float Dps(TowerDef d, int level) => Damage(d, level) / Cooldown(d, level);
	// Cost to go from `level` to level + 1: half the tower's price, growing 30% per level, rounded to 5g
	// (1 -> 2 costs 0.5x, 9 -> 10 about 4x; a max tower is ~17x the base price for ~11x the dps).
	public static int UpgradeCost(TowerDef d, int level) => TotalCost(d, level + 1) - TotalCost(d, level);
	public static int VisualTier(int level) => level >= 10 ? 2 : level >= 5 ? 1 : 0;

	// ---------------------------------------------------------------- creeps

	static UnitDef U(string model, Role role, float height = 0.7f)
	{
		var u = new UnitDef { Id = model.ToLowerInvariant(), Name = model.Replace("_", " "), Model = model, Role = role, Height = height };
		return role switch
		{
			Role.Basic => u with { Hp = 45, Speed = 1.1f },
			Role.Swarm => u with { Hp = 20, Speed = 1.3f, Height = height * 0.8f },
			Role.Fast => u with { Hp = 34, Speed = 1.9f },
			Role.Tank => u with { Hp = 150, Speed = 0.7f, Lives = 2, Height = height * 1.15f },
			Role.Armored => u with { Hp = 95, Speed = 0.9f, Armor = 5 },
			Role.Flyer => u with { Hp = 48, Speed = 1.2f, Flying = true },
			Role.Regen => u with { Hp = 110, Speed = 1.0f, Regen = 0.04f },
			Role.Splitter => u with { Hp = 90, Speed = 0.9f, Height = height * 1.1f },
			Role.Boss => u with { Hp = 700, Speed = 0.8f, Lives = 3, Height = height * 1.6f, Armor = 3 },
			_ => u,
		};
	}

	public static readonly UnitDef[] Units =
	{
		// Slime Meadow
		U("Green_Blob", Role.Basic, 0.6f),
		U("Pink_Slime", Role.Swarm, 0.55f),
		U("Green_Spiky_Blob", Role.Splitter, 0.8f) with { SplitInto = "green_blob", SplitCount = 3 },
		// Mushroom Grove
		U("Mushnub", Role.Basic),
		U("Mushnub_Evolved", Role.Armored),
		U("Mushroom_King", Role.Boss),
		// Barnyard
		U("Chicken", Role.Fast, 0.6f),
		U("Pigeon", Role.Swarm),
		U("Cat", Role.Basic),
		U("Bunny", Role.Boss) with { Speed = 1.3f },
		// Sky Pests
		U("Armabee", Role.Flyer),
		U("Armabee_Evolved", Role.Flyer) with { Armor = 4, Hp = 70 },
		U("Alpaking", Role.Flyer),
		U("Alpaking_Evolved", Role.Boss) with { Flying = true },
		// Cactus Desert
		U("Cactoro", Role.Armored),
		U("Birb", Role.Fast),
		U("Cactoro_2", Role.Tank),
		U("Cactoro_King", Role.Boss) with { Model = "Cactoro_2", Name = "Cactoro King", Armor = 6 },
		// Reef
		U("Fish", Role.Basic),
		U("Glub", Role.Flyer),
		U("Squidle", Role.Regen) with { Flying = true },
		U("Fish_2", Role.Boss) with { Name = "Deep Fish", Regen = 0.02f },
		// Swamp
		U("Frog", Role.Tank),
		U("Glub_Evolved", Role.Regen) with { Flying = true },
		U("Dino", Role.Boss),
		// Frost Peaks
		U("Yeti", Role.Tank),
		U("Goleling", Role.Flyer) with { Armor = 4, Hp = 70 },
		U("Hywirl", Role.Fast) with { Flying = true },
		U("Yeti_2", Role.Boss) with { Name = "Yeti Chief", Regen = 0.02f },
		// Orc Warband
		U("Orc_Enemy", Role.Swarm),
		U("Orc", Role.Armored),
		U("Tribal", Role.Flyer),
		U("Monkroose", Role.Boss) with { Speed = 1.1f },
		// Shadow Realm
		U("Ghost", Role.Fast) with { Flying = true },
		U("Ghost_Skull", Role.Regen) with { Flying = true },
		U("Ninja", Role.Fast),
		U("Ninja_2", Role.Boss) with { Name = "Ninja Master", Speed = 1.4f },
		// Invasion
		U("Alien_2", Role.Basic) with { Name = "Alien Grunt" },
		U("Wizard", Role.Regen),
		U("Goleling_Evolved", Role.Flyer) with { Armor = 7, Hp = 90 },
		U("Alien", Role.Boss) with { Name = "Alien Overlord", Armor = 6 },
		// Inferno
		U("Demon", Role.Armored),
		U("Demon_2", Role.Flyer) with { Name = "Winged Demon" },
		U("Dragon", Role.Flyer) with { Hp = 80, Armor = 3 },
		U("Blue_Demon", Role.Tank) with { Armor = 6 },
		U("Dragon_Evolved", Role.Boss) with { Name = "Elder Dragon", Flying = true, Lives = 10, Hp = 1400 },
	};

	public static readonly Theme[] Themes =
	{
		new("Slime Meadow", new[] { "green_blob", "pink_slime" }, "green_spiky_blob"),
		new("Mushroom Grove", new[] { "mushnub", "mushnub_evolved", "green_blob" }, "mushroom_king"),
		new("Barnyard", new[] { "cat", "chicken", "pigeon" }, "bunny"),
		new("Sky Pests", new[] { "armabee", "alpaking", "armabee_evolved" }, "alpaking_evolved"),
		new("Cactus Desert", new[] { "cactoro", "birb", "cactoro_2" }, "cactoro_king"),
		new("Reef", new[] { "fish", "glub", "squidle" }, "fish_2"),
		new("Swamp", new[] { "frog", "glub_evolved", "green_spiky_blob" }, "dino"),
		new("Frost Peaks", new[] { "yeti", "hywirl", "goleling" }, "yeti_2"),
		new("Orc Warband", new[] { "orc_enemy", "orc", "tribal" }, "monkroose"),
		new("Shadow Realm", new[] { "ninja", "ghost", "ghost_skull" }, "ninja_2"),
		new("Invasion", new[] { "alien_2", "wizard", "goleling_evolved" }, "alien"),
		new("Inferno", new[] { "demon", "demon_2", "dragon", "blue_demon" }, "dragon_evolved"),
	};

	public const int WavesPerTheme = 5;
	public static readonly int FinalWave = Themes.Length * WavesPerTheme;

	public static readonly SendDef[] Sends =
	{
		new("green_blob", 200, 20, 1, "Z"),
		new("chicken", 350, 30, 1, "X"),
		new("mushnub_evolved", 550, 50, 4, "C"),
		new("armabee", 700, 70, 8, "V"),
		new("frog", 1000, 100, 12, "B"),
		new("squidle", 1400, 140, 18, "N"),
		new("orc", 2000, 200, 24, "M"),
		new("blue_demon", 4500, 450, 30, "K"),
	};

	static readonly Dictionary<string, TowerDef> TowerById = Towers.ToDictionary(t => t.Id);
	static readonly Dictionary<string, UnitDef> UnitById = Units.ToDictionary(u => u.Id);
	static readonly Dictionary<string, SendDef> SendById = Sends.ToDictionary(s => s.UnitId);

	public static TowerDef Tower(string id) => TowerById.GetValueOrDefault(id);
	public static UnitDef Unit(string id) => UnitById.TryGetValue(id, out var u) ? u : throw new ArgumentException($"Unknown unit '{id}'");
	public static SendDef Send(string id) => SendById.GetValueOrDefault(id);

	// ---------------------------------------------------------------- research ("Upgrades" menu)

	// Slow and bounces work at half strength on bosses. Vulnerability works fully: the catapult is the
	// ground boss-killer.
	public const float BossEffect = 0.5f;
	public const float SlowPerLevel = 0.10f, SlowDuration = 1.5f;
	public const float SplitDamage = 0.33f, SplitRange = 1.3f;
	public const float BurnPerLevel = 0.15f, BurnDuration = 3f; // burn dps as a fraction of the hit
	public const float ShrapnelPerLevel = 0.15f;                 // cannon splash radius
	static readonly float[] Vulnerability = { 0f, 0.08f, 0.14f, 0.20f }; // extra damage taken from everything
	public const float VulnerableDuration = 2f;
	static readonly float[] ScatterAirDamage = { 0f, 0.40f, 0.55f, 0.70f }; // catapult damage vs flyers

	public static float VulnerabilityFor(int level) => Vulnerability[level];
	public static float ScatterDamageFor(int level) => ScatterAirDamage[level];
	static readonly float[] WarChestRate = { 0f, 0.04f, 0.07f, 0.10f };

	public static float WarChestInterest(int level) => WarChestRate[level];
	public static int WarChestCap(int wave) => 500 + 250 * wave;

	public static readonly ResearchDef[] Research =
	{
		new("slowing", "Slowing Bolts", "Ballista", new[] { 1000, 2200, 4500 }, new[] { 5, 15, 30 }, null,
			l => $"Ballista hits slow by {SlowPerLevel * l * 100:0}% for {SlowDuration}s. Doesn't stack; bosses half."),
		new("splitting", "Splitting Bolts", "Ballista", new[] { 1200, 2600, 5200 }, new[] { 5, 15, 30 }, null,
			l => $"Bolts bounce to {l} more target{(l > 1 ? "s" : "")} for {SplitDamage * 100:0}% damage. Armour applies; bosses half."),
		new("incendiary", "Incendiary Shells", "Cannon", new[] { 1200, 2600, 5200 }, new[] { 5, 15, 30 }, null,
			l => $"Cannon hits burn for {BurnPerLevel * l * 100:0}% of hit damage per second ({BurnDuration}s). Ignores armour, stops regeneration."),
		new("shrapnel", "Shrapnel", "Cannon", new[] { 1000, 2200, 4500 }, new[] { 5, 15, 30 }, null,
			l => $"Cannon splash radius +{ShrapnelPerLevel * l * 100:0}%. Best against swarms."),
		new("boulders", "Heavy Boulders", "Catapult", new[] { 1200, 2600, 5200 }, new[] { 5, 15, 30 }, null,
			l => $"Creeps hit by a catapult take +{Vulnerability[l] * 100:0}% damage from every tower for {VulnerableDuration}s. Works fully on bosses."),
		new("scatter", "Scatter Shot", "Catapult", new[] { 1500, 3000, 6000 }, new[] { 10, 20, 30 }, null,
			l => $"Catapults can hit flyers for {ScatterAirDamage[l] * 100:0}% damage."),
		new("warchest", "War Chest", "Economy", new[] { 1500, 3500, 7000 }, new[] { 5, 15, 30 }, null,
			l => $"Earn {WarChestRate[l] * 100:0}% interest on banked gold each wave (capped at 500 + 250 × wave)."),
	};

	static readonly Dictionary<string, ResearchDef> ResearchById = Research.ToDictionary(r => r.Id);
	public static ResearchDef ResearchOf(string id) => ResearchById.GetValueOrDefault(id);

	// ---------------------------------------------------------------- economy & waves

	public const int StartGold = 2000;
	public const int StartLives = 20;
	public const float SellRefund = 0.7f;

	// Most gold now comes from kills: base income is 20% of the old 40 + 6/wave, and every creep pays a
	// bounty of HP x 10/9 when killed (sent creeps pay half). Killing everything gives ~the old total gold;
	// leaking costs you the bounty as well as lives.
	public static int BaseIncome(int wave) => 80 + 12 * wave; // 20% of the old income, x10
	public static int Bounty(UnitDef u, bool sent) => Math.Max(10, Mathf.RoundToInt(u.Hp * 10f / 9f * (sent ? SentBountyShare : 1f)));
	public const float SentBountyShare = 0.5f;
	// Paid to both players at the start of the build phase before every boss wave: +100 before the
	// first boss, growing so it stays meaningful (375 before the final dragon).
	public static int BossBonus(int wave) => IsBossWave(wave) ? 750 + 50 * wave : 0;
	public static float HpMultiplier(int wave) => Mathf.Pow(1.095f, wave - 1);
	public static float ArmorMultiplier(int wave) => Mathf.Sqrt(HpMultiplier(wave));

	public static Theme ThemeFor(int wave) => Themes[Math.Clamp((wave - 1) / WavesPerTheme, 0, Themes.Length - 1)];
	public static bool IsBossWave(int wave) => wave % WavesPerTheme == 0;

	// The creeps both lanes get regardless of what players send. Each theme ramps up over its five
	// waves: its first unit, then the first two, then all, then mixed with the previous theme, then the boss.
	public static List<string> BaseWave(int wave)
	{
		int t = Math.Clamp((wave - 1) / WavesPerTheme, 0, Themes.Length - 1);
		int step = (wave - 1) % WavesPerTheme;
		var theme = Themes[t];
		int count = 6 + wave / 2;
		var list = new List<string>();

		if (IsBossWave(wave))
		{
			for (int k = 0; k < count / 2; k++) list.Add(theme.Units[k % theme.Units.Length]);
			int bosses = 1 + wave / 25;
			for (int b = 0; b < bosses; b++) list.Add(theme.Boss);
			return list;
		}

		IEnumerable<string> pool = step switch
		{
			0 => theme.Units.Take(1),
			1 => theme.Units.Take(2),
			2 => theme.Units,
			_ => t > 0 ? theme.Units.Concat(Themes[t - 1].Units) : theme.Units,
		};
		var arr = pool.ToArray();
		for (int k = 0; k < count; k++)
		{
			var id = arr[k % arr.Length];
			list.Add(id);
			if (Unit(id).Role == Role.Swarm) { list.Add(id); list.Add(id); } // swarms come in packs of 3
		}
		return list;
	}
}
