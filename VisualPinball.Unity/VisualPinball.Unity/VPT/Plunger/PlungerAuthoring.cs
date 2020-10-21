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
using Unity.Entities;
using UnityEngine;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Plunger;

namespace VisualPinball.Unity
{
	[ExecuteAlways]
	[AddComponentMenu("Visual Pinball/Game Item/Plunger")]
	public class PlungerAuthoring : ItemMainAuthoring<Plunger, PlungerData>,
		ICoilAuthoring, IConvertGameObjectToEntity
	{
		protected override Plunger InstantiateItem(PlungerData data) => new Plunger(data);

		protected override Type MeshAuthoringType { get; } = typeof(ItemMeshAuthoring<Plunger, PlungerData, PlungerAuthoring>);
		protected override Type ColliderAuthoringType { get; } = typeof(ItemColliderAuthoring<Plunger, PlungerData, PlungerAuthoring>);

		private void OnDestroy()
		{
			if (!Application.isPlaying) {
				Table?.Remove<Plunger>(Name);
			}
		}

		public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
		{
			Convert(entity, dstManager);
			var table = gameObject.GetComponentInParent<TableAuthoring>().Item;
			transform.GetComponentInParent<Player>().RegisterPlunger(Item, entity, gameObject);

			Item.Init(table);
			var hit = Item.PlungerHit;
			hit.SetIndex(entity.Index, entity.Version);

			dstManager.AddComponentData(entity, new PlungerStaticData {
				MomentumXfer = Data.MomentumXfer,
				ScatterVelocity = Data.ScatterVelocity,
				FrameStart = hit.FrameBottom,
				FrameEnd = hit.FrameTop,
				FrameLen = hit.FrameLen,
				RestPosition = hit.RestPos,
				IsAutoPlunger = Data.AutoPlunger,
				SpeedFire = Data.SpeedFire,
				NumFrames = Item.MeshGenerator.NumFrames
			});

			dstManager.AddComponentData(entity, new PlungerColliderData {
				JointEnd0 = LineZCollider.Create(hit.JointEnd[0]),
				JointEnd1 = LineZCollider.Create(hit.JointEnd[1]),
				LineSegEnd = LineCollider.Create(hit.LineSegEnd),
				LineSegSide0 = LineCollider.Create(hit.LineSegSide[0]),
				LineSegSide1 = LineCollider.Create(hit.LineSegSide[1])
			});

			dstManager.AddComponentData(entity, new PlungerMovementData {
				FireBounce = 0f,
				Position = hit.Position,
				RetractMotion = false,
				ReverseImpulse = 0f,
				Speed = 0f,
				TravelLimit = hit.FrameTop,
				FireSpeed = 0f,
				FireTimer = 0
			});

			dstManager.AddComponentData(entity, new PlungerVelocityData {
				Mech0 = 0f,
				Mech1 = 0f,
				Mech2 = 0f,
				PullForce = 0f,
				InitialSpeed = 0f,
				AutoFireTimer = 0,
				AddRetractMotion = false,
				RetractWaitLoop = 0,
				MechStrength = Data.MechStrength
			});
		}

		public void OnTypeChanged(int plungerTypeBefore, int plungerTypeAfter)
		{
			if (plungerTypeBefore == plungerTypeAfter) {
				return;
			}

			switch (plungerTypeBefore) {
				case PlungerType.PlungerTypeFlat:
					// remove flat
					var flatPlungerAuthoring = GetComponentInChildren<PlungerFlatMeshAuthoring>();
					if (flatPlungerAuthoring != null) {
						DestroyImmediate(flatPlungerAuthoring.gameObject);
					}

					// create rod
					PlungerExtensions.CreateChild<PlungerRodMeshAuthoring>(gameObject, PlungerMeshGenerator.Rod);

					if (plungerTypeAfter == PlungerType.PlungerTypeCustom) {
						// create spring
						PlungerExtensions.CreateChild<PlungerSpringMeshAuthoring>(gameObject, PlungerMeshGenerator.Spring);
					}
					break;

				case PlungerType.PlungerTypeModern:
					if (plungerTypeAfter == PlungerType.PlungerTypeCustom) {
						// create spring
						PlungerExtensions.CreateChild<PlungerSpringMeshAuthoring>(gameObject, PlungerMeshGenerator.Spring);
					}

					if (plungerTypeAfter == PlungerType.PlungerTypeFlat) {
						// remove rod
						var rodPlungerAuthoring = GetComponentInChildren<PlungerRodMeshAuthoring>();
						if (rodPlungerAuthoring != null) {
							DestroyImmediate(rodPlungerAuthoring.gameObject);
						}
						// create flat
						PlungerExtensions.CreateChild<PlungerFlatMeshAuthoring>(gameObject, PlungerMeshGenerator.Flat);
					}
					break;

				case PlungerType.PlungerTypeCustom:
					// remove spring
					var springPlungerAuthoring = GetComponentInChildren<PlungerSpringMeshAuthoring>();
					if (springPlungerAuthoring != null) {
						DestroyImmediate(springPlungerAuthoring.gameObject);
					}

					if (plungerTypeAfter == PlungerType.PlungerTypeFlat) {
						// remove rod
						var rodPlungerAuthoring = GetComponentInChildren<PlungerRodMeshAuthoring>();
						if (rodPlungerAuthoring != null) {
							DestroyImmediate(rodPlungerAuthoring.gameObject);
						}

						// create flat
						PlungerExtensions.CreateChild<PlungerFlatMeshAuthoring>(gameObject, PlungerMeshGenerator.Flat);
					}

					break;

			}
		}

		public void RemoveHittableComponent()
		{
		}

		public override ItemDataTransformType EditorPositionType => ItemDataTransformType.TwoD;
		public override Vector3 GetEditorPosition() => Data.Center.ToUnityVector3(0f);
		public override void SetEditorPosition(Vector3 pos) => Data.Center = pos.ToVertex3D();
	}
}
