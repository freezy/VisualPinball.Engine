using System.IO;
using System.Linq;
using System;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;

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

		public Primitive(BinaryReader reader, string itemName) : base(new PrimitiveData(reader, itemName))
		{
			_meshGenerator = new PrimitiveMeshGenerator(Data);
		}

		public RenderObject[] GetRenderObjects(Table.Table table)
		{
			return _meshGenerator.GetRenderObjects(table);
		}
	}
}
