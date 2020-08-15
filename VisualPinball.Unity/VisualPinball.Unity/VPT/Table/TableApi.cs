using System;
using System.Collections.Generic;

namespace VisualPinball.Unity
{
	public class TableApi : IApiInitializable
	{
		internal readonly Dictionary<string, BumperApi> Bumpers = new Dictionary<string, BumperApi>();
		internal readonly Dictionary<string, FlipperApi> Flippers = new Dictionary<string, FlipperApi>();
		internal readonly Dictionary<string, GateApi> Gates = new Dictionary<string, GateApi>();
		internal readonly Dictionary<string, HitTargetApi> HitTargets = new Dictionary<string, HitTargetApi>();
		internal readonly Dictionary<string, KickerApi> Kickers = new Dictionary<string, KickerApi>();
		internal readonly Dictionary<string, PlungerApi> Plungers = new Dictionary<string, PlungerApi>();
		internal readonly Dictionary<string, RampApi> Ramps = new Dictionary<string, RampApi>();
		internal readonly Dictionary<string, RubberApi> Rubbers = new Dictionary<string, RubberApi>();
		internal readonly Dictionary<string, SpinnerApi> Spinners = new Dictionary<string, SpinnerApi>();
		internal readonly Dictionary<string, SurfaceApi> Surfaces = new Dictionary<string, SurfaceApi>();
		internal readonly Dictionary<string, TriggerApi> Triggers = new Dictionary<string, TriggerApi>();

		/// <summary>
		/// Event emitted before the game starts.
		/// </summary>
		public event EventHandler Init;

		/// <summary>
		/// Returns a bumper by name.
		/// </summary>
		/// <param name="name">Name of the bumper</param>
		/// <returns>Bumper or `null` if no bumper with that name exists.</returns>
		public BumperApi Bumper(string name) => Bumpers.ContainsKey(name) ? Bumpers[name] : null;

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
		/// Returns a hit target / drop target by name.
		/// </summary>
		/// <param name="name">Name of the target</param>
		/// <returns>Hit/drop target or `null` if no target with that name exists.</returns>
		public HitTargetApi HitTarget(string name) => HitTargets.ContainsKey(name) ? HitTargets[name] : null;

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

		/// <summary>
		/// Returns a ramp by name.
		/// </summary>
		/// <param name="name">Name of the ramp</param>
		/// <returns>Ramp or `null` if no ramp with that name exists.</returns>
		public RampApi Ramp(string name) => Ramps.ContainsKey(name) ? Ramps[name] : null;

		/// <summary>
		/// Returns a rubber by name.
		/// </summary>
		/// <param name="name">Name of the rubber</param>
		/// <returns>Rubber or `null` if no rubber with that name exists.</returns>
		public RubberApi Rubber(string name) => Rubbers.ContainsKey(name) ? Rubbers[name] : null;

		/// <summary>
		/// Returns a spinner by name.
		/// </summary>
		/// <param name="name">Name of the spinner</param>
		/// <returns>Spinner or `null` if no spinner with that name exists.</returns>
		public SpinnerApi Spinner(string name) => Spinners.ContainsKey(name) ? Spinners[name] : null;

		/// <summary>
		/// Returns a surface (wall) by name.
		/// </summary>
		/// <param name="name">Name of the surface</param>
		/// <returns>Surface or `null` if no surface with that name exists.</returns>
		public SurfaceApi Surface(string name) => Surfaces.ContainsKey(name) ? Surfaces[name] : null;

		/// <summary>
		/// Returns a trigger by name.
		/// </summary>
		/// <param name="name">Name of the trigger</param>
		/// <returns>Trigger or `null` if no trigger with that name exists.</returns>
		public TriggerApi Trigger(string name) => Triggers.ContainsKey(name) ? Triggers[name] : null;

		#region Events

		void IApiInitializable.OnInit()
		{
			Init?.Invoke(this, EventArgs.Empty);
		}

		#endregion
	}
}
