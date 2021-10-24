// Visual Pinball Engine
// Copyright (C) 2021 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace VisualPinball.Unity
{
	public class TableApi : IApi
	{
		private readonly Player _player;

		#region Dictionaries

		private readonly Dictionary<string, BumperApi> _bumpersByName = new Dictionary<string, BumperApi>();
		private readonly Dictionary<string, CannonApi> _mechsByName = new Dictionary<string, CannonApi>();
		private readonly Dictionary<string, DropTargetApi> _dropTargetsByName = new Dictionary<string, DropTargetApi>();
		private readonly Dictionary<string, FlipperApi> _flippersByName = new Dictionary<string, FlipperApi>();
		private readonly Dictionary<string, GateApi> _gatesByName = new Dictionary<string, GateApi>();
		private readonly Dictionary<string, HitTargetApi> _hitTargetsByName = new Dictionary<string, HitTargetApi>();
		private readonly Dictionary<string, KickerApi> _kickersByName = new Dictionary<string, KickerApi>();
		private readonly Dictionary<string, LightApi> _lightsByName = new Dictionary<string, LightApi>();
		private readonly Dictionary<string, LightGroupApi> _lightGroupsByName = new Dictionary<string, LightGroupApi>();
		private readonly Dictionary<string, PlungerApi> _plungersByName = new Dictionary<string, PlungerApi>();
		private readonly Dictionary<string, PrimitiveApi> _primitivesByName = new Dictionary<string, PrimitiveApi>();
		private readonly Dictionary<string, RampApi> _rampsByName = new Dictionary<string, RampApi>();
		private readonly Dictionary<string, RubberApi> _rubbersByName = new Dictionary<string, RubberApi>();
		private readonly Dictionary<string, SpinnerApi> _spinnersByName = new Dictionary<string, SpinnerApi>();
		private readonly Dictionary<string, SurfaceApi> _surfacesByName = new Dictionary<string, SurfaceApi>();
		private readonly Dictionary<string, TeleporterApi> _teleportersByName = new Dictionary<string, TeleporterApi>();
		private readonly Dictionary<string, TriggerApi> _triggersByName = new Dictionary<string, TriggerApi>();
		private readonly Dictionary<string, TroughApi> _troughsByName = new Dictionary<string, TroughApi>();

		private readonly Dictionary<MonoBehaviour, BumperApi> _bumpersByComponent = new Dictionary<MonoBehaviour, BumperApi>();
		private readonly Dictionary<MonoBehaviour, CannonApi> _mechsByComponent = new Dictionary<MonoBehaviour, CannonApi>();
		private readonly Dictionary<MonoBehaviour, DropTargetApi> _dropTargetsByComponent = new Dictionary<MonoBehaviour, DropTargetApi>();
		private readonly Dictionary<MonoBehaviour, FlipperApi> _flippersByComponent = new Dictionary<MonoBehaviour, FlipperApi>();
		private readonly Dictionary<MonoBehaviour, GateApi> _gatesByComponent = new Dictionary<MonoBehaviour, GateApi>();
		private readonly Dictionary<MonoBehaviour, HitTargetApi> _hitTargetsByComponent = new Dictionary<MonoBehaviour, HitTargetApi>();
		private readonly Dictionary<MonoBehaviour, KickerApi> _kickersByComponent = new Dictionary<MonoBehaviour, KickerApi>();
		private readonly Dictionary<MonoBehaviour, LightApi> _lightsByComponent = new Dictionary<MonoBehaviour, LightApi>();
		private readonly Dictionary<MonoBehaviour, LightGroupApi> _lightGroupsByComponent = new Dictionary<MonoBehaviour, LightGroupApi>();
		private readonly Dictionary<MonoBehaviour, PlungerApi> _plungersByComponent = new Dictionary<MonoBehaviour, PlungerApi>();
		private readonly Dictionary<MonoBehaviour, PrimitiveApi> _primitivesByComponent = new Dictionary<MonoBehaviour, PrimitiveApi>();
		private readonly Dictionary<MonoBehaviour, RampApi> _rampsByComponent = new Dictionary<MonoBehaviour, RampApi>();
		private readonly Dictionary<MonoBehaviour, RubberApi> _rubbersByComponent = new Dictionary<MonoBehaviour, RubberApi>();
		private readonly Dictionary<MonoBehaviour, SpinnerApi> _spinnersByComponent = new Dictionary<MonoBehaviour, SpinnerApi>();
		private readonly Dictionary<MonoBehaviour, SurfaceApi> _surfacesByComponent = new Dictionary<MonoBehaviour, SurfaceApi>();
		private readonly Dictionary<MonoBehaviour, TeleporterApi> _teleportersByComponent = new Dictionary<MonoBehaviour, TeleporterApi>();
		private readonly Dictionary<MonoBehaviour, TriggerApi> _triggersByComponent = new Dictionary<MonoBehaviour, TriggerApi>();
		private readonly Dictionary<MonoBehaviour, TroughApi> _troughsByComponent = new Dictionary<MonoBehaviour, TroughApi>();

		#endregion

		public TableApi(Player player)
		{
			_player = player;
		}

		internal IApiSwitch Switch(ISwitchDeviceComponent component, string switchItem) => _player.Switch(component, switchItem);
		internal IApiCoil Coil(ICoilDeviceComponent component, string coilItem) => _player.Coil(component, coilItem);

		/// <summary>
		/// Event emitted before the game starts.
		/// </summary>
		public event EventHandler Init;

		#region Items

		/// <summary>
		/// Returns a bumper by name.
		/// </summary>
		/// <param name="name">Name of the bumper</param>
		/// <returns>Bumper or `null` if no bumper with that name exists.</returns>
		public BumperApi Bumper(string name) => Get<BumperApi>(name);
		public BumperApi Bumper(MonoBehaviour component) => Get<BumperApi>(component);

		/// <summary>
		/// Returns a flipper by name.
		/// </summary>
		/// <param name="name">Name of the flipper</param>
		/// <returns>Flipper or `null` if no flipper with that name exists.</returns>
		public FlipperApi Flipper(string name) => Get<FlipperApi>(name);
		public FlipperApi Flipper(MonoBehaviour component) => Get<FlipperApi>(component);

		/// <summary>
		/// Returns a gate by name.
		/// </summary>
		/// <param name="name">Name of the gate</param>
		/// <returns>Gate or `null` if no gate with that name exists.</returns>
		public GateApi Gate(string name) => Get<GateApi>(name);
		public GateApi Gate(MonoBehaviour component) => Get<GateApi>(component);

		/// <summary>
		/// Returns a hit target / drop target by name.
		/// </summary>
		/// <param name="name">Name of the target</param>
		/// <returns>Hit/drop target or `null` if no target with that name exists.</returns>
		public HitTargetApi HitTarget(string name) => Get<HitTargetApi>(name);
		public HitTargetApi HitTarget(MonoBehaviour component) => Get<HitTargetApi>(component);

		/// <summary>
		/// Returns a kicker by name.
		/// </summary>
		/// <param name="name">Name of the kicker</param>
		/// <returns>Kicker or `null` if no kicker with that name exists.</returns>
		public KickerApi Kicker(string name) => Get<KickerApi>(name);
		public KickerApi Kicker(MonoBehaviour component) => Get<KickerApi>(component);

		/// <summary>
		/// Returns a light by name.
		/// </summary>
		/// <param name="name">Name of the light</param>
		/// <returns>Light or `null` if no light with that name exists.</returns>
		public LightApi Light(string name) => Get<LightApi>(name);
		public LightApi Light(MonoBehaviour component) => Get<LightApi>(component);

		/// <summary>
		/// Returns a plunger by name.
		/// </summary>
		/// <param name="name">Name of the plunger</param>
		/// <returns>Plunger or `null` if no plunger with that name exists.</returns>
		public PlungerApi Plunger(string name) => Get<PlungerApi>(name);
		public PlungerApi Plunger(MonoBehaviour component) => Get<PlungerApi>(component);

		/// <summary>
		/// Returns a primitive by name.
		/// </summary>
		/// <param name="name">Name of the primitive</param>
		/// <returns>Primitive or `null` if no primitive with that name exists.</returns>
		public PrimitiveApi Primitive(string name) => Get<PrimitiveApi>(name);
		public PrimitiveApi Primitive(MonoBehaviour component) => Get<PrimitiveApi>(component);

		/// <summary>
		/// Returns a ramp by name.
		/// </summary>
		/// <param name="name">Name of the ramp</param>
		/// <returns>Ramp or `null` if no ramp with that name exists.</returns>
		public RampApi Ramp(string name) => Get<RampApi>(name);
		public RampApi Ramp(MonoBehaviour component) => Get<RampApi>(component);

		/// <summary>
		/// Returns a rubber by name.
		/// </summary>
		/// <param name="name">Name of the rubber</param>
		/// <returns>Rubber or `null` if no rubber with that name exists.</returns>
		public RubberApi Rubber(string name) => Get<RubberApi>(name);
		public RubberApi Rubber(MonoBehaviour component) => Get<RubberApi>(component);

		/// <summary>
		/// Returns a spinner by name.
		/// </summary>
		/// <param name="name">Name of the spinner</param>
		/// <returns>Spinner or `null` if no spinner with that name exists.</returns>
		public SpinnerApi Spinner(string name) => Get<SpinnerApi>(name);
		public SpinnerApi Spinner(MonoBehaviour component) => Get<SpinnerApi>(component);

		/// <summary>
		/// Returns a surface (wall) by name.
		/// </summary>
		/// <param name="name">Name of the surface</param>
		/// <returns>Surface or `null` if no surface with that name exists.</returns>
		public SurfaceApi Surface(string name) => Get<SurfaceApi>(name);
		public SurfaceApi Surface(MonoBehaviour component) => Get<SurfaceApi>(component);

		/// <summary>
		/// Returns a trigger by name.
		/// </summary>
		/// <param name="name">Name of the trigger</param>
		/// <returns>Trigger or `null` if no trigger with that name exists.</returns>
		public TriggerApi Trigger(string name) => Get<TriggerApi>(name);
		public TriggerApi Trigger(MonoBehaviour component) => Get<TriggerApi>(component);

		/// <summary>
		/// Returns a trough by name.
		/// </summary>
		/// <param name="name">Name of the trough</param>
		/// <returns>Trigger or `null` if no trough with that name exists.</returns>
		public TroughApi Trough(string name) => Get<TroughApi>(name);
		public TroughApi Trough(MonoBehaviour component) => Get<TroughApi>(component);

		/// <summary>
		/// Returns a mech by name.
		/// </summary>
		/// <param name="name">Name of the mech</param>
		/// <returns>Primitive or `null` if no mech with that name exists.</returns>
		public CannonApi Cannon(string name) => Get<CannonApi>(name);
		public CannonApi Cannon(MonoBehaviour component) => Get<CannonApi>(component);

		#endregion

		#region Registration

		internal void Register<T>(MonoBehaviour component, T api) where T : IApi
		{
			var nameDict = GetNameDictionary<T>();
			var compDict = GetComponentDictionary<T>();

			nameDict[component.name] = api;
			compDict[component] = api;
		}

		private bool Has<T>(string name) where T : IApi => GetNameDictionary<T>().ContainsKey(name);
		private bool Has<T>(MonoBehaviour comp) where T : IApi => GetComponentDictionary<T>().ContainsKey(comp);
		private T Get<T>(string name) where T : class, IApi => !string.IsNullOrEmpty(name) && Has<T>(name) ? GetNameDictionary<T>()[name] : null;
		private T Get<T>(MonoBehaviour comp) where T : class, IApi => comp != null && Has<T>(comp) ? GetComponentDictionary<T>()[comp] : null;

		private Dictionary<string, T> GetNameDictionary<T>() where T : IApi => GetNameDictionary<T>(typeof(T));
		private Dictionary<MonoBehaviour, T> GetComponentDictionary<T>() where T : IApi => GetComponentDictionary<T>(typeof(T));

		private Dictionary<string, T> GetNameDictionary<T>(Type t) where T : IApi
		{
			if (t == typeof(BumperApi)) return _bumpersByName as Dictionary<string, T>;
			if (t == typeof(CannonApi)) return _mechsByName as Dictionary<string, T>;
			if (t == typeof(DropTargetApi)) return _dropTargetsByName as Dictionary<string, T>;
			if (t == typeof(FlipperApi)) return _flippersByName as Dictionary<string, T>;
			if (t == typeof(GateApi)) return _gatesByName as Dictionary<string, T>;
			if (t == typeof(HitTargetApi)) return _hitTargetsByName as Dictionary<string, T>;
			if (t == typeof(KickerApi)) return _kickersByName as Dictionary<string, T>;
			if (t == typeof(LightApi)) return _lightsByName as Dictionary<string, T>;
			if (t == typeof(LightGroupApi)) return _lightGroupsByName as Dictionary<string, T>;
			if (t == typeof(PlungerApi)) return _plungersByName as Dictionary<string, T>;
			if (t == typeof(PrimitiveApi)) return _primitivesByName as Dictionary<string, T>;
			if (t == typeof(RampApi)) return _rampsByName as Dictionary<string, T>;
			if (t == typeof(RubberApi)) return _rubbersByName as Dictionary<string, T>;
			if (t == typeof(SpinnerApi)) return _spinnersByName as Dictionary<string, T>;
			if (t == typeof(SurfaceApi)) return _surfacesByName as Dictionary<string, T>;
			if (t == typeof(TeleporterApi)) return _teleportersByName as Dictionary<string, T>;
			if (t == typeof(TriggerApi)) return _triggersByName as Dictionary<string, T>;
			if (t == typeof(TroughApi)) return _troughsByName as Dictionary<string, T>;
			throw new ArgumentException($"Unknown API type {t}.");
		}

		private Dictionary<MonoBehaviour, T> GetComponentDictionary<T>(Type t) where T : IApi
		{
			if (t == typeof(BumperApi)) return _bumpersByComponent as Dictionary<MonoBehaviour, T>;
			if (t == typeof(CannonApi)) return _mechsByComponent as Dictionary<MonoBehaviour, T>;
			if (t == typeof(DropTargetApi)) return _dropTargetsByComponent as Dictionary<MonoBehaviour, T>;
			if (t == typeof(FlipperApi)) return _flippersByComponent as Dictionary<MonoBehaviour, T>;
			if (t == typeof(GateApi)) return _gatesByComponent as Dictionary<MonoBehaviour, T>;
			if (t == typeof(HitTargetApi)) return _hitTargetsByComponent as Dictionary<MonoBehaviour, T>;
			if (t == typeof(KickerApi)) return _kickersByComponent as Dictionary<MonoBehaviour, T>;
			if (t == typeof(LightApi)) return _lightsByComponent as Dictionary<MonoBehaviour, T>;
			if (t == typeof(LightGroupApi)) return _lightGroupsByComponent as Dictionary<MonoBehaviour, T>;
			if (t == typeof(PlungerApi)) return _plungersByComponent as Dictionary<MonoBehaviour, T>;
			if (t == typeof(PrimitiveApi)) return _primitivesByComponent as Dictionary<MonoBehaviour, T>;
			if (t == typeof(RampApi)) return _rampsByComponent as Dictionary<MonoBehaviour, T>;
			if (t == typeof(RubberApi)) return _rubbersByComponent as Dictionary<MonoBehaviour, T>;
			if (t == typeof(SpinnerApi)) return _spinnersByComponent as Dictionary<MonoBehaviour, T>;
			if (t == typeof(SurfaceApi)) return _surfacesByComponent as Dictionary<MonoBehaviour, T>;
			if (t == typeof(TeleporterApi)) return _teleportersByComponent as Dictionary<MonoBehaviour, T>;
			if (t == typeof(TriggerApi)) return _triggersByComponent as Dictionary<MonoBehaviour, T>;
			if (t == typeof(TroughApi)) return _troughsByComponent as Dictionary<MonoBehaviour, T>;
			throw new ArgumentException($"Unknown API type {t}.");
		}

		#endregion

		#region Events


		void IApi.OnInit(BallManager ballManager)
		{
			Init?.Invoke(this, EventArgs.Empty);
		}

		void IApi.OnDestroy()
		{
		}

		#endregion
	}
}
