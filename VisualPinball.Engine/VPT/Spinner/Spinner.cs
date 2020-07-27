using System;
using System.IO;
using System.Linq;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Physics;

namespace VisualPinball.Engine.VPT.Spinner
{
	public class Spinner : Item<SpinnerData>, IRenderable, IMovable, IHittable
	{
		public const string BracketMaterialName = "__spinnerBracketMaterial";

		public EventProxy EventProxy { get; private set; }
		public bool IsCollidable => true;

		private readonly SpinnerMeshGenerator _meshGenerator;
		private readonly SpinnerHitGenerator _hitGenerator;

		private SpinnerHit _hitSpinner;
		private HitCircle[] _hitCircles;

		public Spinner(SpinnerData data) : base(data)
		{
			_meshGenerator = new SpinnerMeshGenerator(Data);
			_hitGenerator = new SpinnerHitGenerator(Data);
		}

		public Spinner(BinaryReader reader, string itemName) : this(new SpinnerData(reader, itemName)) { }

		public void Init(Table.Table table)
		{
			var height = table.GetSurfaceHeight(Data.Surface, Data.Center.X, Data.Center.Y);

			EventProxy = new EventProxy(this);
			_hitSpinner = new SpinnerHit(Data, height);
			_hitCircles = _hitGenerator.GetHitCircles(height);
		}

		public RenderObjectGroup GetRenderObjects(Table.Table table, Origin origin = Origin.Global, bool asRightHanded = true)
		{
			return _meshGenerator.GetRenderObjects(table, origin, asRightHanded);
		}

		public HitObject[] GetHitShapes()
		{
			return new HitObject[] {_hitSpinner}
				.Concat(_hitCircles)
				.ToArray();
		}

		public IMoverObject GetMover()
		{
			// not needed in unity ECS
			throw new NotImplementedException();
		}
	}
}
