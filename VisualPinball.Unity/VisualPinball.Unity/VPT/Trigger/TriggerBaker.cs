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

using VisualPinball.Engine.VPT.Trigger;

namespace VisualPinball.Unity
{
	public class TriggerBaker : ItemBaker<TriggerComponent, TriggerData>
	{
		public override void Bake(TriggerComponent authoring)
		{
			base.Bake(authoring);
			
			var collComponent = GetComponentInChildren<TriggerColliderComponent>();
			var animComponent = GetComponentInChildren<TriggerAnimationComponent>();
			var meshComponent = GetComponentInChildren<TriggerMeshComponent>();
			if (collComponent && animComponent && meshComponent) {
				AddComponent(new TriggerAnimationData());
				AddComponent(new TriggerMovementData());
				AddComponent(new TriggerStaticData {
					AnimSpeed = animComponent.AnimSpeed,
					Radius = collComponent.HitCircleRadius,
					Shape = meshComponent.Shape,
					TableScaleZ = 1f
				});
			}

			// register
			GetComponentInParent<Player>().RegisterTrigger(authoring, GetEntity());
		}
	}
}
