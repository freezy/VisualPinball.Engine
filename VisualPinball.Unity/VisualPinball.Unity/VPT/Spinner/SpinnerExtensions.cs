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

using System;
using System.Collections.Generic;
using NLog;
using Unity.Entities;
using UnityEngine;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Spinner;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	internal static class SpinnerExtensions
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public static (IItemMainAuthoring, IEnumerable<IItemMeshAuthoring>) SetupGameObject(this Spinner spinner, GameObject obj, IItemMainAuthoring parentAuthoring)
		{
			var meshAuthoring = new List<IItemMeshAuthoring>();
			var mainAuthoring = obj.AddComponent<SpinnerAuthoring>().SetItem(spinner);

			switch (spinner.SubComponent) {
				case ItemSubComponent.None:
					obj.AddComponent<SpinnerColliderAuthoring>();
					meshAuthoring.Add(CreateChild<SpinnerBracketMeshAuthoring>(obj, SpinnerMeshGenerator.Bracket));

					var wireMeshAuth = CreateChild<SpinnerPlateMeshAuthoring>(obj, SpinnerMeshGenerator.Plate);
					wireMeshAuth.gameObject.AddComponent<SpinnerPlateAnimationAuthoring>();
					meshAuthoring.Add(wireMeshAuth);
					break;

				case ItemSubComponent.Collider: {
					Logger.Error("Cannot parent a spinner collider to a different object than a spinner!");
					break;
				}

				case ItemSubComponent.Mesh: {
					Logger.Error("Cannot parent a spinner mesh to a different object than a spinner!");
					break;
				}

				default:
					throw new ArgumentOutOfRangeException();
			}
			obj.AddComponent<ConvertToEntity>();
			return (mainAuthoring, meshAuthoring);
		}

		private static T CreateChild<T>(GameObject obj, string name) where T : MonoBehaviour, IItemMeshAuthoring
		{
			var subObj = new GameObject(name);
			subObj.transform.SetParent(obj.transform, false);
			var comp = subObj.AddComponent<T>();
			//subObj.layer = ChildObjectsLayer;
			return comp;
		}
	}
}
