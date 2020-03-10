using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using VisualPinball.Unity.VPT.Flipper;
using VisualPinball.Unity.VPT.Kicker;

namespace VisualPinball.Unity.VPT.Table
{
	public class TableApi
	{
		internal readonly Dictionary<string, FlipperApi> Flippers = new Dictionary<string, FlipperApi>();
		internal readonly Dictionary<string, KickerApi> Kickers = new Dictionary<string, KickerApi>();

		internal Engine.VPT.Table.Table Table;
		internal Entity Entity;

		public FlipperApi Flipper(string name) => Flippers.ContainsKey(name) ? Flippers[name] : null;

		internal IEnumerable<IApiInitializable> Initializables => new IApiInitializable[0]
			.Concat(Flippers.Values)
			.ToArray();

	}
}
