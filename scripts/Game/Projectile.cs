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
		_model = Models.Td(Def.Ammo);
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
		if (vel.LengthSquared() > 1e-6f) LookAt(GlobalPosition + vel, Vector3.Up);
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

	bool CanHit(Enemy e) => !e.Def.Flying || Def.HitsAir || AirDamage > 0;

	void Hit(Enemy e, float damage)
	{
		if (e.Def.Flying && !Def.HitsAir) damage *= AirDamage;
		e.TakeDamage(damage, Source, IsBounce);
		if (Slow > 0) e.ApplySlow(Slow, Catalog.SlowDuration);
		if (Burn > 0) e.ApplyBurn(damage * Burn, Catalog.BurnDuration, Source);
		if (Vulnerable > 0) e.ApplyVulnerable(Vulnerable, Catalog.VulnerableDuration);
	}

	// Splitting Bolts: a half-damage bolt to each of the nearest other creeps. Bounces don't bounce again.
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
