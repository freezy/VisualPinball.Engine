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
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using VisualPinball.Engine.Common;
using VisualPinball.Engine.Game.Engines;
using VisualPinball.Engine.Math;

namespace VisualPinball.Unity
{
	[Serializable]
	public class MappingConfig
	{
		[SerializeField] public List<SwitchMapping> Switches = new List<SwitchMapping>();
		[SerializeField] public List<CoilMapping> Coils = new List<CoilMapping>();
		[SerializeField] public List<WireMapping> Wires = new List<WireMapping>();
		[SerializeField] public List<LampMapping> Lamps = new List<LampMapping>();

		public bool IsEmpty() => (Coils == null || Coils.Count == 0) && (Switches == null || Switches.Count == 0) && (Lamps == null || Lamps.Count == 0);

		public void Clear()
		{
			Switches.Clear();
			Coils.Clear();
			Wires.Clear();
			Lamps.Clear();
		}

		#region Switches

		public void PopulateSwitches(GamelogicEngineSwitch[] engineSwitches, TableComponent tableComponent)
		{
			var switchDevices = tableComponent.GetComponentsInChildren<ISwitchDeviceComponent>();

			foreach (var engineSwitch in GetSwitchIds(engineSwitches)) {
				var switchMapping = Switches.FirstOrDefault(mappingsSwitchData => mappingsSwitchData.Id == engineSwitch.Id);
				if (switchMapping != null) {
					continue;
				}

				var description = engineSwitch.Description ?? string.Empty;
				var source = GuessSwitchSource(engineSwitch);
				var device = source == SwitchSource.Playfield ? GuessSwitchDevice(switchDevices, engineSwitch) : null;
				var deviceItem = source == SwitchSource.Playfield && device != null ? GuessSwitchDeviceItem(engineSwitch, device) : null;
				var inputActionMap = source == SwitchSource.InputSystem
					? string.IsNullOrEmpty(engineSwitch.InputMapHint) ? InputConstants.MapCabinetSwitches : engineSwitch.InputMapHint
					: string.Empty;
				var inputAction = source == SwitchSource.InputSystem
					? string.IsNullOrEmpty(engineSwitch.InputActionHint) ? string.Empty : engineSwitch.InputActionHint
					: string.Empty;

				// if there was a device match, device has only one item and there was a hint that didn't match, clear the device.
				if (device != null && deviceItem == null && !string.IsNullOrEmpty(engineSwitch.DeviceItemHint) && device.AvailableSwitches.Count() == 1) {
					device = null;
				}

				AddSwitch(new SwitchMapping {
					Id = engineSwitch.Id,
					InternalId = engineSwitch.InternalId,
					IsNormallyClosed = engineSwitch.NormallyClosed,
					Description = description,
					Source = source,
					InputActionMap = inputActionMap,
					InputAction = inputAction,
					Device = device,
					DeviceItem = deviceItem != null ? deviceItem.Id : string.Empty,
					Constant = engineSwitch.ConstantHint == SwitchConstantHint.AlwaysOpen ? SwitchConstant.Open : SwitchConstant.Closed
				});
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

			foreach (var switchMapping in Switches.Where(mappingsSwitchData => !ids.Exists(sw => sw.Id == mappingsSwitchData.Id))) {
				ids.Add(new GamelogicEngineSwitch(switchMapping.Id));
			}

			ids.Sort((sw1, sw2) => string.Compare(sw1.Id, sw2.Id, StringComparison.Ordinal));
			return ids;
		}

		private static SwitchSource GuessSwitchSource(GamelogicEngineSwitch engineSwitch)
		{
			if (!string.IsNullOrEmpty(engineSwitch.DeviceHint)) {
				return SwitchSource.Playfield;
			}

			if (engineSwitch.ConstantHint != SwitchConstantHint.None) {
				return SwitchSource.Constant;
			}

			return !string.IsNullOrEmpty(engineSwitch.InputActionHint) ? SwitchSource.InputSystem : SwitchSource.Playfield;
		}

		private static ISwitchDeviceComponent GuessSwitchDevice(ISwitchDeviceComponent[] switchDevices, GamelogicEngineSwitch engineSwitch)
		{
			// if no hint, match by name
			if (string.IsNullOrEmpty(engineSwitch.DeviceHint)) {
				var matchKey = int.TryParse(engineSwitch.Id, out var numericSwitchId)
					? $"sw{numericSwitchId}"
					: engineSwitch.Id;

				var swMatch = switchDevices.FirstOrDefault(sw => sw.name == matchKey);
				if (swMatch != null) {
					return swMatch;
				}
			}

			// if no match and no hint, return.
			if (string.IsNullOrEmpty(engineSwitch.DeviceHint)) {
				return null;
			}

			// match by regex
			foreach (var device in switchDevices) {
				var regex = new Regex(engineSwitch.DeviceHint, RegexOptions.IgnoreCase);
				if (regex.Match(device.name).Success) {
					return device;
				}
			}
			return null;
		}

		private static GamelogicEngineSwitch GuessSwitchDeviceItem(GamelogicEngineSwitch engineSwitch, ISwitchDeviceComponent device)
		{
			// if there's only one switch, it's the one.
			if (device.AvailableSwitches.Count() == 1 && string.IsNullOrEmpty(engineSwitch.DeviceItemHint)) {
				return device.AvailableSwitches.First();
			}

			// otherwise, if not hint, we can't know.
			if (string.IsNullOrEmpty(engineSwitch.DeviceItemHint)) {
				return null;
			}

			// match by regex
			foreach (var deviceSwitch in device.AvailableSwitches) {
				var regex = new Regex(engineSwitch.DeviceItemHint, RegexOptions.IgnoreCase);
				if (regex.Match(deviceSwitch.Id).Success) {
					return deviceSwitch;
				}
			}
			return null;
		}

		public void AddSwitch(SwitchMapping switchMapping = null)
		{
			Switches.Add(switchMapping ?? new SwitchMapping());
		}

		public void RemoveSwitch(SwitchMapping switchMapping)
		{
			Switches.Remove(switchMapping);
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

		#region Coils

		/// <summary>
		/// Auto-matches the coils provided by the gamelogic engine with the
		/// coils on the playfield.
		/// </summary>
		/// <param name="engineCoils">List of coils provided by the gamelogic engine</param>
		/// <param name="tableComponent">Table component</param>
		public void PopulateCoils(GamelogicEngineCoil[] engineCoils, TableComponent tableComponent)
		{
			var coilDevices = tableComponent.GetComponentsInChildren<ICoilDeviceComponent>();
			foreach (var engineCoil in GetCoils(engineCoils)) {

				var coilMapping = Coils.FirstOrDefault(mappingsCoilData => mappingsCoilData.Id == engineCoil.Id);
				if (coilMapping != null || engineCoil.IsUnused) {
					continue;
				}

				var destination = GuessCoilDestination(engineCoil);
				var description = string.IsNullOrEmpty(engineCoil.Description) ? string.Empty : engineCoil.Description;
				var device = destination == CoilDestination.Playfield ? GuessCoilDevice(coilDevices, engineCoil) : null;
				var deviceItem = destination == CoilDestination.Playfield && device != null ? GuessCoilDeviceItem(engineCoil, device) : null;

				// if there was a device match, device has only one item and there was a hint that didn't match, clear the device.
				if (device != null && deviceItem == null && !string.IsNullOrEmpty(engineCoil.DeviceItemHint) && device.AvailableCoils.Count() == 1) {
					device = null;
				}

				AddCoil(new CoilMapping {
					Id = engineCoil.Id,
					InternalId = engineCoil.InternalId,
					Description = description,
					Destination = destination,
					Device = device,
					DeviceItem = deviceItem != null ? deviceItem.Id : string.Empty,
				});
			}
		}

		private CoilDestination GuessCoilDestination(GamelogicEngineCoil engineCoil)
		{
			if (!engineCoil.IsLamp) {
				return CoilDestination.Playfield;
			}

			// if it's a lamp, add a new entry to the lamps.
			AddLamp(new LampMapping {
				Id = engineCoil.Id,
				Description = engineCoil.Description,
				Source = LampSource.Lamp,
				IsCoil = true,
			});
			return CoilDestination.Lamp;
		}

		private static ICoilDeviceComponent GuessCoilDevice(IEnumerable<ICoilDeviceComponent> coilDevices, GamelogicEngineCoil engineCoil)
		{
			foreach (var device in coilDevices) {
				if (string.Equals(device.name, engineCoil.Id, StringComparison.OrdinalIgnoreCase)) {
					return device;
				}
				if (string.IsNullOrEmpty(engineCoil.DeviceHint)) {
					continue;
				}
				var regex = new Regex(engineCoil.DeviceHint, RegexOptions.IgnoreCase);
				if (regex.Match(device.name).Success) {
					return device;
				}
			}
			return null;
		}

		private static GamelogicEngineCoil GuessCoilDeviceItem(GamelogicEngineCoil engineCoil, ICoilDeviceComponent device)
		{
			// if only one device item available, it's the one.
			if (device.AvailableCoils.Count() == 1 && string.IsNullOrEmpty(engineCoil.DeviceItemHint)) {
				return device.AvailableCoils.First();
			}

			// no hint, no guess..
			if (string.IsNullOrEmpty(engineCoil.DeviceItemHint)) {
				return null;
			}

			// match by regex if hint provided
			foreach (var deviceCoil in device.AvailableCoils) {
				var regex = new Regex(engineCoil.DeviceItemHint, RegexOptions.IgnoreCase);
				if (regex.Match(deviceCoil.Id).Success) {
					return deviceCoil;
				}
			}
			return null;
		}

		/// <summary>
		/// Returns a sorted list of coil names from the gamelogic engine,
		/// appended with the additional names in the coil mapping. In short,
		/// the list of coil names to choose from.
		/// </summary>
		/// <param name="engineCoils">Coil names provided by the gamelogic engine</param>
		/// <returns>All coil names</returns>
		public IEnumerable<GamelogicEngineCoil> GetCoils(GamelogicEngineCoil[] engineCoils)
		{
			var coils = new List<GamelogicEngineCoil>();

			// first, add coils from the gamelogic engine
			if (engineCoils != null) {
				coils.AddRange(engineCoils);
			}

			// then add coil ids that were added manually
			foreach (var coilMapping in Coils.Where(coilMapping => !coils.Exists(entry => entry.Id == coilMapping.Id))) {
				coils.Add(new GamelogicEngineCoil(coilMapping.Id));
			}

			coils.Sort((s1, s2) => string.Compare(s1.Id, s2.Id, StringComparison.Ordinal));
			return coils;
		}

		public void AddCoil(CoilMapping coilMapping)
		{
			Coils.Add(coilMapping);
		}

		public void RemoveCoil(CoilMapping coilMapping)
		{
			Coils.Remove(coilMapping);
			if (coilMapping.Destination == CoilDestination.Lamp) {
				var lamp = Lamps.FirstOrDefault(l => l.Id == coilMapping.Id && l.IsCoil);
				if (lamp != null) {
					Lamps.Remove(lamp);
				}
			}
		}

		public void RemoveAllCoils()
		{
			Coils.Clear();
			Lamps = Lamps.Where(l => !l.IsCoil).ToList();
		}

		#endregion

		#region Wires

		public void AddWire(WireMapping wireMapping)
		{
			Wires.Add(wireMapping);
		}

		public void RemoveWire(WireMapping wireMapping)
		{
			Wires.Remove(wireMapping);
		}

		public void RemoveAllWires()
		{
			Wires.Clear();
		}

		#endregion

		#region Lamps

		/// <summary>
		/// Auto-matches the lamps provided by the gamelogic engine with the
		/// lamps on the playfield.
		/// </summary>
		/// <param name="engineLamps">List of lamps provided by the gamelogic engine</param>
		/// <param name="tableComponent">Table component</param>
		public void PopulateLamps(GamelogicEngineLamp[] engineLamps, TableComponent tableComponent)
		{
			var lamps = tableComponent.GetComponentsInChildren<ILampDeviceComponent>();
			foreach (var engineLamp in GetLamps(engineLamps)) {

				var lampMapping = Lamps.FirstOrDefault(mappingsLampData => mappingsLampData.Id == engineLamp.Id && !mappingsLampData.IsCoil);
				if (lampMapping != null) {
					continue;
				}

				var description = string.IsNullOrEmpty(engineLamp.Description) ? string.Empty : engineLamp.Description;
				var device = GuessLampDevice(lamps, engineLamp);
				var deviceItem = device != null ? GuessLampDeviceItem(engineLamp, device) : null;

				AddLamp(new LampMapping {
					Id = engineLamp.Id,
					Channel = engineLamp.Channel,
					Source = engineLamp.Source,
					Description = description,
					Device = device,
					DeviceItem = deviceItem != null ? deviceItem.Id : string.Empty,
					Type = GuessLampType(engineLamp),
					FadingSteps = engineLamp.FadingSteps,
				});
			}
		}

		/// <summary>
		/// Returns a sorted list of lamp names from the gamelogic engine,
		/// appended with the additional names in the lamp mapping. In short,
		/// the list of lamp names to choose from.
		/// </summary>
		/// <param name="engineLamps">Lamp names provided by the gamelogic engine</param>
		/// <returns>All lamp names</returns>
		public IEnumerable<GamelogicEngineLamp> GetLamps(GamelogicEngineLamp[] engineLamps)
		{
			var lamps = new List<GamelogicEngineLamp>();

			// first, add lamps from the gamelogic engine
			if (engineLamps != null) {
				lamps.AddRange(engineLamps);
			}

			// then add lamp ids that were added manually
			foreach (var lampMapping in Lamps.Where(lampMapping => !lamps.Exists(entry => entry.Id == lampMapping.Id))) {
				lamps.Add(new GamelogicEngineLamp(lampMapping.Id));
			}

			lamps.Sort((s1, s2) => string.Compare(s1.Id, s2.Id, StringComparison.Ordinal));
			return lamps;
		}

		private static ILampDeviceComponent GuessLampDevice(ILampDeviceComponent[] lamps, GamelogicEngineLamp engineLamp)
		{
			// first, match by regex if hint provided
			if (!string.IsNullOrEmpty(engineLamp.DeviceHint)) {
				foreach (var lamp in lamps) {
					var regex = new Regex(engineLamp.DeviceHint, RegexOptions.IgnoreCase);
					if (regex.Match(lamp.name).Success) {
						return lamp;
					}
				}
			}

			// second, match by "lXX" or name
			var matchKey = int.TryParse(engineLamp.Id, out var numericLampId)
				? $"l{numericLampId}"
				: engineLamp.Id;

			return lamps.FirstOrDefault(l => string.Equals(l.name, matchKey, StringComparison.OrdinalIgnoreCase));
		}

		private static GamelogicEngineLamp GuessLampDeviceItem(GamelogicEngineLamp engineLamp, ILampDeviceComponent device)
		{
			if (device == null) {
				return null;
			}

			// if only one device item available, it's the one.
			if (device.AvailableLamps.Count() == 1 && string.IsNullOrEmpty(engineLamp.DeviceItemHint)) {
				return device.AvailableLamps.First();
			}

			// no hint, no guess..
			if (string.IsNullOrEmpty(engineLamp.DeviceItemHint)) {
				return null;
			}

			// match by regex if hint provided
			foreach (var deviceLamp in device.AvailableLamps) {
				var regex = new Regex(engineLamp.DeviceItemHint, RegexOptions.IgnoreCase);
				if (regex.Match(deviceLamp.Id).Success) {
					return deviceLamp;
				}
			}
			return null;
		}

		private static LampType GuessLampType(GamelogicEngineLamp engineLamp)
		{
			if (engineLamp.Channel != ColorChannel.Alpha) {
				return LampType.RgbMulti;
			}

			return engineLamp.FadingSteps > 0
				? LampType.SingleFading
				: LampType.SingleOnOff;
		}

		public void AddLamp(LampMapping lampMapping)
		{
			Lamps.Add(lampMapping);
		}

		public void RemoveLamp(LampMapping lampMapping)
		{
			Lamps.Remove(lampMapping);
		}

		public void RemoveAllLamps()
		{
			Lamps = Lamps.Where(l => l.IsCoil).ToList();
		}

		#endregion

	}
}
