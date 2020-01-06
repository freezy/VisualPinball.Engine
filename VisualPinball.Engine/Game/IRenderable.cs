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
		public readonly bool IsTransparent;

		public RenderObject(string name = null, Mesh mesh = null, Texture map = null, Texture normalMap = null, Texture envMap = null, Material material = null, bool isTransparent = default)
		{
			Name = name;
			Mesh = mesh;
			Map = map;
			NormalMap = normalMap;
			EnvMap = envMap;
			Material = material;
			IsTransparent = isTransparent;
		}
	}
}
