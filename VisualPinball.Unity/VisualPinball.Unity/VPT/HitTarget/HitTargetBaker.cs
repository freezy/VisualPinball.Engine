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
	public class HitTargetBaker : ItemBaker<HitTargetComponent, HitTargetData>
	{
		public override void Bake(HitTargetComponent authoring)
		{
			base.Bake(authoring);
			
			var hitTargetColliderComponent = GetComponent<HitTargetColliderComponent>();
			var hitTargetAnimationComponent = GetComponentInChildren<HitTargetAnimationComponent>();
			if (hitTargetColliderComponent && hitTargetAnimationComponent) {

				AddComponent(new HitTargetStaticData {
					Speed = hitTargetAnimationComponent.Speed,
					MaxAngle = hitTargetAnimationComponent.MaxAngle,
				});

				AddComponent(new HitTargetAnimationData {
					MoveDirection = true,
				});
			}

			// register
			GetComponentInParent<Player>().RegisterHitTarget(authoring);
		}
	}
}
