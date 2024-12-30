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

using System;
using Unity.Mathematics;
using UnityEngine;
using VisualPinball.Engine.Game.Engines;

namespace VisualPinball.Unity
{
	/// <summary>
	/// The common base interface of all API implementations. <see cref="ItemApi{TItemComponent,TData}"/> implements this.
	/// </summary>
	public interface IApi
	{
		public event EventHandler Init;

		void OnInit(BallManager ballManager);

		void OnDestroy();
	}

	/// <summary>
	/// APIs with this interface represent collidable objects and can generate colliders. <see cref="CollidableApi{TComponent,TCollidableComponent,TData}"/> implements this.
	/// </summary>
	public interface IApiColliderGenerator
	{
		/// <summary>
		/// Create colliders and add them to the provided list.
		/// </summary>
		/// <param name="colliders">List to add colliders to.</param>
		/// <param name="translateWithinPlayfieldMatrix"></param>
		/// <param name="margin"></param>
		void CreateColliders(ref ColliderReference colliders, float4x4 translateWithinPlayfieldMatrix, float margin);

		/// <summary>
		/// Computes collider info based on the component data.
		/// </summary>
		/// <returns></returns>
		ColliderInfo GetColliderInfo();

		/// <summary>
		/// If false, this won't be included in the quad tree.
		/// </summary>
		bool IsColliderAvailable { get; }
	}

	/// <summary>
	/// Hittable APIs send events when the ball hits the game item.
	/// </summary>
	public interface IApiHittable
	{
		/// <summary>
		/// Internally called when the game item was hit.
		/// </summary>
		/// <param name="ballEntity">Which ball</param>
		/// <param name="isUnHit">Whether it exited the hittable area</param>
		void OnHit(int ballId, bool isUnHit = false);

		/// <summary>
		/// Public event to subscribe to for hits.
		/// </summary>
		event EventHandler<HitEventArgs> Hit;
	}

	/// <summary>
	/// Internal interface to group rotatable items (EOS / BOS).
	/// </summary>
	internal interface IApiRotatable
	{
		/// <summary>
		/// Internally called when the rotation event occurred.
		/// </summary>
		/// <param name="speed">Rotation speed</param>
		/// <param name="direction">Rotation direction</param>
		void OnRotate(float speed, bool direction);
	}

	/// <summary>
	/// Internal interface for collidable APIs (currently only the flipper)
	/// </summary>
	internal interface IApiCollidable
	{
		void OnCollide(int ballId, float hit);
	}

	/// <summary>
	/// Internal interface to group spinnable items (currently on the spinner)
	/// </summary>
	internal interface IApiSpinnable
	{
		void OnSpin();
	}

	/// <summary>
	/// Internal interface for slingshots.
	/// </summary>
	internal interface IApiSlingshot
	{
		void OnSlingshot(int ballId);
	}

	/// <summary>
	/// Internal interface for droppables.
	/// </summary>
	internal interface IApiDroppable
	{
		void OnDropStatusChanged(bool isDropped, int ballId);
	}

	/// <summary>
	/// This interface makes the implementation act as a switch that can emit switch events to
	/// one or many destinations.
	/// </summary>
	///
	/// <remarks>
	/// Note that the actual switch implementation is done at <see cref="SwitchHandler"/> for all
	/// game items. The items just instantiate and forward data to it. <p/>
	///
	/// Also note that this interface only handles a single switch. Since all switchable game items
	/// implement <see cref="IApiSwitchDevice"/>, and the ones that only support one switch also
	/// implement <see cref="IApiSwitch"/> and return themself at <see cref="IApiSwitchDevice.Switch"/>.
	/// </remarks>
	public interface IApiSwitch
	{
		/// <summary>
		/// Set up this switch to send its status to the gamelogic engine with the given ID.
		/// </summary>
		/// <param name="switchConfig">Config containing gamelogic engine's switch ID and pulse settings</param>
		/// <param name="switchStatus">Since multiple switch destinations can map to a switch, we might already have a status object.</param>
		IApiSwitchStatus AddSwitchDest(SwitchConfig switchConfig, IApiSwitchStatus switchStatus);

		/// <summary>
		/// Set up this switch to directly trigger another game item (coil or lamp), or
		/// a coil within a coil device.
		/// </summary>
		/// <param name="wireConfig">Configuration which game item to link to</param>
		void AddWireDest(WireDestConfig wireConfig);

		/// <summary>
		/// Removes a wire destination for this switch.
		/// </summary>
		/// <param name="destId"></param>
		void RemoveWireDest(string destId);

		/// <summary>
		/// Event emitted when the trigger is switched on or off.
		/// </summary>
		///
		/// <remarks>
		/// If the ball triggered the switch, you'll get the ball entity as well.
		/// Note that for pulse switches, you currently only get the "closed" event.
		/// </remarks>
		event EventHandler<SwitchEventArgs> Switch;

		/// <summary>
		/// True if switch is enabled, false otherwise. Note that enabled != closed.
		/// </summary>
		bool IsSwitchEnabled { get; }
	}

	/// <summary>
	/// This interface abstracts objects that can represent a switch status (keyboard, constant, items).
	/// </summary>
	public interface IApiSwitchStatus
	{
		/// <summary>
		/// True if switch is enabled, false otherwise. Note that enabled != closed.
		/// </summary>
		bool IsSwitchEnabled { get; set; }

		/// <summary>
		/// True if switch is closed, false otherwise.
		/// </summary>
		bool IsSwitchClosed { get; set; }
	}

	/// <summary>
	/// This interface makes the implementation act as a device with one or more switches that can
	/// each emit switch events to one or many destinations.
	/// </summary>
	///
	/// <remarks>
	/// Game items used to support both switches and switch devices, and in the Switch Manager, the
	/// user needed to select between "Playfield" (game items with one switch) and "Device" (game
	/// items with two or more switches). <br />
	/// This has been changed. Now all game items are devices and the Switch Manager's UI adapts
	/// accordingly.
	/// </remarks>
	internal interface IApiSwitchDevice
	{
		/// <summary>
		/// Which switch to return.
		/// </summary>
		/// <param name="deviceItem">Name of the actual switch</param>
		/// <returns>Switch or <c>null</c> if not found.</returns>
		IApiSwitch Switch(string deviceItem);
	}

	/// <summary>
	/// This interface makes the implementation act as a coil that consumes coil events.
	/// </summary>
	public interface IApiCoil : IApiWireDest
	{
		/// <summary>
		/// The coil status changed.
		/// </summary>
		/// <param name="enabled">Whether the coil was enabled (power) or disabled (no power).</param>
		void OnCoil(bool enabled);
	}

	/// <summary>
	/// This interface makes the implementation act as a device with one or more coils that can
	/// each consume coil events.
	/// </summary>
	internal interface IApiCoilDevice
	{
		/// <summary>
		/// Which coil to return.
		/// </summary>
		/// <param name="deviceItem">Name of the actual coil</param>
		/// <returns></returns>
		IApiCoil Coil(string deviceItem);
	}

	/// <summary>
	/// A game item that acts a lamp that can either receive a float value or a color.
	/// </summary>
	public interface IApiLamp : IApiWireDest, IApi
	{
		void OnLamp(LampStatus newStatus);
		void OnLamp(float intensity);
		void OnLamp(Color color);
	}

	/// <summary>
	/// This interface groups devices that can receive binary data, currently coils and lamps.
	/// </summary>
	public interface IApiWireDest
	{
		/// <summary>
		/// Changes the status at the destination.
		/// </summary>
		/// <param name="enabled"></param>
		void OnChange(bool enabled);
	}

	/// <summary>
	/// A game item that can act as one or more wire destinations.
	/// </summary>
	public interface IApiWireDeviceDest
	{
		/// <summary>
		/// Which wire destination to return.
		/// </summary>
		/// <param name="deviceItem"></param>
		/// <returns></returns>
		IApiWireDest Wire(string deviceItem);
	}
}
