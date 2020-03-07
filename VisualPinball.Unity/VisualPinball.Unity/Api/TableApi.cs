using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using VisualPinball.Engine.VPT.Flipper;

namespace VisualPinball.Engine.VPT.Table
{
	public class TableApi
	{
		public FlipperApi Flipper(string name) => Flippers.ContainsKey(name) ? Flippers[name] : null;
		public FlipperApi Flipper(int entityIndex) => Flippers.Values.FirstOrDefault(f => f.Entity.Index == entityIndex);

		internal Table _table;
		internal Entity _entity;

		internal readonly Dictionary<string, FlipperApi> Flippers = new Dictionary<string, FlipperApi>();

	}
}
