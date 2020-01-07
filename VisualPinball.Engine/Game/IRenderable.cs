using System.Linq;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Engine.Game
{
	public interface IRenderable
	{
		string Name { get; }

		RenderObject[] GetRenderObjects(Table table);
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

		/// <summary>
		/// A unique ID based on the material and its maps.
		/// </summary>
		public string MaterialId => string.Join("-", new[] {
				Material?.Name ?? "__no_material",
				Map?.Name ?? "__no_map",
				NormalMap?.Name ?? "__no_normal_map",
				EnvMap?.Name ?? "__no_env_map"
			}
			.Reverse()
			.SkipWhile(s => s.StartsWith("__no_") && s != "__no_material")
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
