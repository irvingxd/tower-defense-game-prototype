using System.Collections.Generic;
using Godot;

namespace TowerDefense.Game;

// Recolours Kenney TD Kit models by swapping swatches in their shared palette texture (colormap.png:
// 16 x 4 swatches of 32 x 128 px, each a vertical gradient). Unlike a multiply overlay this keeps the
// shading and can make things lighter, not only darker. Swatch use by the tower pieces:
//   stone  (13,3) (9,3)   – walls and trim on every piece
//   accent (15,2) (14,2)  – round towers' red trim / roofs;  (5,1) (4,1) – square towers' purple
//   wood   (1,2) (0,2)    – weapons (ballista, catapult, cannon carriage)
//   gem    (7,1) (6,1)    – the crystal tower's crystals; (8,2) (9,2) greens of the detail crystals
public sealed record TowerPalette(string Name, Color? Stone = null, Color? Accent = null, Color? Wood = null, Color? Gem = null)
{
	public static readonly TowerPalette Classic = new("Classic");

	public static readonly TowerPalette[] All =
	{
		Classic,
		new("Royal", Stone: new(0.93f, 0.9f, 0.82f), Accent: new(0.22f, 0.36f, 0.85f), Wood: new(0.95f, 0.75f, 0.25f)),
		new("Ember", Stone: new(0.3f, 0.28f, 0.3f), Accent: new(1f, 0.45f, 0.1f), Wood: new(0.35f, 0.22f, 0.18f), Gem: new(1f, 0.35f, 0.1f)),
		new("Verdant", Stone: new(0.62f, 0.66f, 0.55f), Accent: new(0.3f, 0.65f, 0.3f), Wood: new(0.55f, 0.4f, 0.25f), Gem: new(0.4f, 1f, 0.45f)),
		new("Frost", Stone: new(0.9f, 0.95f, 1f), Accent: new(0.45f, 0.75f, 1f), Wood: new(0.7f, 0.8f, 0.9f), Gem: new(0.55f, 0.9f, 1f)),
		new("Sandstone", Stone: new(0.9f, 0.78f, 0.58f), Accent: new(0.1f, 0.6f, 0.6f), Wood: new(0.6f, 0.38f, 0.2f), Gem: new(0.2f, 0.85f, 0.8f)),
		new("Necro", Stone: new(0.33f, 0.3f, 0.4f), Accent: new(0.55f, 0.95f, 0.2f), Wood: new(0.3f, 0.3f, 0.25f), Gem: new(0.6f, 1f, 0.25f)),
	};

	const int SwatchW = 32, SwatchH = 128;

	static Image _source;
	static readonly Dictionary<string, ImageTexture> Textures = new();
	static readonly Dictionary<(string, Material), Material> Materials = new();

	// Recolours every mesh under root (TD Kit models only; other kits have their own palette layout).
	public void Apply(Node root)
	{
		if (this == Classic) return;
		var tex = Texture();
		Walk(root, tex);
	}

	void Walk(Node n, ImageTexture tex)
	{
		if (n is MeshInstance3D mi && mi.Mesh != null)
		{
			for (int s = 0; s < mi.Mesh.GetSurfaceCount(); s++)
			{
				if (mi.Mesh.SurfaceGetMaterial(s) is not BaseMaterial3D src) continue;
				if (!Materials.TryGetValue((Name, src), out var mat))
				{
					var m = (BaseMaterial3D)src.Duplicate();
					m.AlbedoTexture = tex;
					Materials[(Name, src)] = mat = m;
				}
				mi.SetSurfaceOverrideMaterial(s, mat);
			}
		}
		foreach (var c in n.GetChildren()) Walk(c, tex);
	}

	ImageTexture Texture()
	{
		if (Textures.TryGetValue(Name, out var tex)) return tex;
		if (_source == null)
		{
			_source = GD.Load<Texture2D>("res://assets/td/Textures/colormap.png").GetImage();
			if (_source.IsCompressed()) _source.Decompress();
			_source.Convert(Image.Format.Rgba8);
		}
		var img = (Image)_source.Duplicate();
		Swap(img, Stone, (13, 3), (9, 3));
		Swap(img, Accent, (15, 2), (14, 2), (5, 1), (4, 1));
		Swap(img, Wood, (1, 2), (0, 2));
		Swap(img, Gem, (7, 1), (6, 1), (8, 2), (9, 2));
		return Textures[Name] = ImageTexture.CreateFromImage(img);
	}

	// Gives a swatch the target hue and saturation while keeping its gradient: each pixel's brightness
	// relative to the swatch's brightest pixel is carried over onto the target colour.
	static void Swap(Image img, Color? target, params (int col, int row)[] swatches)
	{
		if (target is not { } t) return;
		foreach (var (col, row) in swatches)
		{
			int x0 = col * SwatchW, y0 = row * SwatchH;
			float peak = 0.001f;
			for (int y = 0; y < SwatchH; y++)
			for (int x = 0; x < SwatchW; x++)
				peak = Mathf.Max(peak, img.GetPixel(x0 + x, y0 + y).V);
			for (int y = 0; y < SwatchH; y++)
			for (int x = 0; x < SwatchW; x++)
			{
				float k = img.GetPixel(x0 + x, y0 + y).V / peak;
				img.SetPixel(x0 + x, y0 + y, Color.FromHsv(t.H, t.S, Mathf.Clamp(t.V * k, 0, 1)));
			}
		}
	}
}
