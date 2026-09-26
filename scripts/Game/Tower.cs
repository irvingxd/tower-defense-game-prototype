using Godot;
using TowerDefense.Data;

namespace TowerDefense.Game;

// Which creep in range a tower shoots.
public enum TargetMode { First, Last, Strongest, Weakest }

public partial class Tower : Node3D
{
	static readonly char[] MiddleVariants = { 'a', 'b', 'c' };

	public TowerDef Def;
	public Lane Lane;
	public Vector2I Cell;
	public int Level { get; private set; } = 1;
	public int Invested; // total gold spent, for sell refunds
	public TargetMode Targeting = TargetMode.First;
	public TowerRecord Record = new(); // damage/kills for the stats screen
	public string Branch { get; private set; } // chosen at a milestone (e.g. "fire"/"frost"), null before
	public BranchDef BranchDef => Catalog.Branch(Branch);
	public string DisplayName => BranchDef is { } b ? $"{Def.Name} · {b.Name}" : Def.Name;

	public float Damage => Catalog.Damage(Def, Level);
	public float Range => Catalog.Range(Def, Level);
	public float Cooldown => Catalog.Cooldown(Def, Level);
	public float Dps => Catalog.Dps(Def, Level);
	public bool MaxLevel => Level >= Catalog.MaxTowerLevel;
	public int UpgradeCost => Catalog.UpgradeCost(Def, Level);
	// Ground-only towers can target flyers once the owner has Scatter Shot.
	public bool HitsAir => Def.HitsAir || (!Preview && Lane.Match.Players[Lane.Index].Level("scatter") > 0);

	// Preview towers (icon baking, build ghost) are visual only: no lane, no shooting, no label.
	public bool Preview;
	public int PreviewLevel = 1;

	Node3D _body, _weapon;
	float _cooldown, _muzzleY;

	public override void _Ready()
	{
		Invested = Def.Cost;
		Record.Def ??= Def;
		Record.Cell = Cell;
		if (Preview)
		{
			Level = PreviewLevel;
			SetPhysicsProcess(false);
		}
		BuildVisual();
		UpdateLabel();
		if (Preview) _label.Visible = false;
	}

	// The building only changes at milestone levels (5 and 10): each tier adds a middle section
	// and a bigger weapon. In between, the level label above the tower shows progress.
	void BuildVisual()
	{
		int tier = Catalog.VisualTier(Level);
		float yaw = _weapon?.Rotation.Y ?? 0f;
		_body?.QueueFree();
		_weapon?.QueueFree();

		var pieces = new string[tier + 2];
		pieces[0] = $"tower-{Def.Shape}-bottom-{Def.Variant}";
		for (int i = 0; i < tier; i++) pieces[i + 1] = $"tower-{Def.Shape}-middle-{MiddleVariants[i % 3]}";
		pieces[^1] = Def.TopPiece ?? $"tower-{Def.Shape}-top-{Def.Variant}";
		_body = Stack(pieces);
		AddChild(_body);
		// Branch colour on the top piece (the crystal glows red for Fire, ice-blue for Frost).
		if (BranchDef is { } branch) Tint(_body.GetChild<Node3D>(_body.GetChildCount() - 1), branch.Tint);

		float height = Models.Measure(_body).End.Y;
		_muzzleY = height + 0.15f;
		_weapon = null;
		if (Def.Weapon != "")
		{
			_weapon = Models.Td(Def.Weapon);
			_weapon.Position = new Vector3(0, height - 0.05f, 0);
			_weapon.Scale = Vector3.One * (1f + 0.15f * tier);
			_weapon.Rotation = new Vector3(0, yaw, 0);
			AddChild(_weapon);
			_muzzleY = _weapon.Position.Y + 0.2f;
		}

		_label ??= new Label3D
		{
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 48, PixelSize = 0.009f,
			OutlineSize = 10, NoDepthTest = true, RenderPriority = 2,
		};
		if (_label.GetParent() == null) AddChild(_label);
		_label.Position = new Vector3(0, height + 0.55f + 0.1f * tier, 0);
	}

	Label3D _label;

	void UpdateLabel()
	{
		_label.Visible = Level > 1;
		_label.Text = MaxLevel ? "MAX" : Level.ToString();
		_label.Modulate = Catalog.VisualTier(Level) switch { 2 => new Color(1f, 0.8f, 0.2f), 1 => new Color(0.6f, 0.85f, 1f), _ => Colors.White };
	}

	static void Tint(Node n, Color tint)
	{
		if (n is GeometryInstance3D g)
			g.MaterialOverlay = new StandardMaterial3D
			{
				AlbedoColor = tint, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
				Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Mul,
			};
		foreach (var c in n.GetChildren()) Tint(c, tint);
	}

	// branch: the choice made at a milestone that offers one (validated by Match).
	public void Upgrade(string branch = null)
	{
		Invested += UpgradeCost;
		int oldTier = Catalog.VisualTier(Level);
		Level++;
		Record.Level = Level;
		if (branch != null) Branch = branch;
		if (Catalog.VisualTier(Level) != oldTier || branch != null) BuildVisual();
		UpdateLabel();
		float pop = Catalog.VisualTier(Level) != oldTier ? 0.7f : 0.92f;
		Scale = Vector3.One * pop;
		CreateTween().TweenProperty(this, "scale", Vector3.One, 0.25f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
	}

	// Stacks tower pieces on top of each other using their measured heights.
	public static Node3D Stack(params string[] pieces)
	{
		var root = new Node3D();
		float y = 0;
		foreach (var name in pieces)
		{
			var part = Models.Td(name);
			part.Position = new Vector3(0, y, 0);
			root.AddChild(part);
			y += Models.Measure(part).End.Y;
		}
		return root;
	}

	public override void _PhysicsProcess(double delta)
	{
		_cooldown -= (float)delta;
		var target = PickTarget();
		if (target == null) return;

		var to = target.Position - Position;
		if (_weapon != null) _weapon.Rotation = new Vector3(0, Mathf.Atan2(to.X, to.Z), 0);
		if (_cooldown > 0) return;
		_cooldown = Cooldown;

		var shot = new Projectile
		{
			Def = Def, Damage = Damage, Lane = Lane, Target = target,
			Position = Position + new Vector3(0, _muzzleY, 0),
		};
		shot.Source = Record;
		shot.ArmorIgnore = Def.ArmorIgnore;
		ApplyResearch(shot, Lane.Match.Players[Lane.Index]);
		Lane.AddChild(shot);
	}

	// Upgrades-menu effects for this tower type.
	void ApplyResearch(Projectile shot, PlayerState owner)
	{
		switch (Def.Id)
		{
			case "ballista":
				shot.Slow = Catalog.SlowPerLevel * owner.Level("slowing");
				shot.Bounces = owner.Level("splitting");
				break;
			case "cannon":
				shot.Burn = Catalog.BurnPerLevel * owner.Level("incendiary");
				shot.SplashBonus = Catalog.ShrapnelPerLevel * owner.Level("shrapnel");
				shot.ArmourBonusExtra = Catalog.HeavyShellsPerLevel * owner.Level("heavyshells");
				break;
			case "catapult":
				shot.Vulnerable = Catalog.VulnerabilityFor(owner.Level("boulders"));
				shot.AirDamage = Catalog.ScatterDamageFor(owner.Level("scatter"));
				break;
			case "crystal":
			{
				float attune = 1f + Catalog.AttunementPerLevel * owner.Level("attunement");
				shot.Burn = Catalog.BurnPerLevel * owner.Level("incendiary") + (Branch == "fire" ? Catalog.FireBurn * attune : 0f);
				if (Branch == "frost")
				{
					shot.Slow = Mathf.Min(0.6f, Catalog.FrostSlow * attune);
					shot.SlowDuration = Catalog.FrostSlowDuration;
				}
				if (BranchDef is { } b) shot.OrbColor = b.Tint.Clamp();
				break;
			}
		}
	}

	bool Better(Enemy a, Enemy b) => Targeting switch
	{
		TargetMode.Last => a.Distance < b.Distance,
		TargetMode.Strongest => a.Hp > b.Hp || (a.Hp == b.Hp && a.Distance > b.Distance),
		TargetMode.Weakest => a.Hp < b.Hp || (a.Hp == b.Hp && a.Distance > b.Distance),
		_ => a.Distance > b.Distance,
	};

	Enemy PickTarget()
	{
		Enemy best = null;
		float r2 = Range * Range;
		bool air = HitsAir;
		foreach (var e in Lane.Enemies)
		{
			if (e.Def.Flying && !air) continue;
			var d = e.Position - Position;
			if (d.X * d.X + d.Z * d.Z > r2) continue;
			if (best == null || Better(e, best)) best = e;
		}
		return best;
	}
}
