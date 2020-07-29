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
		Global
	}

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
