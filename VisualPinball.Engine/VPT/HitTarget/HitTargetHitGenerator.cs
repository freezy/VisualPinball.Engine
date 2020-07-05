using System.Collections.Generic;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Physics;

namespace VisualPinball.Engine.VPT.HitTarget
{
	public class HitTargetHitGenerator
	{
		private readonly HitTargetData _data;

		public HitTargetHitGenerator(HitTargetData data)
		{
			_data = data;
		}

		public HitObject[] GenerateHitObjects(Table.Table table, EventProxy eventProxy)
		{
			var hitObjects = new List<HitObject>();

			return hitObjects.ToArray();
		}
	}
}
