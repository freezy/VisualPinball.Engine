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

using System;
using NLog;
using UnityEngine;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Bumper;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	public static class BumperExtensions
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public static IConvertedItem SetupGameObject(this Bumper bumper, GameObject obj, IMaterialProvider materialProvider, bool componentsAdded)
		{
			var convertedItem = new ConvertedItem<Bumper, BumperData, BumperAuthoring>(obj, bumper, componentsAdded);
			switch (bumper.SubComponent) {
				case ItemSubComponent.None:
					convertedItem.SetColliderAuthoring<BumperColliderAuthoring>(materialProvider, componentsAdded);
					convertedItem.AddMeshAuthoring<BumperBaseMeshAuthoring>(BumperMeshGenerator.Base, componentsAdded);
					convertedItem.AddMeshAuthoring<BumperCapMeshAuthoring>(BumperMeshGenerator.Cap, componentsAdded);
					convertedItem.AddMeshAuthoring<BumperRingMeshAuthoring>(BumperMeshGenerator.Ring, componentsAdded);
					convertedItem.AddMeshAuthoring<BumperSkirtMeshAuthoring>(BumperMeshGenerator.Skirt, componentsAdded);

					convertedItem.SetAnimationAuthoring<BumperRingAnimationAuthoring>(BumperMeshGenerator.Ring);
					convertedItem.SetAnimationAuthoring<BumperSkirtAnimationAuthoring>(BumperMeshGenerator.Skirt);
					break;

				case ItemSubComponent.Collider: {
					Logger.Warn("Bumper collider cannot be parented to anything else than bumpers.");
					break;
				}

				case ItemSubComponent.Mesh: {
					Logger.Warn("Bumper mesh cannot be parented to anything else than bumpers.");
					break;
				}

				default:
					throw new ArgumentOutOfRangeException();
			}

			return convertedItem.AddConvertToEntity(componentsAdded);
		}
	}
}
