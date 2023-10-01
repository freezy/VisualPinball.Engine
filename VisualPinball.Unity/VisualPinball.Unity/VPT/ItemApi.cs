﻿// Visual Pinball Engine
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

using Unity.Entities;
using UnityEngine;
using VisualPinball.Engine.VPT;
using VisualPinballUnity;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Base class for all item APIs.
	/// </summary>
	/// <typeparam name="TData">Item data type</typeparam>
	/// <typeparam name="TComponent">Component Type</typeparam>
	[Api]
	public abstract class ItemApi<TComponent, TData>
		where TComponent : MainComponent<TData>
		where TData : ItemData
	{
		/// <summary>
		/// Item name
		/// </summary>
		public string Name => MainComponent ? MainComponent.name : "unlinked";

		protected readonly TComponent MainComponent;

		internal readonly GameObject GameObject;

		private protected TableApi TableApi => Player.TableApi;

		internal VisualPinballSimulationSystemGroup SimulationSystemGroup => World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<VisualPinballSimulationSystemGroup>();

		private protected readonly Player Player;
		private protected readonly SwitchHandler SwitchHandler;
		private protected BallManager BallManager;
		private protected TableComponent TableComponent;

		protected ItemApi(GameObject go, Player player)
		{
			GameObject = go;
			MainComponent = go.GetComponent<TComponent>();
			Player = player;
			SwitchHandler = new SwitchHandler(Name, player);
		}

		protected void OnInit(BallManager ballManager)
		{
			BallManager = ballManager;
			TableComponent = GameObject.GetComponentInParent<TableComponent>();
		}

		#region IApiSwitchable

		private protected DeviceSwitch CreateSwitch(string name, bool isPulseSwitch, SwitchDefault switchDefault = SwitchDefault.Configurable) => new DeviceSwitch(name, isPulseSwitch, switchDefault, Player);

		private protected IApiSwitchStatus AddSwitchDest(SwitchConfig switchConfig,IApiSwitchStatus switchStatus) => SwitchHandler.AddSwitchDest(switchConfig, switchStatus);

		internal void AddWireDest(WireDestConfig wireConfig) => SwitchHandler.AddWireDest(wireConfig);
		internal void RemoveWireDest(string destId) => SwitchHandler.RemoveWireDest(destId);

		private protected void OnSwitch(bool closed) => SwitchHandler.OnSwitch(closed);

		#endregion
	}
}
