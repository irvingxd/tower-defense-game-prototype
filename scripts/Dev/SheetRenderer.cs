using System;
using System.Collections.Generic;
using Godot;
using TowerDefense.Game;

namespace TowerDefense.Dev;

// `-- --sheet <parts|branches|concepts|palettes|roster|variants> <out.png>`: renders a labelled grid of models (a parts catalogue, or the
// proposed tower-branch looks) to a PNG and quits. Design tool only; nothing in the game uses it.
public partial class SheetRenderer : Node3D
{
	const float Spacing = 2.3f;

	public static bool TryAttach(Node parent)
	{
		var args = OS.GetCmdlineUserArgs();
		int i = Array.IndexOf(args, "--sheet");
		if (i < 0 || i + 2 >= args.Length) return false;
		parent.AddChild(new SheetRenderer { _mode = args[i + 1], _out = args[i + 2] });
		return true;
	}

	string _mode, _out;

	// Stack: placed on the current top and raises it. Top: sits on the current top. Free: Offset is absolute.
	public enum PartMode { Stack, Top, Free }

	// One cell: a caption and the parts to stack/place, each (source, model, mode, offset, rotation Y, scale, tint).
	public sealed record Part(string Kit, string Model, PartMode Mode = PartMode.Stack, Vector3 Offset = default, float RotY = 0, float Scale = 1, Color? Tint = null, TowerPalette Palette = null);
	public sealed record Entry(string Caption, List<Part> Parts, string Sub = "");

	public override async void _Ready()
	{
		var entries = _mode switch
		{
			"branches" => BranchSheet.Entries(),
			"concepts" => ConceptSheet.Concepts(),
			"palettes" => ConceptSheet.Palettes(),
			"roster" => RosterSheet.Roster(),
			"variants" => RosterSheet.Variants(),
			_ => PartsCatalogue(),
		};
		// Everything but the parts catalogue uses the tilted, billboard-labelled layout.
		bool branches = _mode != "parts";
		int cols = _mode switch { "branches" => 8, "concepts" => 6, "palettes" => TowerPalette.All.Length, "roster" => 7, "variants" => 9, _ => 9 };
		float rowSpacing = branches ? 8.5f : Spacing * 1.25f, pitch = branches ? 25f : 49f;
		for (int i = 0; i < entries.Count; i++)
		{
			var e = entries[i];
			var cellPos = new Vector3((i % cols) * Spacing, 0, (i / cols) * rowSpacing);
			var root = new Node3D { Position = cellPos };
			AddChild(root);
			float top = 0;
			foreach (var p in e.Parts)
			{
				var n = Build(p);
				if (p.Mode != PartMode.Free) n.Position += new Vector3(0, top, 0);
				root.AddChild(n);
				if (p.Mode == PartMode.Stack) top += Models.Measure(n).End.Y * p.Scale;
			}
			AddChild(new Label3D
			{
				Text = e.Caption + (e.Sub != "" ? "\n" + e.Sub : ""), FontSize = branches ? 40 : 30, PixelSize = branches ? 0.0065f : 0.0045f, OutlineSize = 0,
				Modulate = new Color(0.1f, 0.1f, 0.12f), Position = cellPos + (branches ? new Vector3(0, 0.05f, 1.15f) : new Vector3(0, 0.02f, 0.95f)),
				RotationDegrees = branches ? Vector3.Zero : new Vector3(-90, 0, 0), Billboard = branches ? BaseMaterial3D.BillboardModeEnum.Enabled : BaseMaterial3D.BillboardModeEnum.Disabled,
				HorizontalAlignment = HorizontalAlignment.Center,
			});
		}

		int rows = (entries.Count + cols - 1) / cols;
		var center = new Vector3((cols - 1) * Spacing / 2, branches ? 1.5f : 0.6f, (rows - 1) * rowSpacing / 2 + 0.3f);
		float pitchRad = Mathf.DegToRad(pitch);
		var cam = new Camera3D { Projection = Camera3D.ProjectionType.Orthogonal, Size = branches ? Mathf.Max((rows - 1) * rowSpacing * Mathf.Sin(pitchRad) + 6.2f, cols * Spacing / 1.75f + 0.6f) : rows * Spacing * 1.25f + 1.2f, Far = 200 };
		AddChild(cam);
		cam.Position = center + new Vector3(0, Mathf.Sin(pitchRad), Mathf.Cos(pitchRad)) * 40;
		cam.LookAt(center);
		AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-50, -35, 0), ShadowEnabled = true, LightEnergy = 1.1f });
		AddChild(new WorldEnvironment
		{
			Environment = new Godot.Environment
			{
				BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = new Color(0.9f, 0.92f, 0.9f),
				AmbientLightSource = Godot.Environment.AmbientSource.Color, AmbientLightColor = Colors.White, AmbientLightEnergy = 0.6f,
			},
		});
		var floor = new MeshInstance3D
		{
			Mesh = new PlaneMesh { Size = new Vector2(cols * Spacing + 8, rows * rowSpacing + 8) },
			MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.85f, 0.88f, 0.85f) },
			Position = new Vector3(center.X, -0.001f, center.Z),
		};
		AddChild(floor);

		for (int i = 0; i < 6; i++) await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		GetViewport().GetTexture().GetImage().SavePng(_out);
		GetTree().Quit();
	}

	static Node3D Build(Part p)
	{
		var n = p.Kit switch
		{
			"castle" => Models.Castle(p.Model),
			_ => Models.Td(p.Model),
		};
		n.Position = p.Offset;
		n.RotationDegrees = new Vector3(0, p.RotY, 0);
		n.Scale = Vector3.One * p.Scale;
		p.Palette?.Apply(n);
		if (p.Tint is { } tint) TintAll(n, tint);
		return n;
	}

	static void TintAll(Node n, Color tint)
	{
		if (n is GeometryInstance3D g)
			g.MaterialOverlay = new StandardMaterial3D
			{
				AlbedoColor = tint, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
				Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Mul,
			};
		foreach (var c in n.GetChildren()) TintAll(c, tint);
	}

	static List<Entry> PartsCatalogue()
	{
		var list = new List<Entry>();
		void Add(string kit, params string[] names)
		{
			foreach (var name in names) list.Add(new Entry(name, new List<Part> { new(kit, name) }));
		}
		Add("castle", "siege-ballista", "siege-catapult", "siege-trebuchet", "siege-ram", "siege-tower",
			"tower-hexagon-base", "tower-hexagon-mid", "tower-hexagon-top", "tower-hexagon-top-wood", "tower-hexagon-roof",
			"tower-hexagon-roof-secondary", "tower-base", "tower-top", "tower-square-base", "tower-square-mid", "tower-square-top",
			"tower-square-top-roof", "tower-square-top-roof-high", "tower-square-top-roof-rounded", "tower-slant-roof",
			"flag", "flag-pennant", "flag-wide", "flag-banner-long", "flag-banner-short");
		Add("td", "tower-round-roof-a", "tower-round-roof-b", "tower-round-roof-c", "tower-square-roof-b", "tower-square-roof-c",
			"tower-round-crystals", "weapon-turret", "weapon-ammo-bullet", "tower-round-base", "tower-round-bottom-b",
			"tower-square-top-a", "tower-square-top-c", "tower-round-build-c", "wood-structure-high");
		return list;
	}
}
