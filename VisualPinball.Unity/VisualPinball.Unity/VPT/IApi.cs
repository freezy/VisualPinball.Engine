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

using VisualPinball.Engine.Game.Engine;

namespace VisualPinball.Unity
{
	internal interface IApiInitializable
	{
		void OnInit();
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

	internal interface IApiSwitchable
	{
		void SetGamelogicEngine(IGamelogicEngineWithSwitches gle);
	}
}
