// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using UnityEngine;
using VisualPinball.Engine.Game.Engines;

namespace VisualPinball.Unity
{
	/// <summary>ROM and wire events for a directly mapped hybrid insert.</summary>
	public class InsertLightApi : IApiLamp, IApiWireDeviceDest
	{
		public event EventHandler Init;
		private readonly InsertLightComponent _component;
		private bool _initialized;

		internal InsertLightApi(InsertLightComponent component) => _component = component;

		public void OnInit(BallManager ballManager)
		{
			_initialized = true;
			Init?.Invoke(this, EventArgs.Empty);
		}

		public void OnDestroy()
		{
			_initialized = false;
			_component.ReleaseApiControl();
		}

		public void OnLamp(LampStatus status)
		{
			if (_initialized) { _component.SetLampStatus(status); }
		}

		public void OnLamp(float intensity)
		{
			// Fading/RGB mappings send an absolute normalized level without a separate
			// On status. A positive level must therefore also wake an off insert.
			if (_initialized) { _component.SetLampIntensity(intensity); }
		}

		public void OnLamp(Color color)
		{
			// Colour changes must retain the current intensity, including zero.
			if (_initialized) { _component.SetLampColor(color); }
		}

		public void OnChange(bool enabled) => OnLamp(enabled ? LampStatus.On : LampStatus.Off);
		public IApiWireDest Wire(string deviceItem) => this;
	}
}
