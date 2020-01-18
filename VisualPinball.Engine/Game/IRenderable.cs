using System.Linq;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Engine.Game
{
	public interface IRenderable
	{
		string Name { get; }

		RenderObjectGroup GetRenderObjects(Table table, Origin origin = Origin.Global, bool asRightHanded = true);
	}

	public enum Origin
	{
		/// <summary>
		/// Keeps the origin the same as in Visual Pinball. <p/>
		///
		/// This means that the object must additional retrieve a
		/// transformation matrix.
		/// </summary>
		Original,

		/// <summary>
		/// Transforms all vertices so their origin is the global origin. <p/>
		///
		/// No additional transformation matrices must be applied if the object
		/// is static.
		/// </summary>
		Global,

		/// <summary>
		/// Transforms all vertices so their origin is the center of the object.
		/// </summary>
		//Local
	}

	public class RenderObjectGroup
	{
		public readonly string Name;
		public readonly string Parent;
		public readonly RenderObject[] RenderObjects;
		public readonly Matrix3D TransformationMatrix;

		public bool HasOnlyChild => RenderObjects.Length == 1;
		public bool HasChildren => RenderObjects.Length > 0;

		public RenderObjectGroup(string name, string parent)
		{
			Name = name;
			Parent = parent;
			RenderObjects = new RenderObject[0];
			TransformationMatrix = Matrix3D.Identity;
		}

		public RenderObjectGroup(string name, string parent, RenderObject renderObject, Matrix3D matrix)
		{
			Name = name;
			Parent = parent;
			RenderObjects = new []{ renderObject };
			TransformationMatrix = matrix;
		}

		public RenderObjectGroup(string name, string parent, RenderObject[] renderObjects, Matrix3D matrix)
		{
			Name = name;
			Parent = parent;
			RenderObjects = renderObjects;
			TransformationMatrix = matrix;
		}
	}

	public class RenderObject
	{
		public readonly string Name;
		public readonly Mesh Mesh;

		public readonly Texture Map;
		public readonly Texture NormalMap;
		public readonly Texture EnvMap;
		public readonly Material Material;
		public readonly bool IsVisible;
		public readonly bool IsTransparent;

		public const string MaterialNameNoMaterial = "__no_material";

		/// <summary>
		/// A unique ID based on the material and its maps.
		/// </summary>
		public string MaterialId => string.Join("-", new[] {
				Material?.Name ?? MaterialNameNoMaterial,
				Map?.Name ?? "__no_map",
				NormalMap?.Name ?? "__no_normal_map",
				EnvMap?.Name ?? "__no_env_map"
			}
			.Reverse()
			.SkipWhile(s => s.StartsWith("__no_") && s != MaterialNameNoMaterial)
			.Reverse()
		);

		public RenderObject(string name = null, Mesh mesh = null, Texture map = null, Texture normalMap = null, Texture envMap = null, Material material = null, bool isVisible = true, bool isTransparent = false)
		{
			Name = name;
			Mesh = mesh;
			Map = map;
			NormalMap = normalMap;
			EnvMap = envMap;
			Material = material;
			IsVisible = isVisible;
			IsTransparent = isTransparent;
		}
	}
}
