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

using NLog;
using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Spinner;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	public static class SpinnerExtensions
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public static IConvertedItem InstantiateGameObject(this Spinner spinner, IItem item, IMaterialProvider materialProvider)
		{
			var prefab = RenderPipeline.Current.PrefabProvider.CreateSpinner();
			var obj = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
			obj!.name = item.Name;
			return new ConvertedItem<Spinner, SpinnerData, SpinnerAuthoring>(obj);

			// var obj = new GameObject(item.Name);
			// var convertedItem = new ConvertedItem<Spinner, SpinnerData, SpinnerAuthoring>(obj, spinner);
			// switch (spinner.SubComponent) {
			// 	case ItemSubComponent.None:
			// 		convertedItem.SetColliderAuthoring<SpinnerColliderAuthoring>(materialProvider);
			// 		convertedItem.AddMeshAuthoring<SpinnerBracketMeshAuthoring>(SpinnerMeshGenerator.Bracket);
			// 		convertedItem.AddMeshAuthoring<SpinnerPlateMeshAuthoring>(SpinnerMeshGenerator.Plate);
			// 		convertedItem.SetAnimationAuthoring<SpinnerPlateAnimationAuthoring>(SpinnerMeshGenerator.Plate);
			// 		break;
			//
			// 	case ItemSubComponent.Collider: {
			// 		Logger.Warn("Cannot parent a spinner collider to a different object than a spinner!");
			// 		break;
			// 	}
			//
			// 	case ItemSubComponent.Mesh: {
			// 		Logger.Warn("Cannot parent a spinner mesh to a different object than a spinner!");
			// 		break;
			// 	}
			//
			// 	default:
			// 		throw new ArgumentOutOfRangeException();
			// }
			//
			// return convertedItem.AddConvertToEntity();
		}
	}
}
