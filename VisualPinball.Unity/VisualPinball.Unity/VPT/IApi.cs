// Visual Pinball Engine
// Copyright (C) 2020 freezy and VPE Team
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

	internal interface IApiHittable
	{
		void OnHit(bool isUnHit = false);
	}

	internal interface IApiRotatable
	{
		void OnRotate(float speed, bool direction);
	}

	internal interface IApiCollidable
	{
		void OnCollide(float hit);
	}

	internal interface IApiSpinnable
	{
		void OnSpin();
	}

	internal interface IApiSlingshot
	{
		void OnSlingshot();
	}

	internal interface IApiSwitch
	{
		void AddSwitchId(SwitchConfig switchConfig);
		void AddWireDest(WireDestConfig wireConfig);
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

	internal interface IApiWireDest
	{
		void OnChange(bool enabled);
	}

	internal interface IApiWireDeviceDest
	{
		IApiWireDest Wire(string coilId);
	}
}
