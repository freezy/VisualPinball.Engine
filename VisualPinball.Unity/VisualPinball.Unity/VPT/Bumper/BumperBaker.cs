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
using VisualPinball.Engine.VPT.Bumper;

namespace VisualPinball.Unity
{
	public class BumperBaker : ItemBaker<BumperComponent, BumperData>
	{
		public override void Bake(BumperComponent authoring)
		{
			base.Bake(authoring);
			
			// physics collision data
			var collComponent = GetComponentInChildren<BumperColliderComponent>();
			if (collComponent) {
				AddComponent(new BumperStaticData {
					Force = collComponent.Force,
					HitEvent = collComponent.HitEvent,
					Threshold = collComponent.Threshold
				});
			}

			// skirt animation data
			if (GetComponentInChildren<BumperSkirtAnimationComponent>()) {
				AddComponent(new BumperSkirtAnimationData {
					BallPosition = default,
					AnimationCounter = 0f,
					DoAnimate = false,
					DoUpdate = false,
					EnableAnimation = true,
					Rotation = new float2(0, 0),
					Center = authoring.Position
				});
			}

			// ring animation data
			var ringAnimComponent = GetComponentInChildren<BumperRingAnimationComponent>();
			if (ringAnimComponent) {
				AddComponent(new BumperRingAnimationData {

					// dynamic
					IsHit = false,
					Offset = 0,
					AnimateDown = false,
					DoAnimate = false,

					// static
					DropOffset = ringAnimComponent.RingDropOffset,
					HeightScale = authoring.HeightScale,
					Speed = ringAnimComponent.RingSpeed,
				});
			}

			// register at player
			GetComponentInParent<Player>().RegisterBumper(authoring, GetEntity());
		}
	}
}
