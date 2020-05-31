using System.IO;
using VisualPinball.Engine.Game;

namespace VisualPinball.Engine.VPT.Primitive
{
	/// <summary>
	/// A primitive is a 3D playfield element usually imported from
	/// third party software such as Blender.
	/// </summary>
	///
	/// <see href="https://github.com/vpinball/vpinball/blob/master/primitive.cpp"/>
	public class Primitive : Item<PrimitiveData>, IRenderable
	{
		private readonly PrimitiveMeshGenerator _meshGenerator;

		public Primitive(PrimitiveData data) : base(data)
		{
			_meshGenerator = new PrimitiveMeshGenerator(Data);
		}

		public Primitive(BinaryReader reader, string itemName) : this(new PrimitiveData(reader, itemName)) { }

		public RenderObjectGroup GetRenderObjects(Table.Table table, Origin origin, bool asRightHanded, string parent, PbrMaterial material)
		{
			return _meshGenerator.GetRenderObjects(table, origin, asRightHanded, parent, material);
		}

		public RenderObjectGroup GetRenderObjects(Table.Table table, Origin origin = Origin.Global, bool asRightHanded = true)
		{
			return _meshGenerator.GetRenderObjects(table, origin, asRightHanded);
		}

		public Mesh GetMesh()
		{
			return _meshGenerator.GetMesh();
		}
	}
}
