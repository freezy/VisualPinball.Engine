using VisualPinball.Engine.Math;

namespace VisualPinball.Engine.VPT.Flipper
{
	public class FlipperState : ItemState
	{
		public float Angle;
		public Vertex2D Center;
		public string Material;
		public string Texture;
		public string RubberMaterial;

		public FlipperState(string name, bool isVisible, float angle, Vertex2D center, string material, string texture, string rubberMaterial) : base(name, isVisible)
		{
			Angle = angle;
			Center = center;
			Material = material;
			Texture = texture;
			RubberMaterial = rubberMaterial;
		}
	}
}
