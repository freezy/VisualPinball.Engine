// Visual Pinball Engine
// Copyright (C) 2022 freezy and VPE Team
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

// ReSharper disable InconsistentNaming

using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using UnityEngine;
using VisualPinball.Engine.Game.Engines;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Game Item/Light Group")]
	public class LightGroupComponent : MonoBehaviour, ILampDeviceComponent
	{
		public List<ILampDeviceComponent> Lights {
			get => _lights.Select(l => l as ILampDeviceComponent).Where(l => l != null).ToList();
			set => _lights = value.Select(l => l as MonoBehaviour).ToArray();
		}
		[SerializeField]
		[TypeRestriction(typeof(ILampDeviceComponent), PickerLabel = "Lamps", UpdateTransforms = false)]
		[Tooltip("The children of this light group. Can be lights or even other light groups.")]
		public MonoBehaviour[] _lights = Array.Empty<MonoBehaviour>();

		public IApiLamp GetApi(Player player) => _api ??= new LightGroupApi(
			Lights.Select(l => l.GetApi(player)).ToArray()
		);
		public IEnumerable<Light> LightSources => Lights.SelectMany(l => l.LightSources).ToArray();
		public Color LampColor => _lights.Length > 0 ? (_lights.First() as ILampDeviceComponent)!.LampColor : Color.blue;
		public LampStatus LampStatus => _lights.Length > 0 ? (_lights.First() as ILampDeviceComponent)!.LampStatus : LampStatus.Off;
		[NonSerialized]
		private LightGroupApi _api;

		#region Wiring

		public IEnumerable<GamelogicEngineLamp> AvailableLamps => new[] {
			new GamelogicEngineLamp(LightComponent.LampIdDefault),
		};

		public IEnumerable<GamelogicEngineLamp> AvailableDeviceItems => AvailableLamps;
		IEnumerable<IGamelogicEngineDeviceItem> IWireableComponent.AvailableWireDestinations => AvailableLamps;
		IEnumerable<IGamelogicEngineDeviceItem> IDeviceComponent<IGamelogicEngineDeviceItem>.AvailableDeviceItems => AvailableLamps;

		#endregion

		#region Runtime

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		private void Awake()
		{
			var player = GetComponentInParent<Player>();
			if (player == null) {
				Logger.Error($"Cannot find player for lamp group {name}.");
				return;
			}

			player.RegisterLampGroup(this);
		}

		#endregion
	}
}
