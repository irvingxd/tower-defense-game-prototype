using System.Collections.Generic;
using Godot;
using TowerDefense.Data;

namespace TowerDefense.Game;

public sealed class PlayerState
{
	public int Index;
	public string Name;
	public int Gold;
	public int IncomeBonus;
	public int Lives;
	public bool Ready;
	public int LeakedLastWave;
	public int LastInterest; // War Chest payout at the start of this build phase
	public int LastBossBonus; // pre-boss bonus paid at the start of this build phase
	// Units bought this round; they join the opponent's next wave.
	public readonly List<string> PendingSends = new();
	// Upgrades-menu research levels by id.
	public readonly Dictionary<string, int> Research = new();
	public readonly PlayerStats Stats = new();

	public int Level(string researchId) => Research.GetValueOrDefault(researchId);
}

// One tower's lifetime record. Outlives the tower node, so sold towers still show on the stats screen.
public sealed class TowerRecord
{
	public TowerDef Def;
	public Vector2I Cell;
	public int Level = 1;
	public float Damage;
	public int Kills;
	public bool Sold;
}

// Everything the end-of-match screen reports.
public sealed class PlayerStats
{
	public int IncomeEarned, InterestEarned, BountyEarned, BossBonusEarned, SellRefunds;
	public int SpentTowers, SpentUpgrades, SpentResearch, SpentSends;
	public int UnitsSent;
	public readonly List<TowerRecord> Towers = new();
	public readonly Dictionary<string, (int count, int lives)> Leaks = new();

	public void AddLeak(string unitId, int lives)
	{
		var (c, l) = Leaks.GetValueOrDefault(unitId);
		Leaks[unitId] = (c + 1, l + lives);
	}
}
