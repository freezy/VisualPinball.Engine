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

		public static ConvertedItem SetupGameObject(this Spinner spinner, GameObject obj)
		{
			var meshAuthoring = new List<IItemMeshAuthoring>();
			var mainAuthoring = obj.AddComponent<SpinnerAuthoring>().SetItem(spinner);
			SpinnerColliderAuthoring colliderAuthoring = null;

			switch (spinner.SubComponent) {
				case ItemSubComponent.None:
					colliderAuthoring = obj.AddComponent<SpinnerColliderAuthoring>();
					meshAuthoring.Add(ConvertedItem.CreateChild<SpinnerBracketMeshAuthoring>(obj, SpinnerMeshGenerator.Bracket));

					var wireMeshAuth = ConvertedItem.CreateChild<SpinnerPlateMeshAuthoring>(obj, SpinnerMeshGenerator.Plate);
					wireMeshAuth.gameObject.AddComponent<SpinnerPlateAnimationAuthoring>();
					meshAuthoring.Add(wireMeshAuth);
					break;

				case ItemSubComponent.Collider: {
					Logger.Warn("Cannot parent a spinner collider to a different object than a spinner!");
					break;
				}

				case ItemSubComponent.Mesh: {
					Logger.Warn("Cannot parent a spinner mesh to a different object than a spinner!");
					break;
				}

				default:
					throw new ArgumentOutOfRangeException();
			}
			obj.AddComponent<ConvertToEntity>();
			return new ConvertedItem(mainAuthoring, meshAuthoring, colliderAuthoring);
		}
	}
}
