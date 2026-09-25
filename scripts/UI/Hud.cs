using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using TowerDefense.Data;
using TowerDefense.Game;

namespace TowerDefense.UI;

// Screen layout:
//   top bar      — your stats · wave banner + countdown · opponent stats
//   left dock    — Buildings (Q) · Upgrades (R) · Army (T) · Intel (I), each popping out a flyout
//   bottom right — game speed, pause, Start Wave
//   bottom       — selected tower card, or the build hint
//   overlays     — wave/boss banner, leak alerts, pause menu, end screen
public partial class Hud : CanvasLayer
{
	public Match Match;
	public InputController Input; // null when the local player is an AI (autotest)
	public int Player;

	PlayerState Me => Match.Players[Player];
	PlayerState Them => Match.Players[1 - Player];

	Control _root;

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always; // pause menu keeps working while the tree is paused
		_root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, Theme = UiTheme.Build() };
		_root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		AddChild(_root);

		BuildTopBar();
		BuildDock();
		BuildActions();
		BuildTowerCard();
		BuildHint();
		BuildBanner();
		BuildPause();
		BuildGameOver();

		Match.WaveStarting += OnWaveStarting;
		Match.BuildStarted += OnBuildStarted;
		Match.Killed += OnKilled;
		Match.Leaked += OnLeaked;
		Match.MatchOver += ShowGameOver;
	}

	public override void _ExitTree()
	{
		Match.WaveStarting -= OnWaveStarting;
		Match.BuildStarted -= OnBuildStarted;
		Match.Killed -= OnKilled;
		Match.Leaked -= OnLeaked;
		Match.MatchOver -= ShowGameOver;
		GetTree().Paused = false;
	}

	public override void _Process(double delta)
	{
		UpdateTopBar();
		UpdateActions();
		UpdateTowerCard();
		UpdateHint();
		if (_openFlyout != null)
		{
			_flyoutUpdaters[_openFlyout]();
			FitFlyout(_flyouts[_openFlyout]);
		}
		foreach (var (name, button) in _dockButtons) button.ButtonPressed = _openFlyout == name;
	}

	// ================================================================== top bar

	Label _meName, _meLives, _meGold, _meIncome, _meChest, _waveTitle, _waveSub, _themName, _themLives, _themIncome;
	PanelContainer _chestChip;
	ProgressBar _countdown;

	void BuildTopBar()
	{
		var bar = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		bar.SetAnchorsPreset(Control.LayoutPreset.TopWide);
		var style = UiTheme.Panel(UiTheme.Bg, 0, 8);
		style.SetBorderWidthAll(0);
		style.BorderWidthBottom = 1;
		style.BorderColor = UiTheme.Accent with { A = 0.35f };
		bar.AddThemeStyleboxOverride("panel", style);
		_root.AddChild(bar);

		var row = new HBoxContainer();
		bar.AddChild(row);

		// You
		var left = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		left.AddThemeConstantOverride("separation", 10);
		row.AddChild(left);
		_meName = UiTheme.Label("", 17, UiTheme.Accent, bold: true);
		left.AddChild(_meName);
		(var c1, _meLives) = UiTheme.Chip("♥", UiTheme.Heart, "Lives — a creep that reaches your keep costs lives (bosses cost more).");
		(var c2, _meGold) = UiTheme.Chip("●", UiTheme.Gold, "Gold");
		(var c3, _meIncome) = UiTheme.Chip("▲", UiTheme.Good, "Income paid at the start of every wave (small — most gold comes from kills). Sending units raises it permanently.");
		(_chestChip, _meChest) = UiTheme.Chip("◆", UiTheme.Info, "War Chest interest you'll receive next wave.");
		foreach (var c in new[] { c1, c2, c3, _chestChip }) left.AddChild(c);

		// Wave banner
		var center = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center };
		center.AddThemeConstantOverride("separation", 1);
		row.AddChild(center);
		_waveTitle = UiTheme.Label("", 21, bold: true, align: HorizontalAlignment.Center);
		_waveSub = UiTheme.Label("", 13, UiTheme.Muted, align: HorizontalAlignment.Center);
		_countdown = new ProgressBar { ShowPercentage = false, CustomMinimumSize = new Vector2(260, 5), SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter };
		center.AddChild(_waveTitle);
		center.AddChild(_waveSub);
		center.AddChild(_countdown);

		// Opponent
		var right = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.End };
		right.AddThemeConstantOverride("separation", 10);
		row.AddChild(right);
		(var o1, _themLives) = UiTheme.Chip("♥", UiTheme.Heart, "Opponent's lives");
		(var o2, _themIncome) = UiTheme.Chip("▲", UiTheme.Good, "Opponent's income per wave");
		right.AddChild(o1);
		right.AddChild(o2);
		_themName = UiTheme.Label("", 17, UiTheme.Bad, bold: true);
		right.AddChild(_themName);
	}

	void UpdateTopBar()
	{
		_meName.Text = Me.Name;
		_meLives.Text = Me.Lives.ToString();
		_meLives.AddThemeColorOverride("font_color", Me.Lives <= 5 ? UiTheme.Bad : UiTheme.Text);
		_meGold.Text = Me.Gold.ToString("N0");
		_meIncome.Text = $"+{Match.Income(Me)}";
		_chestChip.Visible = Me.Level("warchest") > 0;
		_meChest.Text = $"+{Match.Interest(Me)}";
		_themName.Text = Them.Name;
		_themLives.Text = Them.Lives.ToString();
		_themIncome.Text = $"+{Match.Income(Them)}";

		var theme = Catalog.ThemeFor(Match.Wave);
		_waveTitle.Text = $"WAVE {Match.Wave} / {Catalog.FinalWave}";
		var boss = Catalog.IsBossWave(Match.Wave) ? $"   ·   BOSS: {Catalog.Unit(theme.Boss).Name}" : "";
		_waveSub.Text = Match.State switch
		{
			Match.Phase.Build => $"{theme.Name}{boss}   ·   starts in {Mathf.CeilToInt(Match.BuildTimer)}s",
			Match.Phase.Wave => $"{theme.Name}{boss}   ·   {Match.Lanes[Player].Enemies.Count} creeps on your lane",
			_ => "Game over",
		};
		_waveSub.AddThemeColorOverride("font_color", boss != "" ? UiTheme.Bad : UiTheme.Muted);
		_countdown.Visible = Match.State == Match.Phase.Build;
		_countdown.MaxValue = Match.BuildDuration;
		_countdown.Value = Match.BuildTimer;
	}

	// ================================================================== dock + flyouts

	string _openFlyout;
	readonly Dictionary<string, Control> _flyouts = new();
	readonly Dictionary<string, Action> _flyoutUpdaters = new();
	readonly List<(string name, Button button)> _dockButtons = new();

	void BuildDock()
	{
		var dock = new PanelContainer();
		dock.AddThemeStyleboxOverride("panel", UiTheme.Panel(UiTheme.Bg, 14, 8));
		dock.SetAnchorsPreset(Control.LayoutPreset.CenterLeft);
		dock.Position = new Vector2(10, -170);
		_root.AddChild(dock);
		var col = new VBoxContainer();
		col.AddThemeConstantOverride("separation", 8);
		dock.AddChild(col);

		AddDock(col, "Buildings", "Q", "wrench", "Build towers", BuildBuildings());
		AddDock(col, "Upgrades", "R", "star", "Research upgrades for your towers and economy", BuildUpgrades());
		AddDock(col, "Army", "T", "multiplayer", "Send units into the enemy's next wave — raises your income", BuildArmy());
		AddDock(col, "Intel", "I", "information", "Preview of the next wave", BuildIntel());
	}

	void AddDock(VBoxContainer col, string name, string key, string icon, string tip, (Control panel, Action update) flyout)
	{
		var b = UiTheme.Button($"{name}\n{key}", 84, 74, 12);
		b.Icon = UiTheme.Icon(icon);
		b.ExpandIcon = true;
		b.IconAlignment = HorizontalAlignment.Center;
		b.VerticalIconAlignment = VerticalAlignment.Top;
		b.AddThemeConstantOverride("icon_max_width", 30);
		b.ToggleMode = true;
		b.TooltipText = $"{tip}  [{key}]";
		b.Pressed += () => ToggleFlyout(name);
		col.AddChild(b);
		_dockButtons.Add((name, b));

		flyout.panel.Visible = false;
		flyout.panel.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
		flyout.panel.Position = new Vector2(112, 76);
		_root.AddChild(flyout.panel);
		_flyouts[name] = flyout.panel;
		_flyoutUpdaters[name] = flyout.update;
	}

	// Height = content height, capped so long lists scroll instead of running under the bottom bar.
	void FitFlyout(Control panel)
	{
		var scroll = (ScrollContainer)panel.GetMeta("scroll");
		var body = (Control)panel.GetMeta("body");
		float max = GetViewport().GetVisibleRect().Size.Y - 260;
		scroll.CustomMinimumSize = new Vector2(scroll.CustomMinimumSize.X, Mathf.Min(body.GetCombinedMinimumSize().Y, max));
		panel.Size = panel.GetCombinedMinimumSize();
	}

	void ToggleFlyout(string name)
	{
		_openFlyout = _openFlyout == name ? null : name;
		foreach (var (n, panel) in _flyouts) panel.Visible = n == _openFlyout;
	}

	// Common flyout frame: title, close button, scrolling body.
	static (PanelContainer panel, VBoxContainer body) Flyout(string title, string subtitle, float width)
	{
		var panel = new PanelContainer { CustomMinimumSize = new Vector2(width, 0) };
		var col = new VBoxContainer();
		col.AddThemeConstantOverride("separation", 8);
		panel.AddChild(col);
		var head = new HBoxContainer();
		col.AddChild(head);
		var titles = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		titles.AddThemeConstantOverride("separation", 0);
		titles.AddChild(UiTheme.Label(title, 20, UiTheme.Accent, bold: true));
		titles.AddChild(UiTheme.Label(subtitle, 12, UiTheme.Muted));
		head.AddChild(titles);
		col.AddChild(UiTheme.Divider());
		var scroll = new ScrollContainer
		{
			CustomMinimumSize = new Vector2(width - 24, 0), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
		};
		col.AddChild(scroll);
		var body = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		body.AddThemeConstantOverride("separation", 8);
		scroll.AddChild(body);
		panel.SetMeta("scroll", scroll);
		panel.SetMeta("body", body);
		return (panel, body);
	}

	// A clickable card: a toggle button whose content is laid out with child controls.
	static (Button card, HBoxContainer row) Card(float height)
	{
		var card = new Button { ToggleMode = true, CustomMinimumSize = new Vector2(0, height), FocusMode = Control.FocusModeEnum.None };
		var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		row.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		row.OffsetLeft = 8; row.OffsetRight = -8;
		row.AddThemeConstantOverride("separation", 10);
		card.AddChild(row);
		return (card, row);
	}

	static VBoxContainer TextColumn()
	{
		var col = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
		col.AddThemeConstantOverride("separation", 0);
		return col;
	}

	// ---------------------------------------------------------------- Buildings

	(Control, Action) BuildBuildings()
	{
		var (panel, body) = Flyout("Buildings", "Pick a tower, then click an empty tile. Right-click cancels.", 400);
		var cards = new List<(Button card, TowerDef def)>();
		int i = 1;
		foreach (var def in Catalog.Towers)
		{
			var (card, row) = Card(92);
			row.AddChild(UiTheme.Image(UiTheme.TowerPortrait(def.Id, 1), 72));
			var text = TextColumn();
			row.AddChild(text);
			var head = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
			head.AddChild(UiTheme.Label(def.Name, 17, bold: true));
			head.AddChild(UiTheme.Label($"[{i++}]", 12, UiTheme.Muted));
			text.AddChild(head);
			text.AddChild(UiTheme.Label($"● {def.Cost}", 15, UiTheme.Gold, bold: true));
			text.AddChild(UiTheme.Label($"DMG {def.Damage:0}  ·  RNG {def.Range:0.0}  ·  {1 / def.Cooldown:0.0}/s", 12, UiTheme.Muted));
			var tags = (def.Splash > 0 ? "Splash  ·  " : "Single target  ·  ") + (def.HitsAir ? "Hits air" : "Ground only");
			text.AddChild(UiTheme.Label(tags, 12, def.HitsAir ? UiTheme.Info : UiTheme.Bad));
			var d = def;
			card.Pressed += () => Input?.SelectBuild(d.Id);
			body.AddChild(card);
			cards.Add((card, def));
		}
		body.AddChild(UiTheme.Label("Towers level up to 10 — click a built tower to upgrade.\nThe building changes at level 5 and 10.", 12, UiTheme.Muted));
		return (panel, () =>
		{
			foreach (var (card, def) in cards)
			{
				card.Disabled = Me.Gold < def.Cost;
				card.ButtonPressed = Input?.SelectedTower == def.Id;
			}
		});
	}

	// ---------------------------------------------------------------- Upgrades

	(Control, Action) BuildUpgrades()
	{
		var (panel, body) = Flyout("Upgrades", "Player-wide research: two per tower. Slow and bounces are half as strong on bosses.", 470);
		var interest = UiTheme.Label("", 13, UiTheme.Info);
		body.AddChild(interest);
		var rows = new List<(ResearchDef def, Label title, Label pips, Label desc, Button buy)>();
		foreach (var def in Catalog.Research)
		{
			var card = new PanelContainer();
			card.AddThemeStyleboxOverride("panel", UiTheme.Panel(UiTheme.BgSoft, 10, 10));
			var row = new HBoxContainer();
			card.AddChild(row);
			var text = TextColumn();
			row.AddChild(text);
			var head = new HBoxContainer();
			var title = UiTheme.Label(def.Name, 16, bold: true);
			head.AddChild(title);
			head.AddChild(UiTheme.Label(def.Target, 11, UiTheme.Muted));
			var pips = UiTheme.Label("", 14, UiTheme.Accent);
			head.AddChild(pips);
			text.AddChild(head);
			var desc = UiTheme.Label("", 12, UiTheme.Muted);
			desc.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			desc.CustomMinimumSize = new Vector2(290, 0);
			text.AddChild(desc);
			var buy = UiTheme.Button("", 120, 44, 13);
			buy.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
			var d = def;
			buy.Pressed += () => Match.Submit(Player, new BuyResearch(d.Id));
			row.AddChild(buy);
			body.AddChild(card);
			rows.Add((def, title, pips, desc, buy));
		}
		return (panel, () =>
		{
			int chest = Me.Level("warchest");
			interest.Visible = chest > 0;
			interest.Text = $"War Chest: {Catalog.WarChestInterest(chest) * 100:0}% of banked gold → +{Match.Interest(Me)} next wave (cap {Catalog.WarChestCap(Match.Wave)})";
			foreach (var (def, title, pips, desc, buy) in rows)
			{
				int level = Me.Level(def.Id);
				bool maxed = level >= def.MaxLevel;
				pips.Text = new string('●', level) + new string('○', def.MaxLevel - level);
				desc.Text = maxed ? def.Describe(level) : (level == 0 ? "" : "Next: ") + def.Describe(level + 1);
				var blocker = Match.ResearchBlocker(Me, def);
				buy.Disabled = blocker != null;
				buy.Text = blocker switch
				{
					null => $"Buy  ● {def.Costs[level]}",
					"Not enough gold" => $"● {def.Costs[level]}",
					"Maxed" => "✓ Maxed",
					_ => blocker,
				};
				title.AddThemeColorOverride("font_color", level > 0 ? UiTheme.Accent : UiTheme.Text);
			}
		});
	}

	// ---------------------------------------------------------------- Army

	(Control, Action) BuildArmy()
	{
		var (panel, body) = Flyout("Army", "Units join the enemy's next wave and raise your income for the rest of the game.", 470);
		var pending = UiTheme.Label("", 13, UiTheme.Accent);
		pending.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		body.AddChild(pending);
		var grid = new GridContainer { Columns = 2 };
		grid.AddThemeConstantOverride("h_separation", 8);
		grid.AddThemeConstantOverride("v_separation", 8);
		body.AddChild(grid);
		var cards = new List<(Button card, SendDef send, Label price)>();
		foreach (var send in Catalog.Sends)
		{
			var unit = Catalog.Unit(send.UnitId);
			var (card, row) = Card(78);
			card.ToggleMode = false;
			card.CustomMinimumSize = new Vector2(215, 78);
			card.TooltipText = $"{unit.Name} — {unit.Role}{(unit.Flying ? ", flying" : "")}{(unit.Armor > 0 ? ", armoured" : "")}\n" +
				$"+{send.Income} income every wave from now on.  [{send.Key}]";
			row.AddChild(UiTheme.Image(UiTheme.UnitPortrait(unit.Id), 56));
			var text = TextColumn();
			row.AddChild(text);
			text.AddChild(UiTheme.Label($"{unit.Name}  [{send.Key}]", 14, bold: true));
			var price = UiTheme.Label("", 13, UiTheme.Gold);
			text.AddChild(price);
			text.AddChild(UiTheme.Label(RoleText(unit), 11, UiTheme.Muted));
			var s = send;
			card.Pressed += () => Match.Submit(Player, new SendUnit(s.UnitId));
			grid.AddChild(card);
			cards.Add((card, send, price));
		}
		return (panel, () =>
		{
			pending.Text = Me.PendingSends.Count == 0 ? "Nothing queued for the next wave yet."
				: "Queued: " + string.Join(", ", Me.PendingSends.GroupBy(x => x).Select(g => $"{g.Count()}× {Catalog.Unit(g.Key).Name}"));
			foreach (var (card, send, price) in cards)
			{
				bool locked = Match.Wave < send.UnlockWave;
				card.Disabled = locked || Me.Gold < send.Cost;
				card.Modulate = locked ? new Color(1, 1, 1, 0.45f) : Colors.White;
				price.Text = locked ? $"🔒 Unlocks wave {send.UnlockWave}" : $"● {send.Cost}   ▲ +{send.Income}";
			}
		});
	}

	static string RoleText(UnitDef u)
	{
		var bits = new List<string>();
		if (u.Role is not (Role.Flyer or Role.Armored or Role.Regen or Role.Splitter)) bits.Add(u.Role.ToString());
		if (u.Flying) bits.Add("Flying");
		if (u.Armor > 0) bits.Add($"Armour {u.Armor:0}");
		if (u.Regen > 0) bits.Add($"Regen {u.Regen * 100:0}%/s");
		if (u.SplitInto != null) bits.Add($"Splits ×{u.SplitCount}");
		return string.Join(" · ", bits);
	}

	// ---------------------------------------------------------------- Intel

	(Control, Action) BuildIntel()
	{
		var (panel, body) = Flyout("Intel", "What's coming down your lane next.", 400);
		int shownWave = -1;
		return (panel, () =>
		{
			int wave = Match.State == Match.Phase.Build ? Match.Wave : Math.Min(Match.Wave + 1, Catalog.FinalWave);
			if (wave == shownWave) return;
			shownWave = wave;
			foreach (var c in body.GetChildren()) c.QueueFree();

			var theme = Catalog.ThemeFor(wave);
			body.AddChild(UiTheme.Label($"Wave {wave} · {theme.Name}", 17, bold: true));
			body.AddChild(UiTheme.Label($"Creep HP ×{Catalog.HpMultiplier(wave):0.0}   ·   armour ×{Catalog.ArmorMultiplier(wave):0.0}", 12, UiTheme.Muted));
			foreach (var g in Catalog.BaseWave(wave).GroupBy(x => x))
			{
				var unit = Catalog.Unit(g.Key);
				var row = new HBoxContainer();
				row.AddChild(UiTheme.Image(UiTheme.UnitPortrait(unit.Id), 44));
				var text = TextColumn();
				text.AddChild(UiTheme.Label($"{g.Count()}×  {unit.Name}", 15, unit.Role == Role.Boss ? UiTheme.Bad : UiTheme.Text, bold: true));
				text.AddChild(UiTheme.Label(RoleText(unit), 12, UiTheme.Muted));
				row.AddChild(text);
				body.AddChild(row);
			}
			body.AddChild(UiTheme.Label("+ whatever your opponent sends (hidden)", 12, UiTheme.Muted));
			int nextBoss = (wave + Catalog.WavesPerTheme - 1) / Catalog.WavesPerTheme * Catalog.WavesPerTheme;
			if (nextBoss != wave && nextBoss <= Catalog.FinalWave)
				body.AddChild(UiTheme.Label($"Next boss: wave {nextBoss} — {Catalog.Unit(Catalog.ThemeFor(nextBoss).Boss).Name}", 13, UiTheme.Bad));
		});
	}

	// ================================================================== actions (bottom right)

	Button _startWave;
	readonly List<(Button b, float speed)> _speedButtons = new();
	static readonly float[] Speeds = { 1f, 2f, 3f };

	void BuildActions()
	{
		var panel = new PanelContainer();
		panel.AddThemeStyleboxOverride("panel", UiTheme.Panel(UiTheme.Bg, 14, 10));
		panel.SetAnchorsPreset(Control.LayoutPreset.BottomRight);
		panel.GrowHorizontal = Control.GrowDirection.Begin;
		panel.GrowVertical = Control.GrowDirection.Begin;
		panel.Position -= new Vector2(12, 12);
		_root.AddChild(panel);
		var row = new HBoxContainer();
		panel.AddChild(row);

		foreach (var speed in Speeds)
		{
			var b = UiTheme.Button($"{speed:0}×", 46, 48, 15);
			b.ToggleMode = true;
			b.TooltipText = "Game speed  [F]";
			var s = speed;
			b.Pressed += () => SetSpeed(s);
			row.AddChild(b);
			_speedButtons.Add((b, speed));
		}
		var pause = UiTheme.Button("", 48, 48);
		pause.Icon = UiTheme.Icon("pause");
		pause.ExpandIcon = true;
		pause.AddThemeConstantOverride("icon_max_width", 24);
		pause.TooltipText = "Pause  [Esc]";
		pause.Pressed += () => SetPaused(true);
		row.AddChild(pause);

		_startWave = UiTheme.PrimaryButton("", 210, 48);
		_startWave.TooltipText = "Start the next wave now  [Space]";
		_startWave.Pressed += () => Match.Submit(Player, new ReadyUp());
		row.AddChild(_startWave);
	}

	void SetSpeed(float speed)
	{
		if (Input == null) return; // autotest drives its own time scale
		Engine.TimeScale = speed;
	}

	void UpdateActions()
	{
		foreach (var (b, speed) in _speedButtons)
		{
			b.ButtonPressed = Mathf.IsEqualApprox((float)Engine.TimeScale, speed);
			b.Disabled = Input == null;
		}
		_startWave.Disabled = Match.State != Match.Phase.Build || Me.Ready;
		_startWave.Text = Match.State switch
		{
			Match.Phase.Build when Me.Ready => "Waiting…",
			Match.Phase.Build => $"START WAVE {Match.Wave}   {Mathf.CeilToInt(Match.BuildTimer)}s",
			Match.Phase.Wave => "Wave in progress",
			_ => "Game over",
		};
	}

	// ================================================================== selected tower card

	PanelContainer _towerCard;
	TextureRect _towerImage;
	Label _towerName, _towerLevel, _towerStats, _towerNext, _towerRecord;
	readonly List<(Button b, TargetMode mode)> _targetButtons = new();
	HBoxContainer _pips;
	Button _upgrade, _sell;

	void BuildTowerCard()
	{
		_towerCard = new PanelContainer { Visible = false };
		_towerCard.SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
		_towerCard.GrowHorizontal = Control.GrowDirection.Both;
		_towerCard.GrowVertical = Control.GrowDirection.Begin;
		_towerCard.Position -= new Vector2(150, 12); // keep clear of the bottom-right action cluster
		_root.AddChild(_towerCard);
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 14);
		_towerCard.AddChild(row);
		_towerImage = UiTheme.Image(null, 96);
		row.AddChild(_towerImage);

		var text = TextColumn();
		text.CustomMinimumSize = new Vector2(300, 0);
		text.AddThemeConstantOverride("separation", 3);
		row.AddChild(text);
		var head = new HBoxContainer();
		_towerName = UiTheme.Label("", 19, bold: true);
		_towerLevel = UiTheme.Label("", 14, UiTheme.Accent, bold: true);
		head.AddChild(_towerName);
		head.AddChild(_towerLevel);
		text.AddChild(head);
		_pips = new HBoxContainer();
		_pips.AddThemeConstantOverride("separation", 3);
		for (int i = 1; i <= Catalog.MaxTowerLevel; i++)
		{
			bool milestone = Catalog.VisualTier(i) != Catalog.VisualTier(i - 1);
			_pips.AddChild(new ColorRect { CustomMinimumSize = new Vector2(milestone ? 22 : 16, milestone ? 10 : 8), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter });
		}
		text.AddChild(_pips);
		_towerStats = UiTheme.Label("", 13, UiTheme.Muted);
		text.AddChild(_towerStats);
		_towerNext = UiTheme.Label("", 12, UiTheme.Good);
		text.AddChild(_towerNext);
		_towerRecord = UiTheme.Label("", 12, UiTheme.Info);
		text.AddChild(_towerRecord);

		// Targeting mode selector
		var targeting = new HBoxContainer();
		targeting.AddThemeConstantOverride("separation", 4);
		var targetLabel = UiTheme.Label("Target [G]", 12, UiTheme.Muted);
		targetLabel.CustomMinimumSize = new Vector2(70, 0);
		targeting.AddChild(targetLabel);
		foreach (var mode in Enum.GetValues<TargetMode>())
		{
			var b = UiTheme.Button(mode.ToString(), 62, 26, 12);
			b.ToggleMode = true;
			b.TooltipText = mode switch
			{
				TargetMode.First => "Shoot the creep closest to your keep",
				TargetMode.Last => "Shoot the creep that just entered range",
				TargetMode.Strongest => "Shoot the creep with the most HP (bosses, tanks)",
				_ => "Shoot the creep with the least HP (finish off stragglers)",
			};
			var m = mode;
			b.Pressed += () => Input?.SetTargeting(m);
			targeting.AddChild(b);
			_targetButtons.Add((b, mode));
		}
		text.AddChild(targeting);

		var buttons = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		row.AddChild(buttons);
		_upgrade = UiTheme.PrimaryButton("", 190, 46);
		_upgrade.TooltipText = "Upgrade  [U]";
		_upgrade.Pressed += () => Input?.UpgradeSelected();
		buttons.AddChild(_upgrade);
		_sell = UiTheme.Button("", 190, 34, 13);
		_sell.TooltipText = "Sell for 70% of everything spent on this tower  [Del]";
		_sell.Pressed += () => Input?.SellSelected();
		buttons.AddChild(_sell);
	}

	void UpdateTowerCard()
	{
		var t = Input?.Selected;
		_towerCard.Visible = t != null;
		if (t == null) return;
		_towerImage.Texture = UiTheme.TowerPortrait(t.Def.Id, t.Level);
		_towerName.Text = t.Def.Name;
		_towerLevel.Text = t.MaxLevel ? "MAX LEVEL" : $"Level {t.Level}";
		for (int i = 0; i < _pips.GetChildCount(); i++)
			((ColorRect)_pips.GetChild(i)).Color = i < t.Level ? UiTheme.Accent : new Color(1, 1, 1, 0.12f);
		_towerStats.Text = $"DMG {t.Damage:0}  ·  RNG {t.Range:0.0}  ·  {1 / t.Cooldown:0.0}/s  ·  {(t.Def.Splash > 0 ? "Splash" : "Single")}  ·  {(t.Def.HitsAir ? "Hits air" : t.HitsAir ? $"Hits air ({Catalog.ScatterDamageFor(Me.Level("scatter")) * 100:0}%)" : "Ground only")}";
		if (t.MaxLevel)
		{
			_towerNext.Text = "Fully upgraded.";
			_upgrade.Text = "MAX";
			_upgrade.Disabled = true;
		}
		else
		{
			int next = t.Level + 1;
			bool milestone = Catalog.VisualTier(next) != Catalog.VisualTier(t.Level);
			_towerNext.Text = $"Next: DMG {Catalog.Damage(t.Def, next):0}  ·  RNG {Catalog.Range(t.Def, next):0.0}" + (milestone ? $"   ★ MILESTONE: ×{Catalog.MilestoneDamage(next):0} damage, new look, +range" : "");
			_upgrade.Text = $"UPGRADE   ● {t.UpgradeCost}";
			_upgrade.Disabled = Me.Gold < t.UpgradeCost;
		}
		_sell.Text = $"Sell  +{(int)(t.Invested * Catalog.SellRefund)}";
		_towerRecord.Text = $"Dealt {Short(t.Record.Damage)} damage  ·  {t.Record.Kills} kills";
		foreach (var (b, mode) in _targetButtons) b.ButtonPressed = t.Targeting == mode;
	}

	// ================================================================== build hint

	PanelContainer _hint;
	Label _hintText;

	void BuildHint()
	{
		_hint = new PanelContainer { Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore };
		_hint.AddThemeStyleboxOverride("panel", UiTheme.Panel(UiTheme.Bg, 10, 8));
		_hint.SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
		_hint.GrowHorizontal = Control.GrowDirection.Both;
		_hint.GrowVertical = Control.GrowDirection.Begin;
		_hint.Position -= new Vector2(120, 14);
		_root.AddChild(_hint);
		_hintText = UiTheme.Label("", 14);
		_hint.AddChild(_hintText);
	}

	void UpdateHint()
	{
		var build = Input?.SelectedTower;
		_hint.Visible = Input != null && Input.Selected == null && Match.State != Match.Phase.Over;
		if (!_hint.Visible) return;
		_hintText.Text = build != null
			? $"Placing {Catalog.Tower(build).Name}  ● {Catalog.Tower(build).Cost}   ·   left-click a tile   ·   right-click cancels"
			: "Click a tower to upgrade it   ·   right-drag moves the camera   ·   Esc menu";
	}

	// ================================================================== banner + alerts

	Label _banner, _alert;

	void BuildBanner()
	{
		_banner = UiTheme.Label("", 34, bold: true, align: HorizontalAlignment.Center);
		_banner.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
		_banner.GrowHorizontal = Control.GrowDirection.Both;
		_banner.Position += new Vector2(0, 110);
		_banner.AddThemeConstantOverride("outline_size", 10);
		_banner.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.7f));
		_banner.Modulate = Colors.Transparent;
		_root.AddChild(_banner);

		_alert = UiTheme.Label("", 18, UiTheme.Bad, bold: true);
		_alert.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
		_alert.Position = new Vector2(20, 64);
		_alert.AddThemeConstantOverride("outline_size", 6);
		_alert.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.7f));
		_alert.Modulate = Colors.Transparent;
		_root.AddChild(_alert);
	}

	void Flash(Label label, string text, Color color, float hold)
	{
		label.Text = text;
		label.AddThemeColorOverride("font_color", color);
		var tw = label.CreateTween();
		tw.SetIgnoreTimeScale();
		tw.TweenProperty(label, "modulate", Colors.White, 0.2f).From(Colors.Transparent);
		tw.TweenInterval(hold);
		tw.TweenProperty(label, "modulate", Colors.Transparent, 0.6f);
	}

	void OnWaveStarting()
	{
		var theme = Catalog.ThemeFor(Match.Wave);
		if (Catalog.IsBossWave(Match.Wave))
			Flash(_banner, $"BOSS WAVE {Match.Wave}\n{Catalog.Unit(theme.Boss).Name}", UiTheme.Bad, 1.6f);
		else
			Flash(_banner, $"Wave {Match.Wave}  ·  {theme.Name}", UiTheme.Text, 1.0f);
	}

	void OnLeaked(int lane, Enemy e)
	{
		if (lane != Player) return;
		Flash(_alert, $"−{e.Def.Lives} ♥   {e.Def.Name} got through!", UiTheme.Bad, 1.2f);
	}

	// ================================================================== pause menu

	Control _pause;

	void BuildPause()
	{
		_pause = Overlay(out var box, "Paused");
		var resume = UiTheme.PrimaryButton("Resume", 260, 48);
		resume.Pressed += () => SetPaused(false);
		box.AddChild(resume);
		var restart = UiTheme.Button("Restart match", 260, 42);
		restart.Pressed += Restart;
		box.AddChild(restart);
		var edge = new CheckButton { Text = "Edge scrolling", FocusMode = Control.FocusModeEnum.None, ButtonPressed = CameraRig.EdgeScroll };
		edge.Toggled += on => CameraRig.EdgeScroll = on;
		box.AddChild(edge);
		var numbers = new CheckButton { Text = "Damage numbers", FocusMode = Control.FocusModeEnum.None, ButtonPressed = FloatingText.Enabled };
		numbers.Toggled += on => FloatingText.Enabled = on;
		box.AddChild(numbers);
		box.AddChild(UiTheme.Label(
			"Camera: right- or middle-drag · WASD · mouse wheel zoom\n" +
			"Build: 1–3 towers · U upgrade · G targeting · Del sell · Space start wave · F speed\n" +
			"Panels: Q Buildings · R Upgrades · T Army · I Intel", 12, UiTheme.Muted));
		var quit = UiTheme.Button("Quit to desktop", 260, 42);
		quit.Pressed += () => GetTree().Quit();
		box.AddChild(quit);
	}

	void SetPaused(bool paused)
	{
		GetTree().Paused = paused;
		_pause.Visible = paused;
	}

	void Restart()
	{
		GetTree().Paused = false;
		Engine.TimeScale = 1;
		GetTree().ReloadCurrentScene();
	}

	// Dimmed full-screen overlay with a centred panel.
	Control Overlay(out VBoxContainer box, string title)
	{
		var dim = new ColorRect { Color = new Color(0, 0, 0, 0.55f), Visible = false };
		dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_root.AddChild(dim);
		var center = new CenterContainer();
		center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		dim.AddChild(center);
		var panel = new PanelContainer();
		panel.AddThemeStyleboxOverride("panel", UiTheme.Panel(UiTheme.Bg, 16, 24));
		center.AddChild(panel);
		box = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		box.AddThemeConstantOverride("separation", 12);
		panel.AddChild(box);
		box.AddChild(UiTheme.Label(title, 30, UiTheme.Accent, bold: true, align: HorizontalAlignment.Center));
		return dim;
	}

	// ================================================================== game over

	// End-of-match screen: result, then three columns — economy comparison, your towers by damage,
	// and what leaked on each side. Built when the match ends.

	Control _gameOver;
	Label _resultTitle, _resultText;
	HBoxContainer _statsColumns;

	void BuildGameOver()
	{
		_gameOver = Overlay(out var box, "");
		_resultTitle = (Label)box.GetChild(0);
		_resultTitle.AddThemeFontSizeOverride("font_size", 44);
		_resultText = UiTheme.Label("", 16, UiTheme.Muted, align: HorizontalAlignment.Center);
		box.AddChild(_resultText);
		box.AddChild(UiTheme.Divider());
		_statsColumns = new HBoxContainer();
		_statsColumns.AddThemeConstantOverride("separation", 28);
		box.AddChild(_statsColumns);
		box.AddChild(UiTheme.Divider());
		var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		buttons.AddThemeConstantOverride("separation", 12);
		box.AddChild(buttons);
		var again = UiTheme.PrimaryButton("Play again", 240, 50);
		again.Pressed += Restart;
		buttons.AddChild(again);
		var quit = UiTheme.Button("Quit to desktop", 200, 50);
		quit.Pressed += () => GetTree().Quit();
		buttons.AddChild(quit);
	}

	void ShowGameOver()
	{
		_gameOver.Visible = true;
		bool won = Match.Winner == Player, draw = Match.Winner < 0;
		_resultTitle.Text = won ? "VICTORY" : draw ? "DRAW" : "DEFEAT";
		_resultTitle.AddThemeColorOverride("font_color", won ? UiTheme.Accent : draw ? UiTheme.Text : UiTheme.Bad);
		_resultText.Text = $"Wave {Match.Wave} of {Catalog.FinalWave}   ·   {Me.Name} ♥ {Me.Lives}   ·   {Them.Name} ♥ {Them.Lives}";

		foreach (var c in _statsColumns.GetChildren()) c.QueueFree();
		_statsColumns.AddChild(EconomyColumn());
		_statsColumns.AddChild(TowersColumn());
		_statsColumns.AddChild(LeaksColumn());
	}

	static VBoxContainer StatsColumn(string title, float width)
	{
		var col = new VBoxContainer { CustomMinimumSize = new Vector2(width, 0) };
		col.AddThemeConstantOverride("separation", 4);
		col.AddChild(UiTheme.Label(title, 17, UiTheme.Accent, bold: true));
		return col;
	}

	// A row of cells with fixed widths; the first is left-aligned, the rest right-aligned.
	static HBoxContainer Row(float[] widths, Color? color, params string[] cells)
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 6);
		for (int i = 0; i < cells.Length; i++)
		{
			var l = UiTheme.Label(cells[i], 13, color, align: i == 0 ? HorizontalAlignment.Left : HorizontalAlignment.Right);
			l.CustomMinimumSize = new Vector2(widths[i], 0);
			l.ClipText = true;
			row.AddChild(l);
		}
		return row;
	}

	Control EconomyColumn()
	{
		var col = StatsColumn("Economy", 300);
		float[] w = { 140, 72, 72 };
		col.AddChild(Row(w, UiTheme.Muted, "", "You", "Opponent"));
		PlayerStats a = Me.Stats, b = Them.Stats;
		void Line(string name, int x, int y) => col.AddChild(Row(w, null, name, x.ToString("N0"), y.ToString("N0")));
		Line("Kill bounty", a.BountyEarned, b.BountyEarned);
		Line("Income earned", a.IncomeEarned, b.IncomeEarned);
		Line("Boss bonuses", a.BossBonusEarned, b.BossBonusEarned);
		Line("War Chest interest", a.InterestEarned, b.InterestEarned);
		Line("Spent on towers", a.SpentTowers, b.SpentTowers);
		Line("Spent on upgrades", a.SpentUpgrades, b.SpentUpgrades);
		Line("Spent on research", a.SpentResearch, b.SpentResearch);
		Line("Spent on sends", a.SpentSends, b.SpentSends);
		Line("Units sent", a.UnitsSent, b.UnitsSent);
		Line("Final income", Match.Income(Me), Match.Income(Them));
		col.AddChild(Row(w, null, "Total damage", Short(a.Towers.Sum(t => t.Damage)), Short(b.Towers.Sum(t => t.Damage))));
		col.AddChild(Row(w, null, "Kills", a.Towers.Sum(t => t.Kills).ToString("N0"), b.Towers.Sum(t => t.Kills).ToString("N0")));
		return col;
	}

	Control TowersColumn()
	{
		var col = StatsColumn("Your towers by damage", 360);
		float[] w = { 150, 60, 70, 50 };
		col.AddChild(Row(w, UiTheme.Muted, "Tower", "Level", "Damage", "Kills"));
		var towers = Me.Stats.Towers.OrderByDescending(t => t.Damage).ToList();
		float total = Mathf.Max(1, towers.Sum(t => t.Damage));
		foreach (var t in towers.Take(10))
		{
			var name = $"{t.Def.Name} ({t.Cell.X},{t.Cell.Y}){(t.Sold ? " sold" : "")}";
			col.AddChild(Row(w, t.Sold ? UiTheme.Muted : null, name, t.Level.ToString(), $"{Short(t.Damage)}", t.Kills.ToString()));
			var bar = new ProgressBar { ShowPercentage = false, MaxValue = total, Value = t.Damage, CustomMinimumSize = new Vector2(340, 4) };
			col.AddChild(bar);
		}
		if (towers.Count > 10) col.AddChild(UiTheme.Label($"+ {towers.Count - 10} more", 12, UiTheme.Muted));
		if (towers.Count == 0) col.AddChild(UiTheme.Label("No towers built.", 13, UiTheme.Muted));
		return col;
	}

	Control LeaksColumn()
	{
		var col = StatsColumn("Leaks", 300);
		foreach (var (who, stats) in new[] { ("You", Me.Stats), (Them.Name, Them.Stats) })
		{
			int lives = stats.Leaks.Values.Sum(v => v.lives);
			col.AddChild(UiTheme.Label($"{who} — {lives} lives lost", 14, lives > 0 ? UiTheme.Bad : UiTheme.Good, bold: true));
			float[] w = { 180, 50, 60 };
			foreach (var (unit, (count, l)) in stats.Leaks.OrderByDescending(kv => kv.Value.lives).Take(6))
				col.AddChild(Row(w, null, Catalog.Unit(unit).Name, $"×{count}", $"−{l} ♥"));
			if (stats.Leaks.Count == 0) col.AddChild(UiTheme.Label("Nothing got through.", 13, UiTheme.Muted));
			col.AddChild(new Control { CustomMinimumSize = new Vector2(0, 8) });
		}
		return col;
	}

	static string Short(float v) => v >= 1_000_000 ? $"{v / 1_000_000:0.0}M" : v >= 10_000 ? $"{v / 1000:0}k" : v >= 1000 ? $"{v / 1000:0.0}k" : $"{v:0}";

	// Bounty pops up in gold where your creep died (only on your lane, to keep the screen readable).
	void OnKilled(int lane, Enemy e, int gold)
	{
		if (lane != Player) return;
		FloatingText.Spawn(Match.Lanes[lane], e.Position + new Vector3(0.25f, e.Def.Height + 0.1f, 0), $"+{gold}", FloatingText.GoldColor, 30);
	}

	// ================================================================== gold popup

	Label _goldPopup;

	void OnBuildStarted()
	{
		if (Match.Wave <= 1) return;
		_goldPopup ??= MakeGoldPopup();
		var chip = (Control)_meGold.GetParent().GetParent();
		_goldPopup.Position = chip.GlobalPosition + new Vector2(4, chip.Size.Y + 2);
		int income = Match.Income(Me), interest = Me.LastInterest, boss = Me.LastBossBonus;
		var parts = new System.Collections.Generic.List<string> { $"income {income}" };
		if (interest > 0) parts.Add($"chest {interest}");
		if (boss > 0) parts.Add($"boss prep {boss}");
		_goldPopup.Text = parts.Count > 1 ? $"+{income + interest + boss}  ({string.Join(" · ", parts)})" : $"+{income}";
		var tw = _goldPopup.CreateTween();
		tw.SetIgnoreTimeScale();
		tw.TweenProperty(_goldPopup, "modulate", Colors.White, 0.15f).From(Colors.Transparent);
		tw.Parallel().TweenProperty(_goldPopup, "position:y", _goldPopup.Position.Y + 14, 1.6f).SetEase(Tween.EaseType.Out);
		tw.TweenProperty(_goldPopup, "modulate", Colors.Transparent, 0.6f);
	}

	Label MakeGoldPopup()
	{
		var l = UiTheme.Label("", 16, UiTheme.Gold, bold: true);
		l.AddThemeConstantOverride("outline_size", 6);
		l.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.8f));
		l.Modulate = Colors.Transparent;
		l.MouseFilter = Control.MouseFilterEnum.Ignore;
		_root.AddChild(l);
		return l;
	}

	// ================================================================== keys

	public override void _UnhandledInput(InputEvent e)
	{
		if (e is not InputEventKey { Pressed: true, Echo: false } key) return;
		if (Input == null) return; // AI-only match (autotest): nobody to take keys from
		switch (key.Keycode)
		{
			case Key.Escape:
				if (_pause.Visible) SetPaused(false);
				else if (_openFlyout != null) ToggleFlyout(_openFlyout);
				else if (Input != null && (Input.Selected != null || Input.SelectedTower != null)) Input.ClearSelection();
				else if (!_gameOver.Visible) SetPaused(true);
				break;
			case Key.Q: ToggleFlyout("Buildings"); break;
			case Key.R: ToggleFlyout("Upgrades"); break;
			case Key.T: ToggleFlyout("Army"); break;
			case Key.I: ToggleFlyout("Intel"); break;
			case Key.F:
				int i = Array.FindIndex(Speeds, s => Mathf.IsEqualApprox(s, (float)Engine.TimeScale));
				SetSpeed(Speeds[(i + 1) % Speeds.Length]);
				break;
			default: return;
		}
		GetViewport().SetInputAsHandled();
	}
}
