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
using Unity.Entities;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Engine.Math;
using Color = UnityEngine.Color;

namespace VisualPinball.Unity
{
	public interface IApi
	{
		string Name { get; }
		void OnDestroy();
	}

	internal interface IApiInitializable
	{
		void OnInit(BallManager ballManager);
	}

	public interface IApiColliderGenerator
	{
		void CreateColliders(Table table, List<ICollider> colliders);
		ColliderInfo GetColliderInfo();
		Entity ColliderEntity { get; }

		/// <summary>
		/// If false, this will be included in the quad tree but marked as inactive.
		/// </summary>
		bool IsColliderEnabled { get; }

		/// <summary>
		/// If false, this won't be included in the quad tree.
		/// </summary>
		bool IsColliderAvailable { get; }
	}

	public interface IApiHittable
	{
		void OnHit(Entity ballEntity, bool isUnHit = false);
		event EventHandler<HitEventArgs> Hit;
	}

	internal interface IApiRotatable
	{
		void OnRotate(float speed, bool direction);
	}

	internal interface IApiCollidable
	{
		void OnCollide(Entity ballEntity, float hit);
	}

	internal interface IApiSpinnable
	{
		void OnSpin();
	}

	internal interface IApiSlingshot
	{
		void OnSlingshot(Entity ballEntity);
	}

	internal interface IApiSwitch
	{
		/// <summary>
		/// Set up this switch to send its status to the gamelogic engine with the given ID.
		/// </summary>
		/// <param name="switchConfig">Config containing gamelogic engine's switch ID and pulse settings</param>
		IApiSwitchStatus AddSwitchDest(SwitchConfig switchConfig);

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

		void DestroyBall(Entity ballEntity);

		/// <summary>
		/// Event emitted when the trigger is switched on or off.
		/// </summary>
		///
		/// <remarks>
		/// If the ball triggered the switch, you'll get the ball entity as well.
		/// Note that for pulse switches, you currently only get the "closed" event.
		/// </remarks>
		event EventHandler<SwitchEventArgs> Switch;
	}

	internal interface IApiSwitchStatus
	{
		bool IsSwitchEnabled { get; }
		bool IsSwitchClosed { get; }
	}

	internal interface IApiSwitchDevice
	{
		IApiSwitch Switch(string switchId);
	}

	internal interface IApiCoilDevice
	{
		IApiCoil Coil(string coilId);
	}

	internal interface IApiCoil : IApiWireDest
	{
		void OnCoil(bool enabled, bool isHoldCoil);
	}

	internal interface IApiLamp : IApiWireDest
	{
		/// <summary>
		/// Sets the color of the light.
		/// </summary>
		Color Color { get; set; }

		/// <summary>
		/// Sets the light intensity to a given value between 0 and 1.
		/// </summary>
		/// <param name="value">0.0 = off, 1.0 = full intensity</param>
		/// <param name="channel">If channel is <see cref="ColorChannel.Alpha"/>, change intensity, otherwise update color.</param>
		void OnLamp(float value, ColorChannel channel);

		/// <summary>
		/// Sets the light color of the lamp.
		/// </summary>
		/// <param name="color">New color to set</param>
		void OnLampColor(Color color);
	}

	internal interface IApiWireDest
	{
		void OnChange(bool enabled);
	}

	internal interface IApiWireDeviceDest
	{
		IApiWireDest Wire(string coilId);
	}
}
