using Godot;
using TowerDefense.Data;

namespace TowerDefense.Game;

public partial class Enemy : Node3D
{
	const float FlyHeight = 0.35f;

	public UnitDef Def;
	public Lane Lane;
	public float HpMultiplier = 1f;
	public float ArmorMultiplier = 1f;
	public int StartWaypoint = 1;       // splits continue from where their parent died
	public Vector3? StartPosition;
	public float Hp, MaxHp;
	public float Distance; // distance walked; towers target the creep furthest along
	public bool Dead;
	public bool Sent; // came from the opponent's sends: pays half bounty

	int _wp;
	float _armor;
	float _slow, _slowTime, _burnDps, _burnTime;

	// Status tints: an additive overlay on every mesh, plus embers while burning.
	static readonly StandardMaterial3D SlowTint = Tint(new Color(0.35f, 0.65f, 1f, 0.45f));
	static readonly StandardMaterial3D BurnTint = Tint(new Color(1f, 0.45f, 0.1f, 0.5f));
	static readonly StandardMaterial3D BothTint = Tint(new Color(0.8f, 0.4f, 1f, 0.5f));
	static readonly StandardMaterial3D VulnTint = Tint(new Color(0.9f, 0.15f, 0.2f, 0.45f));
	int _tintState = -1;
	CpuParticles3D _embers;

	static StandardMaterial3D Tint(Color c) => new()
	{
		AlbedoColor = c,
		ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
		Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
		BlendMode = BaseMaterial3D.BlendModeEnum.Add,
	};

	void UpdateStatusVisuals()
	{
		int state = (Slowed ? 1 : 0) | (Burning ? 2 : 0) | (Vulnerable ? 4 : 0);
		if (state == _tintState) return;
		_tintState = state;
		// Burn/slow tints win; red "vulnerable" shows when it's the only effect.
		var overlay = (state & 3) switch { 1 => SlowTint, 2 => BurnTint, 3 => BothTint, _ => state == 4 ? VulnTint : null };
		SetOverlay(_model, overlay);

		if (Burning && _embers == null)
		{
			_embers = new CpuParticles3D
			{
				Amount = 10, Lifetime = 0.6f, Position = new Vector3(0, Def.Height * 0.5f + (Def.Flying ? FlyHeight : 0), 0),
				EmissionShape = CpuParticles3D.EmissionShapeEnum.Sphere, EmissionSphereRadius = Def.Height * 0.3f,
				Direction = Vector3.Up, Spread = 20, Gravity = new Vector3(0, 1.2f, 0), InitialVelocityMin = 0.2f, InitialVelocityMax = 0.5f,
				ScaleAmountMin = 0.5f, ScaleAmountMax = 1f,
				Mesh = new QuadMesh { Size = new Vector2(0.06f, 0.06f), Material = new StandardMaterial3D
				{
					AlbedoColor = new Color(1f, 0.6f, 0.15f), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
					BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled, BlendMode = BaseMaterial3D.BlendModeEnum.Add,
				} },
			};
			AddChild(_embers);
		}
		if (_embers != null) _embers.Emitting = Burning;
		if (_anim != null) _anim.SpeedScale = Slowed ? 1f - _slow : 1f; // slowed creeps visibly trudge
	}

	static void SetOverlay(Node n, Material overlay)
	{
		if (n is GeometryInstance3D g) g.MaterialOverlay = overlay;
		foreach (var c in n.GetChildren()) SetOverlay(c, overlay);
	}

	public bool IsBoss => Def.Role == Role.Boss;
	public float Armor => _armor;
	public float CurrentSpeed => Def.Speed * (Slowed ? 1f - _slow : 1f);
	public bool Slowed => _slowTime > 0;
	public bool Burning => _burnTime > 0;
	AnimationPlayer _anim;
	HealthBar _bar;
	Node3D _model;

	public override void _Ready()
	{
		MaxHp = Hp = Def.Hp * HpMultiplier;
		_armor = Def.Armor * ArmorMultiplier;
		_wp = StartWaypoint;
		Position = StartPosition ?? Lane.PathPoints[0];

		_model = Models.Monster(Def.Model);
		var box = Models.Measure(_model);
		float scale = Def.Height / Mathf.Max(box.Size.Y, 0.001f);
		_model.Scale = Vector3.One * scale;
		// Stand on the path, or hover above it for flyers.
		_model.Position = new Vector3(0, (Def.Flying ? FlyHeight : 0f) - box.Position.Y * scale, 0);
		AddChild(_model);

		_anim = Models.FindOfType<AnimationPlayer>(_model);
		var clip = PickMoveAnimation(_anim);
		if (clip != null)
		{
			_anim.GetAnimation(clip).LoopMode = Animation.LoopModeEnum.Linear;
			_anim.Play(clip, customSpeed: Mathf.Clamp(Def.Speed, 0.7f, 1.6f));
		}

		float top = Def.Height + (Def.Flying ? FlyHeight : 0f);
		_bar = new HealthBar { Offset = new Vector3(0, top + 0.12f, 0), Width = Def.Role == Role.Boss ? 0.8f : 0.42f };
		AddChild(_bar);
	}

	// Monster rigs use different clip names: Walk (blobs/big), Fast_Flying (flyers), walk (Kenney).
	public static string PickMoveAnimation(AnimationPlayer anim)
	{
		if (anim == null) return null;
		foreach (var clip in new[] { "Walk", "walk", "Fast_Flying", "Run", "Flying_Idle" })
			if (anim.HasAnimation(clip)) return clip;
		return null;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (Dead) return;
		float dt = (float)delta;
		_slowTime -= dt;
		_vulnTime -= dt;
		if (_burnTime > 0)
		{
			_burnTime -= dt;
			_burnShown += Hurt(_burnDps * dt, _burnSource); // burn ignores armour
			if (Dead) return;
			_burnShowTimer -= dt;
			if (_burnShowTimer <= 0 || _burnTime <= 0) FlushBurnNumber();
		}
		else if (Def.Regen > 0 && Hp < MaxHp) // burning stops regeneration
		{
			Hp = Mathf.Min(MaxHp, Hp + MaxHp * Def.Regen * dt);
			_bar.Set(Hp / MaxHp);
		}

		UpdateStatusVisuals();
		float step = Def.Speed * (Slowed ? 1f - _slow : 1f) * dt;
		Distance += step;
		while (step > 0)
		{
			var target = Lane.PathPoints[_wp];
			var to = target - Position;
			float len = to.Length();
			if (len > 0.0001f) Rotation = new Vector3(0, Mathf.Atan2(to.X, to.Z), 0);
			if (len <= step)
			{
				Position = target;
				step -= len;
				if (++_wp >= Lane.PathPoints.Count)
				{
					Dead = true;
					Lane.OnEnemyLeaked(this);
					QueueFree();
					return;
				}
			}
			else
			{
				Position += to / len * step;
				step = 0;
			}
		}
	}

	// source gets the damage/kill credit. Vulnerability (Heavy Boulders) amplifies the hit before armour.
	public void TakeDamage(float amount, TowerRecord source = null, bool bounce = false)
	{
		if (Dead) return;
		if (Vulnerable) amount *= 1f + _vuln;
		float dealt = Hurt(Mathf.Max(amount - _armor, amount * 0.2f), source);
		if (dealt >= 0.5f)
			FloatingText.Spawn(Lane, NumberPos, Mathf.RoundToInt(dealt).ToString(),
				bounce ? FloatingText.BounceColor : FloatingText.DamageColor, bounce ? 26 : 34);
	}

	Vector3 NumberPos => Position + new Vector3(0, Def.Height + (Def.Flying ? FlyHeight : 0) + 0.3f, 0);

	// Burn ticks every frame; show it as one number every half second instead.
	float _burnShown, _burnShowTimer;
	TowerRecord _burnSource;

	void FlushBurnNumber()
	{
		_burnShowTimer = 0.5f;
		if (_burnShown >= 0.5f) FloatingText.Spawn(Lane, NumberPos, Mathf.RoundToInt(_burnShown).ToString(), FloatingText.BurnColor, 26);
		_burnShown = 0;
	}

	float _vuln, _vulnTime;
	public bool Vulnerable => _vulnTime > 0;

	// Strongest vulnerability wins and refreshes; never stacks.
	public void ApplyVulnerable(float amount, float duration)
	{
		if (Dead || amount <= 0) return;
		if (Vulnerable && amount < _vuln) return;
		_vuln = amount;
		_vulnTime = duration;
	}

	// Strongest slow wins; slows never stack. Bosses take half.
	public void ApplySlow(float amount, float duration)
	{
		if (Dead) return;
		if (IsBoss) amount *= Catalog.BossEffect;
		if (Slowed && amount < _slow) return;
		_slow = amount;
		_slowTime = duration;
	}

	// Strongest burn wins and refreshes; burns never stack.
	public void ApplyBurn(float dps, float duration, TowerRecord source = null)
	{
		if (Dead) return;
		if (Burning && dps < _burnDps) return;
		_burnDps = dps;
		_burnTime = duration;
		_burnSource = source;
	}

	// Returns the HP actually removed (overkill doesn't count toward a tower's damage).
	float Hurt(float amount, TowerRecord source)
	{
		float dealt = Mathf.Min(amount, Mathf.Max(Hp, 0));
		Hp -= amount;
		if (source != null) source.Damage += dealt;
		_bar.Set(Hp / MaxHp);
		if (Hp <= 0)
		{
			if (source != null) source.Kills++;
			Die();
		}
		return dealt;
	}

	void Die()
	{
		if (Dead) return;
		Dead = true;
		SetOverlay(_model, null);
		if (_embers != null) _embers.Emitting = false;
		if (_anim != null) _anim.SpeedScale = 1;
		Lane.OnEnemyKilled(this);
		if (Def.SplitInto != null)
			for (int i = 0; i < Def.SplitCount; i++)
				Lane.SpawnSplit(Def.SplitInto, Sent, HpMultiplier, ArmorMultiplier, _wp, Position + new Vector3((i - 1) * 0.15f, 0, 0), Distance - i * 0.1f);

		_bar.Visible = false;
		if (_anim != null)
		{
			var death = _anim.HasAnimation("Death") ? "Death" : _anim.HasAnimation("die") ? "die" : null;
			if (death != null)
			{
				_anim.GetAnimation(death).LoopMode = Animation.LoopModeEnum.None;
				_anim.Play(death);
			}
		}
		var tw = CreateTween();
		tw.TweenInterval(0.9f);
		tw.TweenProperty(this, "scale", Vector3.One * 0.01f, 0.3f);
		tw.TweenCallback(Callable.From(QueueFree));
	}
}
