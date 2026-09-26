using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using TowerDefense.Data;

namespace TowerDefense.Game;

// Owns the rules: phases, economy, waves, win/lose. All player input arrives through Submit(),
// so the local player, the AI and a future network peer are indistinguishable here.
public partial class Match : Node3D
{
	public enum Phase { Build, Wave, Over }

	public const float LaneSpacing = Lane.W + 3;
	const float FirstBuildTime = 25f, BuildTime = 15f;

	public readonly PlayerState[] Players = new PlayerState[2];
	public readonly Lane[] Lanes = new Lane[2];
	public int Wave { get; private set; }
	public int FirstWave = 1; // dev: benchmarks jump straight to a later wave
	public Phase State { get; private set; } = Phase.Build;
	public float BuildTimer { get; private set; }
	public float BuildDuration { get; private set; } = FirstBuildTime;
	public int Winner { get; private set; } = -1;

	public event Action BuildStarted;   // income paid, new wave number set
	public event Action WaveStarting;   // build phase over, creeps about to be queued
	public event Action<int> WaveEnded; // wave number
	public event Action<int, Enemy> Leaked; // lane, creep
	public event Action MatchOver;

	public override void _Ready()
	{
		for (int i = 0; i < 2; i++)
		{
			Players[i] = new PlayerState { Index = i, Name = i == 0 ? "You" : "AI", Gold = Catalog.StartGold, Lives = Catalog.StartLives };
			var lane = new Lane { Index = i, Match = this, Name = $"Lane{i}", Position = new Vector3(i * LaneSpacing, 0, 0) };
			Lanes[i] = lane;
			AddChild(lane);
		}
		CallDeferred(MethodName.StartBuild);
	}

	public bool Submit(int player, Command cmd)
	{
		if (State == Phase.Over) return false;
		var me = Players[player];
		var lane = Lanes[player];
		switch (cmd)
		{
			case PlaceTower pt:
			{
				var def = Catalog.Tower(pt.TowerId);
				if (def == null || me.Gold < def.Cost || !lane.IsBuildable(pt.Cell)) return false;
				me.Gold -= def.Cost;
				var built = lane.AddTower(pt.Cell, def);
				built.Record.Def = def;
				built.Record.Cell = pt.Cell;
				me.Stats.Towers.Add(built.Record);
				me.Stats.SpentTowers += def.Cost;
				return true;
			}
			case SellTower st:
			{
				if (!lane.Towers.TryGetValue(st.Cell, out var tower)) return false;
				int refund = (int)(tower.Invested * Catalog.SellRefund);
				me.Gold += refund;
				me.Stats.SellRefunds += refund;
				tower.Record.Sold = true;
				FloatingText.Spawn(lane, Lane.CellCenter(st.Cell, 1.2f), $"+{refund}", FloatingText.GoldColor, 40);
				lane.RemoveTower(st.Cell);
				return true;
			}
			case UpgradeTower ut:
			{
				if (!lane.Towers.TryGetValue(ut.Cell, out var tower) || tower.MaxLevel || me.Gold < tower.UpgradeCost) return false;
				var choices = Catalog.BranchesFor(tower.Def, tower.Level + 1);
				if (choices.Length > 0 && !choices.Any(b => b.Id == ut.Branch)) return false;
				me.Stats.SpentUpgrades += tower.UpgradeCost;
				me.Gold -= tower.UpgradeCost;
				tower.Upgrade(choices.Length > 0 ? ut.Branch : null);
				return true;
			}
			case SetTargeting stg:
			{
				if (!lane.Towers.TryGetValue(stg.Cell, out var tower)) return false;
				tower.Targeting = stg.Mode;
				return true;
			}
			case SendUnit su:
			{
				var send = Catalog.Send(su.UnitId);
				if (send == null || Wave < send.UnlockWave || me.Gold < send.Cost) return false;
				me.Gold -= send.Cost;
				me.Stats.SpentSends += send.Cost;
				me.Stats.UnitsSent++;
				me.IncomeBonus += send.Income;
				me.PendingSends.Add(send.UnitId);
				return true;
			}
			case BuyResearch br:
			{
				var def = Catalog.ResearchOf(br.ResearchId);
				if (def == null || ResearchBlocker(me, def) != null) return false;
				me.Gold -= def.Costs[me.Level(def.Id)];
				me.Stats.SpentResearch += def.Costs[me.Level(def.Id)];
				me.Research[def.Id] = me.Level(def.Id) + 1;
				return true;
			}
			case ReadyUp:
				me.Ready = true;
				return true;
		}
		return false;
	}

	void StartBuild()
	{
		if (Wave == 0) Wave = FirstWave - 1;
		Wave++;
		State = Phase.Build;
		BuildTimer = BuildDuration = Wave == 1 ? FirstBuildTime : BuildTime;
		foreach (var p in Players)
		{
			if (Wave > 1)
			{
				p.LastInterest = Interest(p); // on what was banked, before this wave's income
				p.Gold += p.LastInterest + Income(p);
				p.Stats.InterestEarned += p.LastInterest;
				p.Stats.IncomeEarned += Income(p);
			}
			p.LastBossBonus = Catalog.BossBonus(Wave);
			if (p.LastBossBonus > 0)
			{
				p.Gold += p.LastBossBonus;
				p.Stats.BossBonusEarned += p.LastBossBonus;
			}
			p.Ready = false;
		}
		BuildStarted?.Invoke();
	}

	public int Income(PlayerState p) => Catalog.BaseIncome(Wave) + p.IncomeBonus;

	public int Interest(PlayerState p) =>
		Math.Min(Catalog.WarChestCap(Wave), (int)(p.Gold * Catalog.WarChestInterest(p.Level("warchest"))));

	// Why a research level can't be bought right now, or null if it can.
	public string ResearchBlocker(PlayerState p, ResearchDef def)
	{
		int level = p.Level(def.Id);
		if (level >= def.MaxLevel) return "Maxed";
		if (def.ExclusiveWith != null && p.Level(def.ExclusiveWith) > 0)
			return $"Locked by {Catalog.ResearchOf(def.ExclusiveWith).Name}";
		if (Wave < def.UnlockWaves[level]) return $"Unlocks wave {def.UnlockWaves[level]}";
		if (p.Gold < def.Costs[level]) return "Not enough gold";
		return null;
	}

	void StartWave()
	{
		WaveStarting?.Invoke();
		if (State == Phase.Over) return;
		State = Phase.Wave;
		for (int i = 0; i < 2; i++)
		{
			var sender = Players[1 - i];
			Lanes[i].QueueWave(Interleave(Catalog.BaseWave(Wave), sender.PendingSends), Catalog.HpMultiplier(Wave), Catalog.ArmorMultiplier(Wave));
			sender.PendingSends.Clear();
			Players[i].LeakedLastWave = 0;
		}
	}

	// Spread sent units through the base wave instead of tacking them on the end. Sent ones are flagged
	// because they pay reduced bounty.
	static List<(string, bool)> Interleave(List<string> wave, List<string> sends)
	{
		var result = wave.Select(u => (u, false)).ToList();
		for (int k = 0; k < sends.Count; k++)
			result.Insert(Math.Min(result.Count, 1 + k * 2), (sends[k], true));
		return result;
	}

	// Bounty from the base wave if every creep dies (splits included) — what a lane "should" earn.
	public int ExpectedBounty(int wave) => Catalog.BaseWave(wave).Sum(u =>
	{
		var def = Catalog.Unit(u);
		return Catalog.Bounty(def, false) + (def.SplitInto != null ? def.SplitCount * Catalog.Bounty(Catalog.Unit(def.SplitInto), false) : 0);
	});

	public event Action<int, Enemy, int> Killed; // lane, creep, gold paid

	public void OnKill(int laneIndex, Enemy e)
	{
		if (State == Phase.Over) return;
		var p = Players[laneIndex];
		int gold = Catalog.Bounty(e.Def, e.Sent);
		p.Gold += gold;
		p.Stats.BountyEarned += gold;
		Killed?.Invoke(laneIndex, e, gold);
	}

	public IEnumerable<UnitDef> IncomingUnits(int player) =>
		Catalog.BaseWave(Wave).Concat(Players[1 - player].PendingSends).Select(Catalog.Unit);

	public float IncomingHp(int player) =>
		Catalog.BaseWave(Wave).Concat(Players[1 - player].PendingSends).Sum(u => EffectiveHp(Catalog.Unit(u)));

	// HP adjusted for armour and splitting, as a rough measure of how much damage a creep soaks.
	public float EffectiveHp(UnitDef u)
	{
		float hp = u.Hp * Catalog.HpMultiplier(Wave) * (1f + 0.08f * u.Armor) * (1f + u.Regen * 5f);
		if (u.SplitInto != null) hp += u.SplitCount * EffectiveHp(Catalog.Unit(u.SplitInto));
		return hp;
	}

	// Toughest single creep coming at this player next wave (bosses need focused damage, not just total).
	public float BiggestIncomingHp(int player)
	{
		var units = Catalog.BaseWave(Wave).Concat(Players[1 - player].PendingSends);
		return units.Max(u => EffectiveHp(Catalog.Unit(u)));
	}

	public void OnLeak(int laneIndex, Enemy e)
	{
		int lives = e.Def.Lives;
		Leaked?.Invoke(laneIndex, e);
		var p = Players[laneIndex];
		p.Stats.AddLeak(e.Def.Id, lives);
		p.Lives = Math.Max(0, p.Lives - lives);
		p.LeakedLastWave += lives;
		if (Players.Any(pl => pl.Lives <= 0)) EndMatch();
	}

	void EndMatch()
	{
		if (State == Phase.Over) return;
		State = Phase.Over;
		// Someone ran out of lives, or both survived the final wave: most lives wins.
		int l0 = Players[0].Lives, l1 = Players[1].Lives;
		Winner = l0 == l1 ? -1 : l0 > l1 ? 0 : 1;
		foreach (var lane in Lanes) lane.ProcessMode = ProcessModeEnum.Disabled;
		MatchOver?.Invoke();
	}

	public override void _Process(double delta)
	{
		switch (State)
		{
			case Phase.Build:
				BuildTimer -= (float)delta;
				if (BuildTimer <= 0 || Players.All(p => p.Ready)) StartWave();
				break;
			case Phase.Wave:
				if (Lanes.Any(l => l.Busy)) break;
				WaveEnded?.Invoke(Wave);
				if (Wave >= Catalog.FinalWave) { EndMatch(); break; }
				StartBuild();
				break;
		}
	}
}
