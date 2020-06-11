using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using VisualPinball.Unity.VPT.Flipper;
using VisualPinball.Unity.VPT.Kicker;
using VisualPinball.Unity.VPT.Plunger;

namespace VisualPinball.Unity.VPT.Table
{
	public class TableApi
	{
		internal readonly Dictionary<string, FlipperApi> Flippers = new Dictionary<string, FlipperApi>();
		internal readonly Dictionary<string, KickerApi> Kickers = new Dictionary<string, KickerApi>();
		internal readonly Dictionary<string, PlungerApi> Plungers = new Dictionary<string, PlungerApi>();

		internal Engine.VPT.Table.Table Table;
		internal Entity Entity;

		public FlipperApi Flipper(string name) => Flippers.ContainsKey(name) ? Flippers[name] : null;
		public PlungerApi Plunger(string name) => Plungers.ContainsKey(name) ? Plungers[name] : null;

		internal IEnumerable<IApiInitializable> Initializables => new IApiInitializable[0]
			.Concat(Flippers.Values)
			.ToArray();

	}
}
