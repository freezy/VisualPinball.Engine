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

// ReSharper disable MemberCanBePrivate.Global

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VisualPinball.Unity
{
	public struct LightGroupReferencesPackable
	{
		public ReferencePackable[] Lights;

		/// <summary>The lamp of each light the group drives, by index, null for its first or only one. Missing in older packages.</summary>
		public string[] LampIds;

		public static byte[] Pack(LightGroupComponent comp, PackagedRefs refs)
		{
			return PackageApi.Packer.Pack(new LightGroupReferencesPackable {
				Lights = refs.PackReferences(comp._lights).ToArray(),
				LampIds = comp._lights.Select((_, i) => comp.LampId(i)).ToArray(),
			});
		}

		public static void Unpack(byte[] bytes, LightGroupComponent comp, PackagedRefs refs)
		{
			var data = PackageApi.Packer.Unpack<LightGroupReferencesPackable>(bytes);
			// light by light, so each keeps its lamp when one can't be resolved.
			var lights = new List<MonoBehaviour>();
			var lampIds = new List<string>();
			for (var i = 0; i < (data.Lights?.Length ?? 0); i++) {
				var light = refs.Resolve<MonoBehaviour, ILampDeviceComponent>(data.Lights[i]);
				if (light == null) {
					continue;
				}
				lights.Add(light);
				lampIds.Add(data.LampIds != null && i < data.LampIds.Length ? data.LampIds[i] : null);
			}
			comp._lights = lights.ToArray();
			comp._lampIds = lampIds.ToArray();
		}
	}
}
