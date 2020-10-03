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
using VisualPinball.Engine.VPT.Plunger;

namespace VisualPinball.Unity
{
	internal static class PlungerExtensions
	{
		public static PlungerAuthoring SetupGameObject(this Plunger plunger, GameObject obj, RenderObjectGroup rog)
		{
			var ic = obj.AddComponent<PlungerAuthoring>().SetItem(plunger);

			var rod = obj.transform.Find(PlungerMeshGenerator.RodName);
			if (rod != null) {
				rod.gameObject.AddComponent<PlungerRodAuthoring>();
			}

			var spring = obj.transform.Find(PlungerMeshGenerator.SpringName);
			if (spring != null) {
				spring.gameObject.AddComponent<PlungerSpringAuthoring>();
			}

			var flat = obj.transform.Find(PlungerMeshGenerator.FlatName);
			if (flat != null) {
				flat.gameObject.AddComponent<PlungerFlatAuthoring>();
			}

			obj.AddComponent<ConvertToEntity>();
			return ic as PlungerAuthoring;
		}
	}
}
