using System.IO;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.Physics;

namespace VisualPinball.Engine.VPT.Rubber
{
	public class Rubber : Item<RubberData>, IRenderable, IHittable
	{
		public EventProxy EventProxy { get; private set; }
		public bool IsCollidable => Data.IsCollidable;
		public HitObject[] GetHitShapes() => _hits;

		private readonly RubberMeshGenerator _meshGenerator;
		private readonly RubberHitGenerator _hitGenerator;
		private HitObject[] _hits;

		public Rubber(RubberData data) : base(data)
		{
			_meshGenerator = new RubberMeshGenerator(Data);
			_hitGenerator = new RubberHitGenerator(Data, _meshGenerator);
		}

		public Rubber(BinaryReader reader, string itemName) : this(new RubberData(reader, itemName))
		{
		}

		public static Rubber GetDefault(Table.Table table)
		{
			var x = table.Width / 2f;
			var y = table.Height / 2f;
			var rubberData = new RubberData(table.GetNewName<Rubber>("Rubber")) {
				DragPoints = new[] {
					new DragPointData(x, y - 50f) {IsSmooth = true },
					new DragPointData(x - 50f * MathF.Cos(MathF.PI / 4), y - 50f * MathF.Sin(MathF.PI / 4)) {IsSmooth = true },
					new DragPointData(x - 50f, y) {IsSmooth = true },
					new DragPointData(x - 50f * MathF.Cos(MathF.PI / 4), y + 50f * MathF.Sin(MathF.PI / 4)) {IsSmooth = true },
					new DragPointData(x, y + 50f) {IsSmooth = true },
					new DragPointData(x + 50f * MathF.Cos(MathF.PI / 4), y + 50f * MathF.Sin(MathF.PI / 4)) {IsSmooth = true },
					new DragPointData(x + 50f, y) {IsSmooth = true },
					new DragPointData(x + 50f * MathF.Cos(MathF.PI / 4), y - 50f * MathF.Sin(MathF.PI / 4)) {IsSmooth = true },
				}
			};
			return new Rubber(rubberData);
		}

		public RenderObjectGroup GetRenderObjects(Table.Table table, Origin origin = Origin.Global, bool asRightHanded = true)
		{
			return _meshGenerator.GetRenderObjects(table, origin, asRightHanded);
		}

		public void Init(Table.Table table)
		{
			EventProxy = new EventProxy(this);
			_hits = _hitGenerator.GenerateHitObjects(EventProxy, table);
		}
	}
}
