using System.Collections.Generic;
using Godot;

namespace TowerDefense.Game;

public static class Models
{
	static readonly Dictionary<string, PackedScene> Cache = new();

	public static Node3D Td(string name) => Load($"res://assets/td/{name}.glb");
	public static Node3D Graveyard(string name) => Load($"res://assets/graveyard/{name}.glb");
	public static Node3D Castle(string name) => Load($"res://assets/castle/{name}.glb");
	public static Node3D Monster(string name)
	{
		var model = Load($"res://assets/monsters/{name}.glb");
		Matte(model);
		return model;
	}

	static Node3D Load(string path)
	{
		if (!Cache.TryGetValue(path, out var scene))
			Cache[path] = scene = GD.Load<PackedScene>(path);
		return scene.Instantiate<Node3D>();
	}

	static readonly Dictionary<Material, Material> MatteCache = new();

	// Quaternius monsters ship glossy/metallic; flatten them to match Kenney's matte look.
	static void Matte(Node n)
	{
		if (n is MeshInstance3D mi && mi.Mesh != null)
		{
			for (int s = 0; s < mi.Mesh.GetSurfaceCount(); s++)
			{
				if (mi.Mesh.SurfaceGetMaterial(s) is not BaseMaterial3D src) continue;
				if (!MatteCache.TryGetValue(src, out var matte))
				{
					var m = (BaseMaterial3D)src.Duplicate();
					m.Metallic = 0;
					m.Roughness = 1;
					m.MetallicSpecular = 0.2f;
					MatteCache[src] = matte = m;
				}
				mi.SetSurfaceOverrideMaterial(s, matte);
			}
		}
		foreach (var c in n.GetChildren()) Matte(c);
	}

	// Bounds of all meshes under root, in root's local space (works before the node enters the tree).
	public static Aabb Measure(Node3D root)
	{
		Aabb? acc = null;
		Walk(root, root, Transform3D.Identity, true, ref acc);
		return acc ?? new Aabb();
	}

	static void Walk(Node root, Node n, Transform3D xf, bool isRoot, ref Aabb? acc)
	{
		if (!isRoot && n is Node3D n3) xf = xf * n3.Transform;
		if (n is VisualInstance3D vi)
		{
			var b = SkinnedBounds(root, vi) ?? xf * vi.GetAabb();
			acc = acc == null ? b : acc.Value.Merge(b);
		}
		foreach (var c in n.GetChildren()) Walk(root, c, xf, false, ref acc);
	}

	// Skinned glTF meshes are positioned purely by their bones (the mesh node's own transform, often a
	// stray 100x scale, is ignored at render time), so skin the vertices at rest pose to get real bounds.
		static Aabb? SkinnedBounds(Node root, VisualInstance3D vi)
	{
		if (vi is not MeshInstance3D { Skin: { } skin, Mesh: { } mesh } mi || skin.GetBindCount() == 0) return null;
		if (mi.GetNodeOrNull(mi.Skeleton) is not Skeleton3D skel) return null;
		var skelXf = FromRoot(root, skel);

		var binds = new Transform3D[skin.GetBindCount()];
		for (int j = 0; j < binds.Length; j++)
		{
			int bone = skin.GetBindBone(j);
			if (bone < 0) bone = skel.FindBone(skin.GetBindName(j));
			binds[j] = bone < 0 ? Transform3D.Identity : skel.GetBoneGlobalRest(bone) * skin.GetBindPose(j);
		}

		Aabb? acc = null;
		for (int s = 0; s < mesh.GetSurfaceCount(); s++)
		{
			var arrays = mesh.SurfaceGetArrays(s);
			var verts = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
			var bones = arrays[(int)Mesh.ArrayType.Bones].AsInt32Array();
			var weights = arrays[(int)Mesh.ArrayType.Weights].AsFloat32Array();
			if (verts.Length == 0 || bones.Length < verts.Length) continue;
			int per = bones.Length / verts.Length;
			for (int v = 0; v < verts.Length; v += 3) // every 3rd vertex is plenty for bounds
			{
				int best = 0;
				for (int k = 1; k < per; k++)
					if (weights[v * per + k] > weights[v * per + best]) best = k;
				int bind = bones[v * per + best];
				if (bind < 0 || bind >= binds.Length) continue;
				var p = skelXf * (binds[bind] * verts[v]);
				acc = acc == null ? new Aabb(p, Vector3.Zero) : acc.Value.Expand(p);
			}
		}
		return acc;
	}

	// Accumulated transform of n relative to root (works outside the scene tree).
	static Transform3D FromRoot(Node root, Node n)
	{
		var xf = Transform3D.Identity;
		for (; n != null && n != root; n = n.GetParent())
			if (n is Node3D n3) xf = n3.Transform * xf;
		return xf;
	}

	public static T FindOfType<T>(Node root) where T : Node
	{
		if (root is T t) return t;
		foreach (var c in root.GetChildren())
			if (FindOfType<T>(c) is { } found) return found;
		return null;
	}

	public static StandardMaterial3D Flat(Color c, bool transparent = false) => new()
	{
		AlbedoColor = c,
		ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
		Transparency = transparent ? BaseMaterial3D.TransparencyEnum.Alpha : BaseMaterial3D.TransparencyEnum.Disabled,
	};
}
