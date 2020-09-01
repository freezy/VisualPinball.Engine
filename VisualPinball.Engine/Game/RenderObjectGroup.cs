using System.Linq;
using VisualPinball.Engine.Math;

namespace VisualPinball.Engine.Game
{
	public class RenderObjectGroup
	{
		public readonly string Name;
		/// <summary>
		/// Name of the game item group this item is added under (e.g. "Flippers", "Walls", etc)
		/// </summary>
		public readonly string Parent;
		public readonly RenderObject[] RenderObjects;
		public readonly Matrix3D TransformationMatrix;

		public bool ForceChild { get; set; }
		public bool HasOnlyChild => RenderObjects.Length == 1;
		public bool HasChildren => RenderObjects.Length > 0;

		public RenderObject Get(string name) => RenderObjects.First(ro => ro.Name == name);

		public RenderObjectGroup(string name, string parent, Matrix3D matrix, params RenderObject[] renderObjects)
		{
			Name = name;
			Parent = parent;
			RenderObjects = renderObjects;
			TransformationMatrix = matrix;
		}
	}
}
