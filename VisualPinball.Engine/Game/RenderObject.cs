using VisualPinball.Engine.VPT;

namespace VisualPinball.Engine.Game
{
	public class RenderObject
	{
		public readonly string Name;
		public readonly Mesh Mesh;

		public readonly PbrMaterial Material;
		public readonly bool IsVisible;

		public RenderObject(string name, Mesh mesh, PbrMaterial material, bool isVisible)
		{
			Name = name;
			Mesh = mesh;
			Material = material;
			IsVisible = isVisible;
		}
	}
}
