using Godot;

namespace TowerDefense.Game;

// Every player action goes through Match.Submit as one of these, whether it came from the mouse,
// the AI, or (later) the network. Keep them small and serializable.
public abstract record Command;
public sealed record PlaceTower(Vector2I Cell, string TowerId) : Command;
public sealed record SellTower(Vector2I Cell) : Command;
public sealed record UpgradeTower(Vector2I Cell) : Command;
public sealed record SetTargeting(Vector2I Cell, TargetMode Mode) : Command;
public sealed record SendUnit(string UnitId) : Command;
public sealed record BuyResearch(string ResearchId) : Command;
public sealed record ReadyUp : Command;
