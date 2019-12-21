using System.IO;

namespace VisualPinball.Engine.VPT.Primitive
{
	/// <summary>
	/// A primitive is a 3D playfield element usually imported from
	/// third party software such as Blender.
	/// </summary>
	///
	/// <see href="https://github.com/vpinball/vpinball/blob/master/primitive.cpp"/>
	public class Primitive : Item<PrimitiveData>
	{
		private readonly PrimitiveMeshGenerator MeshGenerator;

		public Primitive(BinaryReader reader, string itemName) : base(new PrimitiveData(reader, itemName))
		{
			MeshGenerator = new PrimitiveMeshGenerator(Data);
		}

		public Mesh GetMesh(Table.Table table)
		{
			return MeshGenerator.GetMesh(table);
		}
	}
}
