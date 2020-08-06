using System.IO;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.Physics;

namespace VisualPinball.Engine.VPT.Trigger
{
	public class Trigger : Item<TriggerData>, IRenderable, IHittable
	{
		public bool IsCollidable => true;
		public HitObject[] GetHitShapes() => _hits;
		public EventProxy EventProxy { get; private set; }

		private readonly TriggerMeshGenerator _meshGenerator;
		private readonly TriggerHitGenerator _hitGenerator;
		private HitObject[] _hits;

		public Trigger(TriggerData data) : base(data)
		{
			_meshGenerator = new TriggerMeshGenerator(Data);
			_hitGenerator = new TriggerHitGenerator(Data);
		}

		public Trigger(BinaryReader reader, string itemName) : this(new TriggerData(reader, itemName))
		{
		}

		public static Trigger GetDefault(Table.Table table)
		{
			var triggerData = new TriggerData(table.GetNewName<Trigger>("Trigger"), table.Width / 2f, table.Height / 2f)
			{
				DragPoints = new[] {
					new DragPointData(table.Width / 2f - 50f, table.Height / 2f - 50f),
					new DragPointData(table.Width / 2f - 50f, table.Height / 2f + 50f),
					new DragPointData(table.Width / 2f + 50f, table.Height / 2f + 50f),
					new DragPointData(table.Width / 2f + 50f, table.Height / 2f - 50f)
				}
			};
			return new Trigger(triggerData);
		}

		public void Init(Table.Table table)
		{
			EventProxy = new EventProxy(this);
			_hits = _hitGenerator.GenerateHitObjects(table, EventProxy);
		}

		public RenderObjectGroup GetRenderObjects(Table.Table table, Origin origin = Origin.Global, bool asRightHanded = true)
		{
			return _meshGenerator.GetRenderObjects(table, origin, asRightHanded);
		}
	}
}
