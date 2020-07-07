using System;
using System.Collections.Generic;
using VisualPinball.Unity.VPT.Flipper;
using VisualPinball.Unity.VPT.Gate;
using VisualPinball.Unity.VPT.Kicker;
using VisualPinball.Unity.VPT.Plunger;

namespace VisualPinball.Unity.VPT.Table
{
	public class TableApi : IApiInitializable
	{
		internal readonly Dictionary<string, FlipperApi> Flippers = new Dictionary<string, FlipperApi>();
		internal readonly Dictionary<string, KickerApi> Kickers = new Dictionary<string, KickerApi>();
		internal readonly Dictionary<string, PlungerApi> Plungers = new Dictionary<string, PlungerApi>();
		internal readonly Dictionary<string, GateApi> Gates = new Dictionary<string, GateApi>();

		/// <summary>
		/// Event triggered before the game starts.
		/// </summary>
		public event EventHandler Init;

		/// <summary>
		/// Returns a flipper by name.
		/// </summary>
		/// <param name="name">Name of the flipper</param>
		/// <returns>Flipper or `null` if no flipper with that name exists.</returns>
		public FlipperApi Flipper(string name) => Flippers.ContainsKey(name) ? Flippers[name] : null;

		/// <summary>
		/// Returns a gate by name.
		/// </summary>
		/// <param name="name">Name of the gate</param>
		/// <returns>Gate or `null` if no gate with that name exists.</returns>
		public GateApi Gate(string name) => Gates.ContainsKey(name) ? Gates[name] : null;

		/// <summary>
		/// Returns a kicker by name.
		/// </summary>
		/// <param name="name">Name of the kicker</param>
		/// <returns>Kicker or `null` if no kicker with that name exists.</returns>
		public KickerApi Kicker(string name) => Kickers.ContainsKey(name) ? Kickers[name] : null;

		/// <summary>
		/// Returns a plunger by name.
		/// </summary>
		/// <param name="name">Name of the plunger</param>
		/// <returns>Plunger or `null` if no plunger with that name exists.</returns>
		public PlungerApi Plunger(string name) => Plungers.ContainsKey(name) ? Plungers[name] : null;


		#region Events

		void IApiInitializable.OnInit()
		{
			Init?.Invoke(this, EventArgs.Empty);
		}

		#endregion
	}
}
