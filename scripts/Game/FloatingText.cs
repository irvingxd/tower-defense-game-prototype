using Godot;

namespace TowerDefense.Game;

// Short-lived world-space text (damage numbers, gold refunds) that rises and fades.
public static class FloatingText
{
	public static bool Enabled = true;

	public static readonly Color DamageColor = new(1f, 1f, 1f);
	public static readonly Color BounceColor = new(0.75f, 0.85f, 1f);
	public static readonly Color BurnColor = new(1f, 0.6f, 0.2f);
	public static readonly Color GoldColor = new(1f, 0.85f, 0.3f);

	public static void Spawn(Node3D parent, Vector3 localPos, string text, Color color, int size = 34)
	{
		if (!Enabled || !GodotObject.IsInstanceValid(parent) || !parent.IsInsideTree()) return;
		var label = new Label3D
		{
			Text = text, FontSize = size, PixelSize = 0.01f, Modulate = color,
			OutlineSize = 8, OutlineModulate = new Color(0, 0, 0, 0.8f),
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, NoDepthTest = true, RenderPriority = 3,
			Position = localPos + new Vector3((float)GD.RandRange(-0.12, 0.12), 0, 0),
		};
		parent.AddChild(label);
		var tw = label.CreateTween();
		tw.SetParallel();
		tw.TweenProperty(label, "position:y", label.Position.Y + 0.6f, 0.8f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
		tw.TweenProperty(label, "modulate:a", 0f, 0.8f).SetDelay(0.3f);
		tw.Chain().TweenCallback(Callable.From(label.QueueFree));
	}
}
