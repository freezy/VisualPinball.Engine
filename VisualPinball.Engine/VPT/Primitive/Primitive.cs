using System.IO;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Physics;

namespace VisualPinball.Engine.VPT.Primitive
{
	/// <summary>
	/// A primitive is a 3D playfield element usually imported from
	/// third party software such as Blender.
	/// </summary>
	///
	/// <see href="https://github.com/vpinball/vpinball/blob/master/primitive.cpp"/>
	public class Primitive : Item<PrimitiveData>, IRenderable, IHittable
	{
		public bool UseAsPlayfield;

		private readonly PrimitiveMeshGenerator _meshGenerator;
		private readonly PrimitiveHitGenerator _hitGenerator;
		private HitObject[] _hits;

		public Primitive(PrimitiveData data) : base(data)
		{
			_meshGenerator = new PrimitiveMeshGenerator(Data);
			_hitGenerator = new PrimitiveHitGenerator(this);
		}

		public Primitive(BinaryReader reader, string itemName) : this(new PrimitiveData(reader, itemName))
		{
		}

		public void Init(Table.Table table)
		{
			_hits = _hitGenerator.GenerateHitObjects(table, _meshGenerator, this);
		}

		public static Primitive GetDefault(Table.Table table)
		{
			var primitiveData = new PrimitiveData(table.GetNewName<Primitive>("Primitive"), table.Width / 2f, table.Height / 2f);
			return new Primitive(primitiveData);
		}

		public RenderObjectGroup GetRenderObjects(Table.Table table, Origin origin, bool asRightHanded, string parent, PbrMaterial material) =>
			_meshGenerator.GetRenderObjects(table, origin, asRightHanded, parent, material);

		public RenderObjectGroup GetRenderObjects(Table.Table table, Origin origin = Origin.Global, bool asRightHanded = true) =>
			_meshGenerator.GetRenderObjects(table, origin, asRightHanded);

		public HitObject[] GetHitShapes() => _hits;

		public Mesh GetMesh() => _meshGenerator.GetMesh();

	}
}
