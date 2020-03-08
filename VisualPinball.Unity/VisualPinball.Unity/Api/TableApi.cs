using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Unity.Api
{
	public class TableApi
	{
		public FlipperApi Flipper(string name) => Flippers.ContainsKey(name) ? Flippers[name] : null;
		internal FlipperApi Flipper(int entityIndex) => Flippers.Values.FirstOrDefault(f => f.Entity.Index == entityIndex);

		internal Table Table;
		internal Entity Entity;

		internal readonly Dictionary<string, FlipperApi> Flippers = new Dictionary<string, FlipperApi>();

	}
}
