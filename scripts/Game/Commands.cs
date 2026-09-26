using Godot;

namespace TowerDefense.Game;

// Every player action goes through Match.Submit as one of these, whether it came from the mouse,
// the AI, or (later) the network. Keep them small and serializable.
public abstract record Command;
public sealed record PlaceTower(Vector2I Cell, string TowerId) : Command;
public sealed record SellTower(Vector2I Cell) : Command;
// Branch is required when the next level offers a choice (see Catalog.BranchesFor), ignored otherwise.
public sealed record UpgradeTower(Vector2I Cell, string Branch = null) : Command;
public sealed record SetTargeting(Vector2I Cell, TargetMode Mode) : Command;
public sealed record SendUnit(string UnitId) : Command;
public sealed record BuyResearch(string ResearchId) : Command;
public sealed record ReadyUp : Command;
