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

// ReSharper disable InconsistentNaming

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using VisualPinball.Engine.Common;
using VisualPinball.Engine.Game.Engines;
using VisualPinball.Engine.VPT;

namespace VisualPinball.Unity
{
	[Serializable]
	public class MappingConfig
	{
		[SerializeField]
		public List<SwitchMapping> Switches = new List<SwitchMapping>();
		public List<CoilMapping> Coils = new List<CoilMapping>();

		private static void Retrieve<T>(IEnumerable node, List<T> components, Action<Transform, List<T>> action)
		{
			foreach (Transform childTransform in node) {
				action(childTransform, components);
				Retrieve(childTransform, components, action);
			}
		}

		#region Switches

		public void PopulateSwitches(GamelogicEngineSwitch[] engineSwitches, TableAuthoring tableComponent)
		{
			var switchDevices = tableComponent.GetComponentsInChildren<ISwitchDeviceAuthoring>();

			foreach (var engineSwitch in GetSwitchIds(engineSwitches)) {
				var switchMapping = Switches.FirstOrDefault(mappingsSwitchData => mappingsSwitchData.Id == engineSwitch.Id);
				if (switchMapping == null) {

					var description = engineSwitch.Description ?? string.Empty;
					var source = GuessSwitchSource(engineSwitch);
					var device = source == ESwitchSource.Playfield ? GuessDevice(switchDevices, engineSwitch) : null;
					var deviceItem = source == ESwitchSource.Playfield && device != null ? GuessDeviceSwitch(engineSwitch, device) : null;
					var inputActionMap = source == SwitchSource.InputSystem
						? string.IsNullOrEmpty(engineSwitch.InputMapHint) ? InputConstants.MapCabinetSwitches : engineSwitch.InputMapHint
						: string.Empty;
					var inputAction = source == SwitchSource.InputSystem
						? string.IsNullOrEmpty(engineSwitch.InputActionHint) ? string.Empty : engineSwitch.InputActionHint
						: string.Empty;

					AddSwitch(new SwitchMapping {
						Id = engineSwitch.Id,
						InternalId = engineSwitch.InternalId,
						IsNormallyClosed = engineSwitch.NormallyClosed,
						Description = description,
						Source = source,
						InputActionMap = inputActionMap,
						InputAction = inputAction,
						Device = device,
						DeviceSwitchId = deviceItem != null ? deviceItem.Id : string.Empty,
						Constant = engineSwitch.ConstantHint == SwitchConstantHint.AlwaysOpen ? SwitchConstant.Open : SwitchConstant.Closed
					});
				}
			}
		}


		/// <summary>
		/// Returns a sorted list of switch names from the gamelogic engine,
		/// appended with the additional names in the switch mapping. In short,
		/// the list of switch names to choose from.
		/// </summary>
		/// <param name="engineSwitches">Switch names provided by the gamelogic engine</param>
		/// <returns>All switch names</returns>
		public IEnumerable<GamelogicEngineSwitch> GetSwitchIds(GamelogicEngineSwitch[] engineSwitches)
		{
			var ids = new List<GamelogicEngineSwitch>();
			if (engineSwitches != null) {
				ids.AddRange(engineSwitches);
			}

			foreach (var mappingsSwitchData in Switches) {
				if (!ids.Exists(entry => entry.Id == mappingsSwitchData.Id))
				{
					ids.Add(new GamelogicEngineSwitch(mappingsSwitchData.Id));
				}
			}

			ids.Sort((s1, s2) => string.Compare(s1.Id, s2.Id, StringComparison.Ordinal));
			return ids;
		}

		private static ESwitchSource GuessSwitchSource(GamelogicEngineSwitch engineSwitch)
		{
			if (!string.IsNullOrEmpty(engineSwitch.DeviceHint)) {
				return ESwitchSource.Playfield;
			}

			if (engineSwitch.ConstantHint != SwitchConstantHint.None) {
				return ESwitchSource.Constant;
			}

			return !string.IsNullOrEmpty(engineSwitch.InputActionHint) ? ESwitchSource.InputSystem : ESwitchSource.Playfield;
		}

		private static ISwitchDeviceAuthoring GuessDevice(ISwitchDeviceAuthoring[] switchDevices, GamelogicEngineSwitch engineSwitch)
		{
			// match by regex if hint provided
			if (!string.IsNullOrEmpty(engineSwitch.DeviceHint)) {
				foreach (var device in switchDevices) {
					var regex = new Regex(engineSwitch.DeviceHint, RegexOptions.IgnoreCase);
					if (regex.Match(device.name).Success) {
						return device;
					}
				}
			}
			return null;
		}

		private static GamelogicEngineSwitch GuessDeviceSwitch(GamelogicEngineSwitch engineSwitch, ISwitchDeviceAuthoring device)
		{
			if (!string.IsNullOrEmpty(engineSwitch.DeviceItemHint)) {
				foreach (var deviceSwitch in device.AvailableSwitches) {
					var regex = new Regex(engineSwitch.DeviceItemHint, RegexOptions.IgnoreCase);
					if (regex.Match(deviceSwitch.Id).Success) {
						return deviceSwitch;
					}
				}
			}
			return null;
		}

		public void AddSwitch(SwitchMapping switchMapping)
		{
			Switches?.Add(switchMapping);
		}

		public void RemoveSwitch(SwitchMapping switchMapping)
		{
			Switches?.Remove(switchMapping);
		}

		public void RemoveAllSwitches()
		{
			if (Switches == null) {
				Switches = new List<SwitchMapping>();

			} else {
				Switches.Clear();
			}
		}

		#endregion
		public void AddCoil(CoilMapping coilMapping)
		{
			Coils?.Add(coilMapping);
		}

		public void RemoveCoil(CoilMapping coilMapping)
		{
			Coils.Remove(coilMapping);
			// todo
			// if (data.Destination == CoilDestination.Lamp) {
			// 	Lamps = Lamps.Where(l => l.Id == data.Id && l.Source == LampSource.Coils).ToArray();
			// }
		}
	}
}
