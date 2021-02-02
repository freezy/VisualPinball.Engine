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
using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using UnityEngine;
using UnityEngine.InputSystem;
using VisualPinball.Engine.Game.Engines;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Plunger;

namespace VisualPinball.Unity
{
	[ExecuteAlways]
	[AddComponentMenu("Visual Pinball/Game Item/Plunger")]
	public class PlungerAuthoring : ItemMainRenderableAuthoring<Plunger, PlungerData>,
		ICoilDeviceAuthoring, IConvertGameObjectToEntity
	{
		protected override Plunger InstantiateItem(PlungerData data) => new Plunger(data);

		public IEnumerable<GamelogicEngineCoil> AvailableCoils => Item.AvailableCoils;

		public InputActionReference analogPlungerAction;

		protected override Type MeshAuthoringType { get; } = typeof(ItemMeshAuthoring<Plunger, PlungerData, PlungerAuthoring>);
		protected override Type ColliderAuthoringType { get; } = typeof(ItemColliderAuthoring<Plunger, PlungerData, PlungerAuthoring>);

		public override IEnumerable<Type> ValidParents => PlungerColliderAuthoring.ValidParentTypes
			.Concat(PlungerFlatMeshAuthoring.ValidParentTypes)
			.Concat(PlungerRodMeshAuthoring.ValidParentTypes)
			.Concat(PlungerSpringMeshAuthoring.ValidParentTypes)
			.Distinct();

		public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
		{
			Convert(entity, dstManager);
			var table = gameObject.GetComponentInParent<TableAuthoring>().Item;
			transform.GetComponentInParent<Player>().RegisterPlunger(Item, entity, analogPlungerAction);

			Item.Init(table);
			var hit = Item.PlungerHit;
			hit.SetIndex(entity.Index, entity.Version, 0, 0);

			dstManager.AddComponentData(entity, new PlungerStaticData {
				MomentumXfer = Data.MomentumXfer,
				ScatterVelocity = Data.ScatterVelocity,
				FrameStart = hit.FrameBottom,
				FrameEnd = hit.FrameTop,
				FrameLen = hit.FrameLen,
				RestPosition = hit.RestPos,
				IsAutoPlunger = Data.AutoPlunger,
				IsMechPlunger = Data.IsMechPlunger,
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

		public override void Restore()
		{
			// update the name
			Item.Name = name;

			// update visibility
			Data.IsVisible = false;
			foreach (var meshComponent in MeshComponents) {
				switch (meshComponent) {
					case PlungerFlatMeshAuthoring flatMeshAuthoring:
						Data.IsVisible = flatMeshAuthoring.gameObject.activeInHierarchy;
						break;
					case PlungerRodMeshAuthoring rodMeshAuthoring:
						Data.IsVisible = Data.IsVisible || rodMeshAuthoring.gameObject.activeInHierarchy;
						break;
					case PlungerSpringMeshAuthoring springMeshAuthoring:
						Data.IsVisible = Data.IsVisible || springMeshAuthoring.gameObject.activeInHierarchy;
						break;
				}
			}
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
					ConvertedItem.CreateChild<PlungerRodMeshAuthoring>(gameObject, PlungerMeshGenerator.Rod);

					if (plungerTypeAfter == PlungerType.PlungerTypeCustom) {
						// create spring
						ConvertedItem.CreateChild<PlungerSpringMeshAuthoring>(gameObject, PlungerMeshGenerator.Spring);
					}
					break;

				case PlungerType.PlungerTypeModern:
					if (plungerTypeAfter == PlungerType.PlungerTypeCustom) {
						// create spring
						ConvertedItem.CreateChild<PlungerSpringMeshAuthoring>(gameObject, PlungerMeshGenerator.Spring);
					}

					if (plungerTypeAfter == PlungerType.PlungerTypeFlat) {
						// remove rod
						var rodPlungerAuthoring = GetComponentInChildren<PlungerRodMeshAuthoring>();
						if (rodPlungerAuthoring != null) {
							DestroyImmediate(rodPlungerAuthoring.gameObject);
						}
						// create flat
						ConvertedItem.CreateChild<PlungerFlatMeshAuthoring>(gameObject, PlungerMeshGenerator.Flat);
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
						ConvertedItem.CreateChild<PlungerFlatMeshAuthoring>(gameObject, PlungerMeshGenerator.Flat);
					}
					break;
			}
		}


		private void OnDestroy()
		{
			if (!Application.isPlaying) {
				Table?.Remove<Plunger>(Name);
			}
		}

		public override ItemDataTransformType EditorPositionType => ItemDataTransformType.TwoD;
		public override Vector3 GetEditorPosition() => Data.Center.ToUnityVector3(0f);
		public override void SetEditorPosition(Vector3 pos) => Data.Center = pos.ToVertex3D();
	}
}
