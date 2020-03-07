using System.Collections.Generic;
using Unity.Entities;
using VisualPinball.Engine.VPT.Flipper;

namespace VisualPinball.Engine.VPT.Table
{
	public class TableApi
	{
		public FlipperApi Flipper(string name) => _flippers.ContainsKey(name) ? _flippers[name] : null;

		internal Table _table;
		internal Entity _entity;

		internal readonly Dictionary<string, FlipperApi> _flippers = new Dictionary<string, FlipperApi>();

	}
}
