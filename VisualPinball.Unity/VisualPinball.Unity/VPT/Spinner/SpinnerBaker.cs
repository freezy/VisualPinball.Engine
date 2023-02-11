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
using VisualPinball.Engine.Common;
using VisualPinball.Engine.VPT.Spinner;

namespace VisualPinball.Unity
{
	public class SpinnerBaker : ItemBaker<SpinnerComponent, SpinnerData>
	{
		public override void Bake(SpinnerComponent authoring)
		{
			base.Bake(authoring);

			// physics collision data
			var collComponent = GetComponent<SpinnerColliderComponent>();
			if (collComponent) {

				AddComponent(new SpinnerStaticData {
					AngleMax = math.radians(authoring.AngleMax),
					AngleMin = math.radians(authoring.AngleMin),
					Damping = math.pow(authoring.Damping, (float)PhysicsConstants.PhysFactor),
					Elasticity = collComponent.Elasticity,
					Height = authoring.Height
				});
			}

			// animation
			if (GetComponentInChildren<SpinnerPlateAnimationComponent>()) {
				AddComponent(new SpinnerMovementData {
					Angle = math.radians(math.clamp(0.0f, authoring.AngleMin, authoring.AngleMax)),
					AngleSpeed = 0f
				});
			}

			// register
			GetComponentInParent<Player>().RegisterSpinner(authoring);

		}
	}
}
