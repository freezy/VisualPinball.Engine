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

using Unity.Mathematics;
using UnityEngine.UIElements;
using VisualPinball.Engine.Common;
using VisualPinball.Engine.VPT.Gate;

namespace VisualPinball.Unity
{
	public class GateBaker : ItemBaker<GateComponent, GateData>
	{
		public override void Bake(GateComponent authoring)
		{
			base.Bake(authoring);
			
			// collision
			var colliderComponent = GetComponent<GateColliderComponent>();
			if (colliderComponent) {

				AddComponent(new GateStaticData {
					AngleMin = math.radians(colliderComponent._angleMin),
					AngleMax = math.radians(colliderComponent._angleMax),
					Height = authoring.Position.z,
					Damping = math.pow(colliderComponent.Damping, (float)PhysicsConstants.PhysFactor),
					GravityFactor = colliderComponent.GravityFactor,
					TwoWay = colliderComponent.TwoWay,
				});

				// movement data
				if (GetComponentInChildren<GateWireAnimationComponent>()) {
					AddComponent(new GateMovementData {
						Angle = math.radians(colliderComponent._angleMin),
						AngleSpeed = 0,
						ForcedMove = false,
						IsOpen = false,
						HitDirection = false
					});
				}
			}

			// register
			GetComponentInParent<Player>().RegisterGate(authoring);

		}
	}
}
