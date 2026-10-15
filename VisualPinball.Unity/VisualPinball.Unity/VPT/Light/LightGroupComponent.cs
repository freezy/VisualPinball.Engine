// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
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
	[PackAs("LightGroup")]
	[AddComponentMenu("Pinball/Game Item/Light Group")]
	public class LightGroupComponent : MonoBehaviour, ILampDeviceComponent, IPackable
	{
		#region Data

		public List<ILampDeviceComponent> Lights {
			get => _lights.Select(l => l as ILampDeviceComponent).Where(l => l != null).ToList();
			set {
				_lights = value.Select(l => l as MonoBehaviour).ToArray();
				_lampIds = Array.Empty<string>();
			}
		}
		[SerializeField]
		[TypeRestriction(typeof(ILampDeviceComponent), PickerLabel = "Lamps", UpdateTransforms = false)]
		[Tooltip("The children of this light group. Can be lights or even other light groups.")]
		public MonoBehaviour[] _lights = Array.Empty<MonoBehaviour>();

		/// <summary>
		/// Which of its lamps each of <see cref="_lights"/> takes part with, by
		/// index, for lights that have more than one, like an insert with several
		/// bulbs. Empty, or missing, is the light's first or only lamp.
		/// </summary>
		[SerializeField]
		public string[] _lampIds = Array.Empty<string>();

		/// <summary>The lamp of the light at that index the group drives, or null for its first or only one.</summary>
		public string LampId(int index) => _lampIds != null && index >= 0 && index < _lampIds.Length && !string.IsNullOrEmpty(_lampIds[index]) ? _lampIds[index] : null;

		#endregion

		#region Packaging

		public byte[] Pack() => null;

		public byte[] PackReferences(Transform root, PackagedRefs refs, PackagedFiles files)
			=> LightGroupReferencesPackable.Pack(this, refs);

		public void Unpack(byte[] bytes) { }

		public void UnpackReferences(byte[] data, Transform root, PackagedRefs refs, PackagedFiles files)
			=> LightGroupReferencesPackable.Unpack(data, this, refs);

		#endregion

		public IApiLamp GetApi(Player player) => _api ??= new LightGroupApi(
			_lights
				.Select((light, index) => (Light: light as ILampDeviceComponent, LampId: LampId(index)))
				.Where(entry => entry.Light != null)
				.Select(entry => LampApi(entry.Light, entry.LampId, player))
				.ToArray()
		);

		/// <summary>The API of the light's lamp the group names, or the light's own for its first or only one.</summary>
		private static IApiLamp LampApi(ILampDeviceComponent light, string lampId, Player player)
		{
			var api = light.GetApi(player);
			return lampId != null && api is IApiLampDevice device ? device.Lamp(lampId) : api;
		}
		public IEnumerable<Light> LightSources => Lights.SelectMany(l => l.LightSources).ToArray();
		public Color LampColor => _lights.Length > 0 ? (_lights.First() as ILampDeviceComponent)!.LampColor : Color.blue;
		public LampStatus LampStatus => _lights.Length > 0 ? (_lights.First() as ILampDeviceComponent)!.LampStatus : LampStatus.Off;
		[NonSerialized]
		private LightGroupApi _api;

		// Without a domain or scene reload, the API would survive into the next
		// game with the lights and lamps it was made with.
		private void OnValidate() => _api = null;

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
				Logger.Warn("Cannot find player for lamp group {Name}. Is it part of a table?", name);
				return;
			}

			player.Register(GetApi(player), this);
		}

		#endregion
	}
}
