using System.Collections.Generic;
using System.Linq;
using Godot;
using TowerDefense.Data;

namespace TowerDefense.Game;

// Decides once per build phase: first make sure the lane can hold the incoming wave, buying whichever
// new tower or upgrade closes the most of the shortfall per gold, then invest a personality-dependent
// share of the rest into sends, which raise income.
public partial class AiController : Node
{
	public enum Personality { Turtle, Balanced, Greedy, Hoarder }

	public Match Match;
	public int Player;
	public Personality Style;

	readonly RandomNumberGenerator _rng = new();

	float Safety => Style switch { Personality.Turtle => 1.6f, Personality.Greedy => 1.05f, Personality.Hoarder => 1.15f, _ => 1.3f };
	float Greed => Style switch { Personality.Turtle => 0.25f, Personality.Greedy => 0.7f, Personality.Hoarder => 0.2f, _ => 0.45f };
	// How many waves of income each personality likes to keep in the bank once it owns a War Chest.
	float BankWaves => Style switch { Personality.Hoarder => 3f, Personality.Balanced => 1f, Personality.Greedy => 0.7f, _ => 0.4f };

	// Gold the AI wants to keep banked: a few waves of income, but never more than the War Chest cap can use.
	int Reserve()
	{
		var me = Match.Players[Player];
		float rate = Catalog.WarChestInterest(me.Level("warchest"));
		if (rate <= 0) return Style == Personality.Hoarder ? Catalog.ResearchOf("warchest").Costs[0] : 0; // saving up for it
		float useful = Catalog.WarChestCap(Match.Wave + 1) / rate;
		return (int)Mathf.Min(useful, (Match.Income(me) + Match.ExpectedBounty(Match.Wave)) * BankWaves);
	}

	float _safety; // Shortfall's stream safety factor for the current spending pass
	int _savingFor; // gold set aside this build phase for a milestone upgrade (kept out of sends)

	public override void _Ready()
	{
		_rng.Randomize();
		Match.BuildStarted += OnBuildStarted;
		Match.WaveStarting += Defend; // re-check: the opponent may have sent more since we decided
	}

	public override void _ExitTree()
	{
		Match.BuildStarted -= OnBuildStarted;
		Match.WaveStarting -= Defend;
	}

	void OnBuildStarted()
	{
		Defend();
		Invest();
		Match.Submit(Player, new ReadyUp());
	}

	// ------------------------------------------------------------------ threat model

	// What the lane must handle: the whole stream, and its single toughest creep passing every tower.
	sealed class Threat
	{
		public float StreamHp, StreamSpeed, StreamArmor, AirShare, SwarmShare, ArmouredShare, SiegeShare, RegenShare;
		public float BossHp, BossSpeed, BossArmor;
		public bool BossFlying;
	}

	// Look-ahead: the wave's HP and bosses decide HOW MUCH to buy, but WHAT to buy is judged on the next
	// three waves' make-up, so a cannon goes up before the Orc swarms and a catapult before the Swamp
	// tanks instead of a tower that is only right for one wave. A boss within two waves is prepared for.
	static readonly float[] LookAheadWeights = { 1f, 0.6f, 0.35f };

	Threat Assess()
	{
		var t = AssessWave(Match.Wave, Match.IncomingUnits(Player).ToList());
		float air = 0, armor = 0, speed = 0, swarm = 0, armoured = 0, siege = 0, regen = 0, weights = 0;
		for (int i = 0; i < LookAheadWeights.Length; i++)
		{
			int w = Match.Wave + i;
			if (w > Catalog.FinalWave) break;
			var profile = i == 0 ? t : AssessWave(w, Catalog.BaseWave(w).Select(Catalog.Unit).ToList());
			float k = LookAheadWeights[i];
			air += profile.AirShare * k;
			armor += profile.StreamArmor * k;
			speed += profile.StreamSpeed * k;
			swarm += profile.SwarmShare * k;
			armoured += profile.ArmouredShare * k;
			siege += profile.SiegeShare * k;
			regen += profile.RegenShare * k;
			weights += k;
			if (i > 0 && i <= 2 && Catalog.IsBossWave(w))
			{
				// Start preparing: the coming boss counts at 60% of its full requirement.
				if (profile.BossHp * 0.6f > t.BossHp)
				{
					t.BossHp = profile.BossHp * 0.6f;
					t.BossSpeed = profile.BossSpeed;
					t.BossArmor = profile.BossArmor;
					t.BossFlying = profile.BossFlying;
				}
			}
		}
		t.AirShare = air / weights;
		t.StreamArmor = armor / weights;
		t.StreamSpeed = speed / weights;
		t.SwarmShare = swarm / weights;
		t.ArmouredShare = armoured / weights;
		t.SiegeShare = siege / weights;
		t.RegenShare = regen / weights;
		return t;
	}

	Threat AssessWave(int wave, List<UnitDef> units)
	{
		float hpMul = Catalog.HpMultiplier(wave), armorMul = Catalog.ArmorMultiplier(wave);
		var t = new Threat();
		float hpSum = 0;
		UnitDef boss = null;
		foreach (var u in units)
		{
			float hp = Match.EffectiveHp(u);
			hpSum += hp;
			t.StreamSpeed += u.Speed * hp;
			t.StreamArmor += u.Armor * armorMul * hp;
			if (u.Flying) t.AirShare += hp;
			if (u.Role == Role.Swarm) t.SwarmShare += 1;
			if (Catalog.IsArmoured(u)) t.ArmouredShare += hp;
			if (Catalog.IsSiegeTarget(u)) t.SiegeShare += hp;
			if (u.Regen > 0) t.RegenShare += hp;
			if (boss == null || hp > Match.EffectiveHp(boss)) boss = u;
		}
		t.StreamHp = hpSum;
		t.StreamSpeed /= hpSum;
		t.StreamArmor /= hpSum;
		t.AirShare /= hpSum;
		t.SwarmShare /= units.Count;
		t.ArmouredShare /= hpSum;
		t.SiegeShare /= hpSum;
		t.RegenShare /= hpSum;
		// Bosses arrive back to back, so the lane has to chew through all of them in one pass.
		var bosses = units.Where(u => u.Role == Role.Boss).ToList();
		if (bosses.Count == 0) bosses.Add(boss);
		t.BossHp = bosses.Sum(b => b.Hp * hpMul * (1 + b.Regen * 5f));
		t.BossSpeed = bosses.Max(b => b.Speed);
		t.BossArmor = bosses.Max(b => b.Armor) * armorMul;
		t.BossFlying = bosses.Any(b => b.Flying);
		return t;
	}

	static float Dps(TowerDef d, int level, float armor)
	{
		float dmg = Catalog.Damage(d, level);
		return Mathf.Max(dmg - armor, dmg * 0.2f) / Catalog.Cooldown(d, level);
	}

	// Research levels as the threat model sees them; `hypo` previews buying one more level.
	int ResearchLevel(string id, (string id, int level)? hypo) =>
		hypo is { } h && h.id == id ? h.level : Match.Players[Player].Level(id);

	// Fraction of what the lane still lacks, 0 when it can hold.
	float Shortfall(Lane lane, Threat t)
	{
		var (stream, boss) = Capacity(lane, t);
		return Mathf.Max(0, 1 - stream / NeedStream(t)) + Mathf.Max(0, 1 - boss / NeedBoss(t));
	}

	float NeedStream(Threat t) => t.StreamHp * _safety;
	static float NeedBoss(Threat t) => t.BossHp * 1.3f;

	// How much of the need a purchase adds, NOT capped at "enough". Capping made every small gap look
	// best filled by the cheapest item (a 1k ballista or a 300g upgrade step), so the AI never built
	// cannons/catapults and never took the level-10 jump. Gaps still open weigh fully; closed ones a little.
	float Gain(Threat t, (float stream, float boss) before, (float stream, float boss) after)
	{
		float needS = NeedStream(t), needB = NeedBoss(t);
		float wS = before.stream < needS ? 1f : 0.15f, wB = before.boss < needB ? 1f : 0.15f;
		return wS * (after.stream - before.stream) / needS + wB * (after.boss - before.boss) / needB;
	}

	// Damage the lane can put into the stream and into the bosses. `hypo` replaces/adds one tower,
	// `research` previews one research level. Mirrors the real effects closely enough to rank buys.
	(float stream, float boss) Capacity(Lane lane, Threat t, (TowerDef def, int level, Vector2I cell, string branch)? hypo = null, (string id, int level)? research = null)
	{
		float slow = Catalog.SlowPerLevel * ResearchLevel("slowing", research);
		int bounces = ResearchLevel("splitting", research);
		float incendiary = Catalog.BurnPerLevel * ResearchLevel("incendiary", research);
		float attune = 1 + Catalog.AttunementPerLevel * ResearchLevel("attunement", research);
		float heavy = Catalog.HeavyShellsPerLevel * ResearchLevel("heavyshells", research);
		float shrapnel = Catalog.ShrapnelPerLevel * ResearchLevel("shrapnel", research);
		float vuln = Catalog.VulnerabilityFor(ResearchLevel("boulders", research));
		float scatter = Catalog.ScatterDamageFor(ResearchLevel("scatter", research));

		var layout = Layout(lane, hypo).ToList();
		float Cover(System.Func<(TowerDef def, int level, Vector2I cell, string branch), bool> which) =>
			lane.PathCells.Count(p => layout.Any(x => which(x) && (p - x.cell).LengthSquared() <= Catalog.Range(x.def, x.level) * Catalog.Range(x.def, x.level)))
			/ (float)lane.PathCells.Count;

		// Slows only matter on the stretch of path their towers cover: Slowing Bolts on ballistas, Frost crystals.
		float frostSlow = Mathf.Min(0.6f, Catalog.FrostSlow * attune);
		float slowFrac = (slow > 0 ? slow * Cover(x => x.def.Id == "ballista") : 0f)
			+ frostSlow * Cover(x => x.branch == "frost");
		slowFrac = Mathf.Min(slowFrac, 0.6f);
		float streamSpeed = t.StreamSpeed * (1 - slowFrac);
		float bossSpeed = t.BossSpeed * (1 - slowFrac * Catalog.BossEffect);
		// Heavy Boulders: everything hit by a catapult takes more damage for a while — scale by how much
		// of the path catapults cover (the debuff is up about 70% of the time there).
		float amp = 1 + (vuln <= 0 ? 0 : vuln * Cover(x => x.def.Id == "catapult") * 0.7f);
		// Splash is worth more against dense swarms (the benchmark: cannon best on Orc Warband).
		float splashTargets = 1.5f + 1.2f * t.SwarmShare;

		float stream = 0, boss = 0;
		foreach (var (def, level, cell, branch) in layout)
		{
			float cells = lane.PathCellsInRange(cell, Catalog.Range(def, level));
			bool ballista = def.Id == "ballista", cannon = def.Id == "cannon", catapult = def.Id == "catapult", crystal = def.Id == "crystal";
			float airDamage = def.HitsAir ? 1f : catapult ? scatter : 0f;
			float airFactor = 1f - t.AirShare * (1f - airDamage);
			// Role bonus on the share of the stream it applies to; bosses always count as siege targets.
			float roleStream = cannon ? 1 + (Catalog.ArmourBonus + heavy - 1) * t.ArmouredShare
				: catapult ? 1 + (Catalog.SiegeBonus - 1) * t.SiegeShare : 1f;
			float roleBoss = catapult ? Catalog.SiegeBonus : 1f;
			float pierce = 1f - def.ArmorIgnore;
			float streamDps = Dps(def, level, t.StreamArmor * pierce) * amp * roleStream;
			float bossDps = Dps(def, level, t.BossArmor * pierce) * amp * roleBoss;
			// A refreshing burn is roughly burn x hit damage per second on what it hits (ignores armour).
			float burn = crystal ? incendiary + (branch == "fire" ? Catalog.FireBurn * attune : 0f) : 0f;
			if (burn > 0)
			{
				streamDps += Catalog.Damage(def, level) * burn * (1 + t.RegenShare);
				bossDps += Catalog.Damage(def, level) * burn;
			}
			float spread = def.Splash > 0 ? splashTargets * (cannon ? 1 + shrapnel : 1f) : 1f;
			if (ballista) spread *= 1 + Catalog.SplitDamage * bounces * 0.6f;
			stream += streamDps * cells / streamSpeed * spread * airFactor;
			boss += bossDps * cells / bossSpeed * (t.BossFlying ? airDamage : 1f);
		}
		return (stream, boss);
	}

	static IEnumerable<(TowerDef def, int level, Vector2I cell, string branch)> Layout(Lane lane, (TowerDef def, int level, Vector2I cell, string branch)? hypo)
	{
		foreach (var tw in lane.Towers.Values)
			if (hypo == null || tw.Cell != hypo.Value.cell)
				yield return (tw.Def, tw.Level, tw.Cell, tw.Branch);
		if (hypo != null) yield return hypo.Value;
	}

	// ------------------------------------------------------------------ spending

	// Two passes: first bare survival (may dip into the bank), then the personality's safety margin,
	// paid only from gold above the reserve so hoarders actually keep a bank earning interest.
	void Defend()
	{
		var lane = Match.Lanes[Player];
		var threat = Assess();
		// Hoarders never dip below 60% of their bank, even to stop leaks: interest now vs lives now.
		_savingFor = 0;
		SpendPass(lane, threat, 1f, Style == Personality.Hoarder ? (int)(Reserve() * 0.6f) : 0, canSave: false);
		// The safety margin can wait for a milestone: survival is already covered by the first pass.
		SpendPass(lane, threat, Safety, Reserve(), canSave: true);
	}

	void SpendPass(Lane lane, Threat threat, float safety, int reserve, bool canSave)
	{
		_safety = safety;
		for (int guard = 0; guard < 40; guard++)
		{
			float now = Shortfall(lane, threat);
			if (now <= 0 || !SpendOnce(lane, threat, now, Match.Players[Player].Gold - reserve, canSave)) break;
		}
		_safety = Safety;
	}

	bool SpendOnce(Lane lane, Threat threat, float now, int gold, bool canSave)
	{
		if (gold <= 0) return false;
		var before = Capacity(lane, threat);
		Command best = null;
		float bestValue = 0;

		foreach (var def in Catalog.Towers)
		{
			if (def.Cost > gold) continue;
			var cell = BestCell(lane, Catalog.Range(def, 1));
			if (cell == null) continue;
			// Value a new tower partly by what it becomes: the same gold taken to level 5 (best branch).
			// Without this a late bloomer (the Crystal) never looks worth building.
			float nowValue = Gain(threat, before, Capacity(lane, threat, (def, 1, cell.Value, null))) / def.Cost;
			float value = Blend(nowValue, Projected(lane, threat, before, def, 0, cell.Value, null)) * (0.9f + _rng.Randf() * 0.2f);
			if (value > bestValue) { bestValue = value; best = new PlaceTower(cell.Value, def.Id); }
		}

		var me = Match.Players[Player];
		foreach (var r in Catalog.Research)
		{
			if (r.Id == "warchest" || Match.ResearchBlocker(me, r) != null) continue;
			int cost = r.Costs[me.Level(r.Id)];
			if (cost > gold) continue;
			float value = Gain(threat, before, Capacity(lane, threat, research: (r.Id, me.Level(r.Id) + 1))) / cost;
			if (value > bestValue) { bestValue = value; best = new BuyResearch(r.Id); }
		}

		// A milestone just out of reach (within about a wave of income) that beats everything affordable:
		// stop here and save for it rather than frittering the gold on weaker buys.
		float income = Match.Income(me) + Match.ExpectedBounty(Match.Wave);
		float saveValue = 0;
		int saveCost = 0;
		foreach (var tw in lane.Towers.Values)
		{
			if (tw.MaxLevel || tw.UpgradeCost <= gold || !Catalog.IsMilestone(tw.Level + 1) || tw.UpgradeCost > gold + income * 1.2f) continue;
			foreach (var option in BranchOptions(tw.Def, tw.Level + 1, tw.Branch))
			{
				float value = Gain(threat, before, Capacity(lane, threat, (tw.Def, tw.Level + 1, tw.Cell, option))) / tw.UpgradeCost;
				if (value > saveValue) { saveValue = value; saveCost = tw.UpgradeCost; }
			}
		}

		foreach (var tw in lane.Towers.Values)
		{
			if (tw.MaxLevel || tw.UpgradeCost > gold) continue;
			// At a milestone choice, weigh each branch against the coming waves (look-ahead) and keep the best.
			var choices = Catalog.BranchesFor(tw.Def, tw.Level + 1);
			var options = choices.Length > 0 ? choices.Select(c => c.Id).ToArray() : new string[] { tw.Branch };
			foreach (var option in options)
			{
				float nowValue = Gain(threat, before, Capacity(lane, threat, (tw.Def, tw.Level + 1, tw.Cell, option))) / tw.UpgradeCost;
				// Same look-ahead as new towers get, so a started tower is carried on to its milestone.
				float value = Catalog.IsMilestone(tw.Level + 1) ? nowValue
					: Blend(nowValue, Projected(lane, threat, before, tw.Def, tw.Level, tw.Cell, tw.Branch));
				if (value > bestValue) { bestValue = value; best = new UpgradeTower(tw.Cell, choices.Length > 0 ? option : null); }
			}
		}

		if (canSave && saveValue > bestValue * 1.15f)
		{
			_savingFor = Mathf.Max(_savingFor, saveCost);
			return false;
		}
		return best != null && Match.Submit(Player, best);
	}

	static float Blend(float now, float projected) => 0.6f * now + 0.4f * Mathf.Max(now, projected);

	// Value per gold of taking a tower from `level` (0 = not built) to its next milestone (5, then 10),
	// with the best branch on the way: what the gold is really buying when you start down that road.
	float Projected(Lane lane, Threat threat, (float stream, float boss) before, TowerDef def, int level, Vector2I cell, string branch)
	{
		int target = level < 5 ? 5 : 10;
		int paid = level == 0 ? 0 : Catalog.TotalCost(def, level);
		float best = 0;
		foreach (var option in level < 5 ? BranchOptions(def, 5) : new[] { branch })
			best = Mathf.Max(best, Gain(threat, before, Capacity(lane, threat, (def, target, cell, option))));
		return best / (Catalog.TotalCost(def, target) - paid);
	}

	// Branch ids to try when a tower reaches this level (null = no choice; keep the current branch).
	static string[] BranchOptions(TowerDef def, int level, string current = null)
	{
		var choices = Catalog.BranchesFor(def, level);
		return choices.Length > 0 ? choices.Select(c => c.Id).ToArray() : new[] { current };
	}

	void Invest()
	{
		var me = Match.Players[Player];
		var opponent = Match.Players[1 - Player];
		// War Chest pays off over many waves: greedy AIs buy it as soon as they can, others once they're
		// comfortably ahead. Whatever isn't spent below stays banked and earns interest.
		var chest = Catalog.ResearchOf("warchest");
		float chestMargin = Style switch { Personality.Hoarder => 1f, Personality.Greedy => 1.5f, Personality.Balanced => 2f, _ => 3f };
		while (Match.ResearchBlocker(me, chest) == null && me.Gold >= chest.Costs[me.Level("warchest")] * chestMargin)
			if (!Match.Submit(Player, new BuyResearch("warchest"))) break;

		float greed = Greed + (opponent.LeakedLastWave > 0 ? 0.15f : 0f);
		int budget = (int)(Mathf.Max(0, me.Gold - Reserve() - _savingFor) * greed);
		while (true)
		{
			var affordable = Catalog.Sends.Where(s => s.UnlockWave <= Match.Wave && s.Cost <= budget).ToList();
			if (affordable.Count == 0) break;
			// Lean towards the priciest units available: same income ratio, fewer creeps to fight.
			var pick = affordable[affordable.Count - 1 - (int)(_rng.Randi() % (uint)Mathf.Min(3, affordable.Count))];
			if (!Match.Submit(Player, new SendUnit(pick.UnitId))) break;
			budget -= pick.Cost;
		}
	}

	Vector2I? BestCell(Lane lane, float range)
	{
		Vector2I? best = null;
		float bestScore = -1;
		for (int r = 0; r < Lane.H; r++)
		for (int c = 0; c < Lane.W; c++)
		{
			var cell = new Vector2I(c, r);
			if (!lane.IsBuildable(cell)) continue;
			float score = lane.PathCellsInRange(cell, range) + _rng.Randf() * 0.5f;
			if (score > bestScore) { bestScore = score; best = cell; }
		}
		return best;
	}
}
