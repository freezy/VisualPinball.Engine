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

using Unity.Entities;
using UnityEngine;
using VisualPinball.Engine.Game;

namespace VisualPinball.Unity
{
	internal static class SpinnerExtensions
	{
		public static SpinnerAuthoring SetupGameObject(this Engine.VPT.Spinner.Spinner spinner, GameObject obj, RenderObjectGroup rog)
		{
			var ic = obj.AddComponent<SpinnerAuthoring>().SetItem(spinner);
			obj.AddComponent<ConvertToEntity>();

			var wire = obj.transform.Find("Plate").gameObject;
			wire.AddComponent<SpinnerPlateAuthoring>().SetItem(spinner, "Plate");

			return ic as SpinnerAuthoring;
		}
	}
}
