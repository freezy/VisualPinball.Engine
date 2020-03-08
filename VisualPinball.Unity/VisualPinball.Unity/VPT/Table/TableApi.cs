using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using VisualPinball.Unity.VPT.Flipper;

namespace VisualPinball.Unity.VPT.Table
{
	public class TableApi
	{
		public FlipperApi Flipper(string name) => Flippers.ContainsKey(name) ? Flippers[name] : null;
		internal FlipperApi Flipper(int entityIndex) => Flippers.Values.FirstOrDefault(f => f.Entity.Index == entityIndex);

		internal Engine.VPT.Table.Table Table;
		internal Entity Entity;

		internal readonly Dictionary<string, FlipperApi> Flippers = new Dictionary<string, FlipperApi>();

		internal IEnumerable<IApiInitializable> Initializables => new IApiInitializable[0]
			.Concat(Flippers.Values)
			.ToArray();

	}
}
