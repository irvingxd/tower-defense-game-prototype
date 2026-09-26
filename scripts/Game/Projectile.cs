using System.Linq;
using Godot;
using TowerDefense.Data;

namespace TowerDefense.Game;

// Flies from the tower to the target along an arc. If the target dies mid-flight it lands where
// the target was last seen. Splash towers damage every creep near the impact. Research effects
// (slow, burn, bounces, vulnerability, shrapnel, scatter) ride along from the owner's Upgrades.
public partial class Projectile : Node3D
{
	public TowerDef Def;
	public float Damage;
	public Lane Lane;
	public Enemy Target;
	public float Slow, Burn, Vulnerable, SplashBonus, AirDamage; // AirDamage: ground-only towers vs flyers (Scatter Shot)
	public float SlowDuration = Catalog.SlowDuration;
	public float ArmorIgnore, ArmourBonusExtra; // Crystal bolts; Heavy Shells
	public Color OrbColor = new(0.85f, 0.55f, 1f); // magic bolt colour when the tower has no ammo model
	public int Bounces;
	public TowerRecord Source; // damage/kill credit; outlives the tower if it is sold mid-flight
	public bool IsBounce;

	Vector3 _start, _aim;
	float _t, _duration;
	Node3D _model;

	public override void _Ready()
	{
		_start = Position;
		_aim = AimPoint();
		_duration = Mathf.Max(_start.DistanceTo(_aim) / Def.ProjectileSpeed, 0.08f);
		_model = Def.Ammo != "" ? Models.Td(Def.Ammo) : MagicOrb(OrbColor);
		AddChild(_model);
	}

	Vector3 AimPoint() => Target.Position + new Vector3(0, Target.Def.Flying ? 0.7f : 0.3f, 0);

	public override void _PhysicsProcess(double delta)
	{
		if (IsInstanceValid(Target) && !Target.Dead) _aim = AimPoint();
		_t += (float)delta / _duration;
		var prev = Position;
		float t = Mathf.Min(_t, 1f);
		Position = _start.Lerp(_aim, t) + new Vector3(0, Def.Arc * 4 * t * (1 - t), 0);
		var vel = Position - prev;
		// Skip near-vertical motion: LookAt with a direction parallel to Up is undefined (Godot warns).
		if (vel.LengthSquared() > 1e-6f && new Vector2(vel.X, vel.Z).LengthSquared() > vel.LengthSquared() * 1e-4f)
			LookAt(GlobalPosition + vel, Vector3.Up);
		if (_t < 1f) return;

		if (Def.Splash > 0)
		{
			float radius = Def.Splash * (1f + SplashBonus), r2 = radius * radius;
			foreach (var e in Lane.Enemies.ToArray())
				if (CanHit(e) && (e.Position - _aim).LengthSquared() <= r2) Hit(e, Damage);
		}
		else if (IsInstanceValid(Target) && !Target.Dead)
		{
			Hit(Target, Damage);
		}
		if (Bounces > 0) Bounce();
		QueueFree();
	}

	// Glowing sphere for magic towers (no ammo model in the kit fits a crystal).
	static Node3D MagicOrb(Color c)
	{
		var orb = new MeshInstance3D
		{
			Mesh = new SphereMesh { Radius = 0.09f, Height = 0.18f, RadialSegments = 12, Rings = 6 },
			MaterialOverride = new StandardMaterial3D
			{
				AlbedoColor = c, EmissionEnabled = true, Emission = c, EmissionEnergyMultiplier = 2.5f,
				ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			},
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
		};
		var root = new Node3D();
		root.AddChild(orb);
		return root;
	}

	bool CanHit(Enemy e) => !e.Def.Flying || Def.HitsAir || AirDamage > 0;

	void Hit(Enemy e, float damage)
	{
		if (e.Def.Flying && !Def.HitsAir) damage *= AirDamage;
		if (!IsBounce)
		{
			float role = Catalog.RoleBonus(Def, e.Def);
			if (role > 1f && Def.Id == "cannon") role += ArmourBonusExtra;
			damage *= role;
		}
		e.TakeDamage(damage, Source, IsBounce, ArmorIgnore);
		if (Slow > 0) e.ApplySlow(Slow, SlowDuration);
		if (Burn > 0) e.ApplyBurn(damage * Burn, Catalog.BurnDuration, Source);
		if (Vulnerable > 0) e.ApplyVulnerable(Vulnerable, Catalog.VulnerableDuration);
	}

	// Splitting Bolts: a reduced-damage bolt (SplitDamage) to each of the nearest other creeps. Bounces don't bounce again.
	void Bounce()
	{
		var from = new Vector3(_aim.X, 0, _aim.Z);
		float r2 = Catalog.SplitRange * Catalog.SplitRange;
		var targets = Lane.Enemies
			.Where(e => e != Target && !e.Dead && (!e.Def.Flying || Def.HitsAir))
			.Select(e => (e, d: new Vector3(e.Position.X, 0, e.Position.Z).DistanceSquaredTo(from)))
			.Where(x => x.d <= r2)
			.OrderBy(x => x.d)
			.Take(Bounces)
			.ToList();
		foreach (var (e, _) in targets)
		{
			Lane.AddChild(new Projectile
			{
				Def = Def, Lane = Lane, Target = e, Position = _aim, Source = Source, IsBounce = true,
				Damage = Damage * Catalog.SplitDamage * (e.IsBoss ? Catalog.BossEffect : 1f),
			});
		}
	}
}
