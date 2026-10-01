// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using UnityEngine;
using VisualPinball.Engine.Game.Engines;

namespace VisualPinball.Unity
{
	/// <summary>
	/// ROM and wire events for a directly mapped hybrid insert. The device's
	/// own API drives the first bulb; each further bulb of the material has an
	/// API of its own, handed out by bulb index, sharing this one's
	/// initialisation.
	/// </summary>
	public class InsertLightApi : IApiLamp, IApiLampDevice, IApiWireDeviceDest
	{
		public event EventHandler Init;
		private readonly InsertLightComponent _component;
		private readonly InsertLightApi _root;
		private readonly int _bulb;
		private readonly InsertLightApi[] _bulbs = new InsertLightApi[InsertLightComponent.MaxBulbs];
		private bool _initialized;

		internal InsertLightApi(InsertLightComponent component)
		{
			_component = component;
			_root = this;
			_bulbs[0] = this;
		}

		private InsertLightApi(InsertLightApi root, int bulb)
		{
			_component = root._component;
			_root = root;
			_bulb = bulb;
		}

		/// <summary>The API driving the bulb at that index, the first being this one.</summary>
		internal IApiLamp Bulb(int index)
		{
			index = Mathf.Clamp(index, 0, _root._bulbs.Length - 1);
			return _root._bulbs[index] ??= new InsertLightApi(_root, index);
		}

		public void OnInit(BallManager ballManager)
		{
			_initialized = true;
			_component.StartGame();
			Init?.Invoke(this, EventArgs.Empty);
		}

		public void OnDestroy()
		{
			_initialized = false;
			_component.ReleaseApiControl();
		}

		public void OnLamp(LampStatus status)
		{
			if (_root._initialized) { _component.SetBulbStatus(_bulb, status); }
		}

		public void OnLamp(float intensity)
		{
			// Fading/RGB mappings send an absolute normalized level without a separate
			// On status. A positive level must therefore also wake an off insert.
			if (_root._initialized) { _component.SetBulbIntensity(_bulb, intensity); }
		}

		public void OnLamp(Color color)
		{
			// Colour changes must retain the current intensity, including zero.
			if (_root._initialized) { _component.SetBulbColor(_bulb, color); }
		}

		public void OnChange(bool enabled) => OnLamp(enabled ? LampStatus.On : LampStatus.Off);

		/// <summary>The API of the bulb a mapping names, the first for an item it does not know.</summary>
		public IApiLamp Lamp(string deviceItem) => Bulb(InsertLightComponent.BulbIndex(deviceItem));
		public IApiWireDest Wire(string deviceItem) => Lamp(deviceItem);
	}
}
