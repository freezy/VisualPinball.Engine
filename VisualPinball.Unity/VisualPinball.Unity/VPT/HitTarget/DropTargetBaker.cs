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

using VisualPinball.Engine.VPT.HitTarget;

namespace VisualPinball.Unity
{
	public class DropTargetBaker : ItemBaker<DropTargetComponent, HitTargetData>
	{
		public override void Bake(DropTargetComponent authoring)
		{
			base.Bake(authoring);
			
			var colliderComponent = GetComponent<DropTargetColliderComponent>();
			var animationComponent = GetComponentInChildren<DropTargetAnimationComponent>();
			if (colliderComponent && animationComponent) {

				AddComponent(new DropTargetStaticData {
					Speed = animationComponent.Speed,
					RaiseDelay = animationComponent.RaiseDelay,
					UseHitEvent = colliderComponent.UseHitEvent,
				});

				AddComponent(new DropTargetAnimationData {
					IsDropped = animationComponent.IsDropped,
					MoveDown = !animationComponent.IsDropped,
					ZOffset = animationComponent.IsDropped ? -DropTargetAnimationData.DropTargetLimit : 0f
				});
			}

			// register
			GetComponentInParent<Player>().RegisterDropTarget(authoring, GetEntity());
		}
	}
}
