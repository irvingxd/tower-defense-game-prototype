using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace TowerDefense.Data;

public sealed record TowerDef(
	string Id, string Name, int Cost,
	float Damage, float Range, float Cooldown, float Splash, bool HitsAir,
	string Shape, char Variant, string Weapon, string Ammo,
	float ProjectileSpeed, float Arc, string TopPiece = null, float ArmorIgnore = 0f,
	float Growth = 1.25f); // damage per normal level (the Crystal starts weak and grows faster)

// A level-5 (later also level-10) choice that changes how a tower works. Tint colours its crystal/weapon.
public sealed record BranchDef(string Id, string TowerId, int Level, string Name, string Summary, Color Tint);

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
		new("crystal", "Crystal Tower", 1300, 20, 2.8f, 0.6f, 0f, true, "round", 'b', "", "", 11f, 0.1f,
			TopPiece: "tower-round-crystals", ArmorIgnore: 0.5f, Growth: 1.35f),
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
		"crystal" => "Late bloomer: +35% damage per level · ignores half of armour · Fire or Frost at level 5",
		"cannon" => $"×{ArmourBonus} vs armoured",
		_ => "All-rounder",
	};

	// Player-facing summary of what each tower is for (Buildings cards, tower card).
	public static (string excels, string weak) Matchups(TowerDef d) => d.Id switch
	{
		"ballista" => ("Flyers, bosses one-on-one, fast creeps", "Armour (small hits), crowds without Splitting"),
		"cannon" => ("Crowds & swarm packs, armoured creeps", "Lone bosses, spread-out fast creeps"),
		"catapult" => ("Bosses & tanks, long range", "Flyers (until Scatter Shot), fast creeps"),
		"crystal" => ("Fire: regen & armour (burn). Frost: fast creeps & flyers (slow)", "Early waves — starts weak, grows faster than any other tower"),
		_ => ("", ""),
	};

	// ---------------------------------------------------------------- wave tags (Intel)

	[Flags]
	public enum WaveTag { None = 0, Boss = 1, Air = 2, Armoured = 4, Tanks = 8, Swarm = 16, Regen = 32, Fast = 64, Splits = 128 }

	public static readonly WaveTag[] AllTags =
		{ WaveTag.Boss, WaveTag.Air, WaveTag.Armoured, WaveTag.Tanks, WaveTag.Swarm, WaveTag.Regen, WaveTag.Fast, WaveTag.Splits };

	public static WaveTag TagsOf(UnitDef u)
	{
		var t = WaveTag.None;
		if (u.Role == Role.Boss) t |= WaveTag.Boss;
		if (u.Flying) t |= WaveTag.Air;
		if (IsArmoured(u)) t |= WaveTag.Armoured;
		if (u.Role == Role.Tank) t |= WaveTag.Tanks;
		if (u.Role == Role.Swarm) t |= WaveTag.Swarm;
		if (u.Regen > 0) t |= WaveTag.Regen;
		if (u.Speed >= 1.4f) t |= WaveTag.Fast;
		if (u.SplitInto != null) t |= WaveTag.Splits;
		return t;
	}

	// A wave shows a tag when that kind makes up a real part of it (any boss/splitter/regen; a quarter for the rest).
	public static WaveTag TagsForWave(int wave)
	{
		var units = BaseWave(wave).Select(Unit).ToList();
		var tags = WaveTag.None;
		foreach (var tag in AllTags)
		{
			int n = units.Count(u => (TagsOf(u) & tag) != 0);
			bool any = tag is WaveTag.Boss or WaveTag.Splits or WaveTag.Regen or WaveTag.Swarm;
			if (n > 0 && (any || n * 4 >= units.Count)) tags |= tag;
		}
		return tags;
	}

	public static (string label, string counter) TagInfo(WaveTag tag) => tag switch
	{
		WaveTag.Boss => ("BOSS", "Catapult (×1.75 vs bosses), Heavy Boulders; Ballista one-on-one"),
		WaveTag.Air => ("AIR", "Ballista; Cannon splash; Catapult only with Scatter Shot"),
		WaveTag.Armoured => ("ARMOURED", "Cannon (×1.5 vs armoured) or big single hits — Ballista's small hits lose the most"),
		WaveTag.Tanks => ("TANKS", "Catapult (×1.75 vs tanks)"),
		WaveTag.Swarm => ("SWARM", "Cannon splash, Shrapnel — packs of three arrive bunched"),
		WaveTag.Regen => ("REGEN", "Burst them down, or Incendiary Shells (burning stops regeneration)"),
		WaveTag.Fast => ("FAST", "Ballista + Slowing Bolts; place towers early on the path"),
		WaveTag.Splits => ("SPLITS", "Splash (Cannon/Catapult) catches the pieces"),
		_ => ("", ""),
	};

	// Normal levels: +25% damage (the tower's Growth) for 30% of what the tower cost so far.
	// Milestones pay more than they cost: 4 -> 5 doubles damage for +70% of the investment, and
	// 9 -> 10 triples it for +160%. So damage per gold climbs at each milestone, and saving up for
	// level 10 beats spreading the same gold over new towers.
	static float DamageStep(TowerDef d, int toLevel) => toLevel == 5 ? 2f : toLevel == 10 ? 3f : d.Growth;
	static float CostStep(int toLevel) => toLevel == 5 ? 0.7f : toLevel == 10 ? 1.6f : 0.3f;

	public static float Damage(TowerDef d, int level)
	{
		float m = 1f;
		for (int l = 2; l <= level; l++) m *= DamageStep(d, l);
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

	public const float FireBurn = 0.6f;   // burn dps as a fraction of the hit
	public const float FrostSlow = 0.35f;
	public const float FrostSlowDuration = 2f;
	public const float AttunementPerLevel = 0.2f; // element strength

	public static readonly BranchDef[] Branches =
	{
		new("fire", "crystal", 5, "Fire", $"Hits burn for {FireBurn * 100:0}% of the hit per second (3 s). Burning stops regeneration and ignores armour.",
			new Color(1.35f, 0.55f, 0.35f)),
		new("frost", "crystal", 5, "Frost", $"Hits slow by {FrostSlow * 100:0}% for {FrostSlowDuration} s (bosses half).",
			new Color(0.45f, 0.8f, 1.45f)),
	};

	static readonly Dictionary<string, BranchDef> BranchById = Branches.ToDictionary(b => b.Id);
	public static BranchDef Branch(string id) => id == null ? null : BranchById.GetValueOrDefault(id);
	// Choices offered when upgrading this tower TO the given level (empty = a plain upgrade).
	public static BranchDef[] BranchesFor(TowerDef d, int toLevel) => Branches.Where(b => b.TowerId == d.Id && b.Level == toLevel).ToArray();
	public static float MilestoneDamage(int toLevel) => toLevel == 10 ? 3f : 2f;
	public static float Range(TowerDef d, int level) =>
		d.Range + 0.08f * (level - 1) + (level >= 5 ? 0.3f : 0f) + (level >= 10 ? 0.3f : 0f);
	public static float Cooldown(TowerDef d, int level) => d.Cooldown * Mathf.Pow(0.96f, level - 1);
	public static float Dps(TowerDef d, int level) => Damage(d, level) / Cooldown(d, level);
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
	public const float HeavyShellsPerLevel = 0.25f;              // cannon armour bonus
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
		new("shrapnel", "Shrapnel", "Cannon", new[] { 1000, 2200, 4500 }, new[] { 5, 15, 30 }, null,
			l => $"Cannon splash radius +{ShrapnelPerLevel * l * 100:0}%. Best against swarms."),
		new("heavyshells", "Heavy Shells", "Cannon", new[] { 1200, 2600, 5200 }, new[] { 5, 15, 30 }, null,
			l => $"Cannon's bonus vs armoured creeps rises to ×{ArmourBonus + HeavyShellsPerLevel * l:0.00}."),
		new("boulders", "Heavy Boulders", "Catapult", new[] { 1200, 2600, 5200 }, new[] { 5, 15, 30 }, null,
			l => $"Creeps hit by a catapult take +{Vulnerability[l] * 100:0}% damage from every tower for {VulnerableDuration}s. Works fully on bosses."),
		new("scatter", "Scatter Shot", "Catapult", new[] { 1500, 3000, 6000 }, new[] { 10, 20, 30 }, null,
			l => $"Catapults can hit flyers for {ScatterAirDamage[l] * 100:0}% damage."),
		new("incendiary", "Incendiary", "Crystal", new[] { 1200, 2600, 5200 }, new[] { 5, 15, 30 }, null,
			l => $"Crystal hits burn for +{BurnPerLevel * l * 100:0}% of the hit per second ({BurnDuration}s), on top of Fire. Ignores armour, stops regeneration."),
		new("attunement", "Attunement", "Crystal", new[] { 1000, 2200, 4500 }, new[] { 5, 15, 30 }, null,
			l => $"Element strength +{AttunementPerLevel * l * 100:0}%: Fire burns hotter, Frost slows harder."),
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
