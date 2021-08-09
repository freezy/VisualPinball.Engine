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

using UnityEngine;
using VisualPinball.Engine.VPT;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Common interface creating VPE's game item prefabs.
	/// </summary>
	public interface IPrefabProvider
	{
		/// <summary>
		/// Creates a bumper prefab.
		/// </summary>
		GameObject CreateBumper();

		/// <summary>
		/// Creates a gate prefab.
		/// </summary>
		/// <param name="type">Type of the game</param>
		/// <returns>Prefab of the gate (still needs to be instantiated)</returns>
		/// <see cref="GateType"/>
		GameObject CreateGate(int type);

		/// <summary>
		/// Creates a target prefab.
		/// </summary>
		/// <param name="type">Target type</param>
		/// <returns>Prefab of the gate (still needs to be instantiated)</returns>
		/// <see cref="TargetType"/>
		GameObject CreateTarget(int type);

		/// <summary>
		/// Creates a kicker prefab.
		/// </summary>
		/// <param name="type">Kicker type</param>
		/// <returns>Prefab of the kicker (still needs to be instantiated)</returns>
		/// <see cref="KickerType"/>
		GameObject CreateKicker(int type);
	}
}
