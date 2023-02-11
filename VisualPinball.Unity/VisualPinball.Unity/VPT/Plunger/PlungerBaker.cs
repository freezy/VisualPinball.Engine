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
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Plunger;

namespace VisualPinball.Unity
{
	public class PlungerBaker : ItemBaker<PlungerComponent, PlungerData>
	{
		public override void Bake(PlungerComponent authoring)
		{
			base.Bake(authoring);
			
					var collComponent = GetComponent<PlungerColliderComponent>();
			if (!collComponent) {
				// without collider, the plunger is only a dead mesh.
				return;
			}

			var zHeight = authoring.PositionZ;
			var x = authoring.Position.x - authoring.Width;
			var y = authoring.Position.y + authoring.Height;
			var x2 = authoring.Position.x + authoring.Width;

			var frameTop = authoring.Position.y - collComponent.Stroke;
			var frameBottom = authoring.Position.y;
			var frameLen = frameBottom - frameTop;
			var restPos = collComponent.ParkPosition;
			var position = frameTop + restPos * frameLen;

			var info = new ColliderInfo {
				ItemId = authoring.GetInstanceID(),
				FireEvents = true,
				IsEnabled = true,
				ItemType = ItemType.Plunger,
			};

			AddComponent(new PlungerStaticData {
				MomentumXfer = collComponent.MomentumXfer,
				ScatterVelocity = collComponent.ScatterVelocity,
				FrameStart = frameBottom,
				FrameEnd = frameTop,
				FrameLen = frameLen,
				RestPosition = restPos,
				IsAutoPlunger = collComponent.IsAutoPlunger,
				IsMechPlunger = collComponent.IsMechPlunger,
				SpeedFire = collComponent.SpeedFire,
				NumFrames = (int)(collComponent.Stroke * (float)(PlungerMeshGenerator.PlungerFrameCount / 80.0f)) + 1, // 25 frames per 80 units travel
			});

			AddComponent(new PlungerColliderData {
				LineSegSide0 = new LineCollider(new float2(x + 0.0001f, position), new float2(x, y), zHeight, zHeight + Plunger.PlungerHeight, info),
				LineSegSide1 = new LineCollider(new float2(x2, y), new float2(x2 + 0.0001f, position), zHeight, zHeight + Plunger.PlungerHeight, info),
				LineSegEnd = new LineCollider(new float2(x2, position), new float2(x, position), zHeight, zHeight + Plunger.PlungerHeight, info),
				JointEnd0 = new LineZCollider(new float2(x, position), zHeight, zHeight + Plunger.PlungerHeight, info),
				JointEnd1 = new LineZCollider(new float2(x2, position), zHeight, zHeight + Plunger.PlungerHeight, info),
			});

			AddComponent(new PlungerMovementData {
				FireBounce = 0f,
				Position = position,
				RetractMotion = false,
				ReverseImpulse = 0f,
				Speed = 0f,
				TravelLimit = frameTop,
				FireSpeed = 0f,
				FireTimer = 0
			});

			AddComponent(new PlungerVelocityData {
				Mech0 = 0f,
				Mech1 = 0f,
				Mech2 = 0f,
				PullForce = 0f,
				InitialSpeed = 0f,
				AutoFireTimer = 0,
				AddRetractMotion = false,
				RetractWaitLoop = 0,
				MechStrength = collComponent.MechStrength
			});

			AddComponent(new PlungerAnimationData {
				Position = collComponent.ParkPosition
			});

			// register at player
			GetComponentInParent<Player>().RegisterPlunger(authoring, authoring.analogPlungerAction);

		}
	}
}
