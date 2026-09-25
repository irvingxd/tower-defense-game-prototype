using System.Collections.Generic;
using Godot;

namespace TowerDefense.UI;

// One place for the look: palette, fonts, panel/button styles, and small widget helpers.
public static class UiTheme
{
	public static readonly Color Bg = new(0.07f, 0.09f, 0.13f, 0.94f);
	public static readonly Color BgSoft = new(0.11f, 0.14f, 0.2f, 0.96f);
	public static readonly Color Line = new(0.2f, 0.25f, 0.34f);
	public static readonly Color Accent = new(0.95f, 0.76f, 0.3f);
	public static readonly Color Text = new(0.92f, 0.94f, 0.97f);
	public static readonly Color Muted = new(0.6f, 0.65f, 0.72f);
	public static readonly Color Good = new(0.45f, 0.85f, 0.45f);
	public static readonly Color Bad = new(0.92f, 0.38f, 0.36f);
	public static readonly Color Info = new(0.45f, 0.68f, 1f);
	public static readonly Color Gold = new(1f, 0.82f, 0.3f);
	public static readonly Color Heart = new(0.95f, 0.35f, 0.4f);

	public static Font Regular { get; } = new SystemFont { FontNames = new[] { "Segoe UI", "Arial" }, FontWeight = 500 };
	public static Font Bold { get; } = new SystemFont { FontNames = new[] { "Segoe UI", "Arial" }, FontWeight = 700 };

	public static Theme Build()
	{
		var t = new Theme { DefaultFont = Regular, DefaultFontSize = 15 };

		t.SetStylebox("panel", "PanelContainer", Panel());
		t.SetStylebox("panel", "TooltipPanel", Panel(BgSoft, 8, 8));
		t.SetColor("font_color", "TooltipLabel", Text);
		t.SetColor("font_color", "Label", Text);

		t.SetStylebox("normal", "Button", ButtonBox(new Color(0.13f, 0.17f, 0.24f), Line));
		t.SetStylebox("hover", "Button", ButtonBox(new Color(0.17f, 0.22f, 0.31f), Accent with { A = 0.55f }));
		t.SetStylebox("pressed", "Button", ButtonBox(new Color(0.25f, 0.2f, 0.09f), Accent));
		t.SetStylebox("hover_pressed", "Button", ButtonBox(new Color(0.3f, 0.24f, 0.1f), Accent));
		t.SetStylebox("disabled", "Button", ButtonBox(new Color(0.09f, 0.11f, 0.15f), new Color(0.15f, 0.18f, 0.24f)));
		t.SetStylebox("focus", "Button", new StyleBoxEmpty());
		t.SetColor("font_color", "Button", Text);
		t.SetColor("font_hover_color", "Button", Colors.White);
		t.SetColor("font_pressed_color", "Button", Accent);
		t.SetColor("font_hover_pressed_color", "Button", Accent);
		t.SetColor("font_disabled_color", "Button", Muted with { A = 0.55f });
		t.SetColor("icon_disabled_color", "Button", new Color(1, 1, 1, 0.35f));

		t.SetStylebox("background", "ProgressBar", Rounded(new Color(0, 0, 0, 0.45f), 4));
		t.SetStylebox("fill", "ProgressBar", Rounded(Accent, 4));
		t.SetConstant("separation", "HBoxContainer", 8);
		t.SetConstant("separation", "VBoxContainer", 6);
		return t;
	}

	public static StyleBoxFlat Panel(Color? bg = null, int radius = 12, int margin = 12)
	{
		var s = Rounded(bg ?? Bg, radius);
		s.BorderColor = Line;
		s.SetBorderWidthAll(1);
		s.ShadowColor = new Color(0, 0, 0, 0.35f);
		s.ShadowSize = 8;
		s.SetContentMarginAll(margin);
		return s;
	}

	public static StyleBoxFlat Rounded(Color c, int radius)
	{
		var s = new StyleBoxFlat { BgColor = c };
		s.SetCornerRadiusAll(radius);
		return s;
	}

	static StyleBoxFlat ButtonBox(Color bg, Color border)
	{
		var s = Rounded(bg, 8);
		s.BorderColor = border;
		s.SetBorderWidthAll(1);
		s.SetContentMarginAll(8);
		return s;
	}

	// ------------------------------------------------------------------ helpers

	static readonly Dictionary<string, Texture2D> TexCache = new();

	public static Texture2D Tex(string path)
	{
		if (!TexCache.TryGetValue(path, out var tex))
			TexCache[path] = tex = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
		return tex;
	}

	public static Texture2D Icon(string name) => Tex($"res://assets/ui/icons/{name}.png");
	public static Texture2D UnitPortrait(string unitId) => Tex($"res://assets/ui/portraits/unit-{unitId}.png");
	public static Texture2D TowerPortrait(string towerId, int level) =>
		Tex($"res://assets/ui/portraits/tower-{towerId}-{(level >= 10 ? 10 : 1)}.png");

	public static Label Label(string text = "", int size = 15, Color? color = null, bool bold = false,
		HorizontalAlignment align = HorizontalAlignment.Left)
	{
		var l = new Label { Text = text, HorizontalAlignment = align, VerticalAlignment = VerticalAlignment.Center };
		l.AddThemeFontSizeOverride("font_size", size);
		if (bold) l.AddThemeFontOverride("font", Bold);
		if (color is { } c) l.AddThemeColorOverride("font_color", c);
		return l;
	}

	public static TextureRect Image(Texture2D tex, float size) => new()
	{
		Texture = tex, CustomMinimumSize = new Vector2(size, size),
		ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
		MouseFilter = Control.MouseFilterEnum.Ignore,
	};

	// A small "icon value" pill for the top bar.
	public static (PanelContainer chip, Label value) Chip(string glyph, Color glyphColor, string tooltip)
	{
		var chip = new PanelContainer { TooltipText = tooltip, MouseFilter = Control.MouseFilterEnum.Pass };
		chip.AddThemeStyleboxOverride("panel", Flat(Panel(new Color(1, 1, 1, 0.05f), 8, 6)));
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 6);
		chip.AddChild(row);
		row.AddChild(Label(glyph, 17, glyphColor, bold: true));
		var value = Label("", 17, bold: true);
		row.AddChild(value);
		return (chip, value);
	}

	static StyleBoxFlat Flat(StyleBoxFlat s)
	{
		s.ShadowSize = 0;
		s.BorderColor = new Color(1, 1, 1, 0.06f);
		s.ContentMarginLeft = s.ContentMarginRight = 10;
		s.ContentMarginTop = s.ContentMarginBottom = 4;
		return s;
	}

	public static Button Button(string text, float minWidth = 0, float minHeight = 36, int fontSize = 15)
	{
		var b = new Button { Text = text, CustomMinimumSize = new Vector2(minWidth, minHeight), FocusMode = Control.FocusModeEnum.None };
		b.AddThemeFontSizeOverride("font_size", fontSize);
		return b;
	}

	// Big gold call-to-action button.
	public static Button PrimaryButton(string text, float minWidth, float minHeight)
	{
		var b = Button(text, minWidth, minHeight, 19);
		b.AddThemeFontOverride("font", Bold);
		var normal = ButtonBox(new Color(0.85f, 0.62f, 0.18f), new Color(1f, 0.85f, 0.45f));
		var hover = ButtonBox(new Color(0.95f, 0.72f, 0.24f), Colors.White);
		b.AddThemeStyleboxOverride("normal", normal);
		b.AddThemeStyleboxOverride("hover", hover);
		b.AddThemeStyleboxOverride("pressed", ButtonBox(new Color(0.7f, 0.5f, 0.12f), Colors.White));
		b.AddThemeColorOverride("font_color", new Color(0.12f, 0.08f, 0.02f));
		b.AddThemeColorOverride("font_hover_color", new Color(0.12f, 0.08f, 0.02f));
		b.AddThemeColorOverride("font_pressed_color", new Color(0.12f, 0.08f, 0.02f));
		return b;
	}

	public static HSeparator Divider()
	{
		var sep = new HSeparator();
		var line = new StyleBoxLine { Color = Line, Thickness = 1 };
		sep.AddThemeStyleboxOverride("separator", line);
		return sep;
	}
}
