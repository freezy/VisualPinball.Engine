﻿// Visual Pinball Engine
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

using System.Text;
using VisualPinball.Engine.VPT.Light;
using Light = UnityEngine.Light;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Common interface for light conversion with the various render pipelines
	/// </summary>
	public interface ILightConverter
	{
		/// <summary>
		/// Updates a Unity light based on VPX's light data.
		/// </summary>
		/// <param name="light">Which light to update</param>
		/// <param name="data">VPX light data</param>
		void UpdateLight(Light light, LightData data);
	}
}
