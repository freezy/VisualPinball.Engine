// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
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
using UnityEngine;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Light values in a .vpe package are authored under HDRP in physical units (candela/lumen/lux)
	/// and assume HDRP's exposure-based tone mapping. A pipeline without physical light units (URP)
	/// registers an adjuster here from its bootstrap — the same seam as
	/// <see cref="VpeMaterialResolver.Register"/> — and <see cref="LightSourcePackable.Apply"/>
	/// invokes it after a restored light profile has been applied. When nothing is registered
	/// (HDRP, authoring editor), restored lights keep their authored values.
	/// </summary>
	public static class VpeLightUnitAdjuster
	{
		public static Action<Light> Active { get; private set; }

		public static void Register(Action<Light> adjuster) => Active = adjuster;
	}
}
