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

#region ReSharper
// ReSharper disable InconsistentNaming
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using VisualPinball.Engine.Game.Engines;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Plunger;
using Mesh = VisualPinball.Engine.VPT.Mesh;

namespace VisualPinball.Unity
{
	[ExecuteAlways]
	[AddComponentMenu("Visual Pinball/Game Item/Plunger")]
	public class PlungerAuthoring : ItemMainRenderableAuthoring<Plunger, PlungerData>,
		ICoilDeviceAuthoring, IConvertGameObjectToEntity
	{
		#region Data

		public int Type = PlungerType.PlungerTypeModern;

		public float Width = 25f;

		public float Height = 20f;

		public float ZAdjust;

		public float Stroke = 80f;

		public float SpeedPull = 0.5f;

		public float SpeedFire = 80f;

		public float MechStrength = 85f;

		public float ParkPosition = 0.5f / 3.0f;

		public float ScatterVelocity;

		public float MomentumXfer = 1f;

		public bool IsMechPlunger;

		public bool AutoPlunger;

		public SurfaceAuthoring Surface;

		public string TipShape = "0 .34; 2 .6; 3 .64; 5 .7; 7 .84; 8 .88; 9 .9; 11 .92; 14 .92; 39 .84";

		public float RodDiam = 0.6f;

		public float RingGap = 2.0f;

		public float RingDiam = 0.94f;

		public float RingWidth = 3.0f;

		public float SpringDiam = 0.77f;

		public float SpringGauge = 1.38f;

		public float SpringLoops = 8.0f;

		public float SpringEndLoops = 2.5f;

		#endregion

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
			var go = gameObject;
			var table = go.GetComponentInParent<TableAuthoring>().Item;
			transform.GetComponentInParent<Player>().RegisterPlunger(Item, entity, ParentEntity, analogPlungerAction, go);

			var zHeight = table.GetSurfaceHeight(Data.Surface, Data.Center.X, Data.Center.Y);
			var x = Data.Center.X - Data.Width;
			var y = Data.Center.Y + Data.Height;
			var x2 = Data.Center.X + Data.Width;

			var frameTop = Data.Center.Y - Data.Stroke;
			var frameBottom = Data.Center.Y;
			var frameLen = frameBottom - frameTop;
			var restPos = Data.ParkPosition;
			var position = frameTop + restPos * frameLen;

			var info = new ColliderInfo {
				Entity = entity,
				FireEvents = true,
				IsEnabled = true,
				ItemType = ItemType.Plunger,
				ParentEntity = entity
			};

			dstManager.AddComponentData(entity, new PlungerStaticData {
				MomentumXfer = Data.MomentumXfer,
				ScatterVelocity = Data.ScatterVelocity,
				FrameStart = frameBottom,
				FrameEnd = frameTop,
				FrameLen = frameLen,
				RestPosition = restPos,
				IsAutoPlunger = Data.AutoPlunger,
				IsMechPlunger = Data.IsMechPlunger,
				SpeedFire = Data.SpeedFire,
				NumFrames = Item.MeshGenerator.NumFrames
			});

			dstManager.AddComponentData(entity, new PlungerColliderData {
				LineSegSide0 = new LineCollider(new float2(x + 0.0001f, position), new float2(x, y), zHeight, zHeight + Plunger.PlungerHeight, info),
				LineSegSide1 = new LineCollider(new float2(x2, y), new float2(x2 + 0.0001f, position), zHeight, zHeight + Plunger.PlungerHeight, info),
				LineSegEnd = new LineCollider(new float2(x2, position), new float2(x, position), zHeight, zHeight + Plunger.PlungerHeight, info),
				JointEnd0 = new LineZCollider(new float2(x, position), zHeight, zHeight + Plunger.PlungerHeight, info),
				JointEnd1 = new LineZCollider(new float2(x2, position), zHeight, zHeight + Plunger.PlungerHeight, info),
			});

			dstManager.AddComponentData(entity, new PlungerMovementData {
				FireBounce = 0f,
				Position = position,
				RetractMotion = false,
				ReverseImpulse = 0f,
				Speed = 0f,
				TravelLimit = frameTop,
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

			dstManager.AddComponentData(entity, new PlungerAnimationData {
				Position = _data.ParkPosition
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

			var convertedItem = new ConvertedItem<Plunger, PlungerData, PlungerAuthoring>(gameObject);
			switch (plungerTypeBefore) {
				case PlungerType.PlungerTypeFlat:
					// remove flat
					convertedItem.Destroy<PlungerFlatMeshAuthoring>();

					// create rod
					convertedItem.AddMeshAuthoring<PlungerRodMeshAuthoring>(PlungerMeshGenerator.Rod, false);

					if (plungerTypeAfter == PlungerType.PlungerTypeCustom) {
						// create spring
						convertedItem.AddMeshAuthoring<PlungerSpringMeshAuthoring>(PlungerMeshGenerator.Spring, false);
					}
					break;

				case PlungerType.PlungerTypeModern:
					if (plungerTypeAfter == PlungerType.PlungerTypeCustom) {
						// create spring
						convertedItem.AddMeshAuthoring<PlungerSpringMeshAuthoring>(PlungerMeshGenerator.Spring, false);
					}

					if (plungerTypeAfter == PlungerType.PlungerTypeFlat) {
						// remove rod
						convertedItem.Destroy<PlungerRodMeshAuthoring>();
						// create flat
						convertedItem.AddMeshAuthoring<PlungerFlatMeshAuthoring>(PlungerMeshGenerator.Flat, false);
					}
					break;

				case PlungerType.PlungerTypeCustom:
					// remove spring
					convertedItem.Destroy<PlungerSpringMeshAuthoring>();

					if (plungerTypeAfter == PlungerType.PlungerTypeFlat) {
						// remove rod
						convertedItem.Destroy<PlungerRodMeshAuthoring>();
						// create flat
						convertedItem.AddMeshAuthoring<PlungerFlatMeshAuthoring>(PlungerMeshGenerator.Flat, false);
					}
					break;
			}
		}

		public void UpdateParkPosition(float pos)
		{
			foreach (var skinnedMeshRenderer in GetComponentsInChildren<SkinnedMeshRenderer>()) {
				skinnedMeshRenderer.SetBlendShapeWeight(0, pos);
			}
		}

		public override ItemDataTransformType EditorPositionType => ItemDataTransformType.TwoD;
		public override Vector3 GetEditorPosition() => Data.Center.ToUnityVector3(0f);
		public override void SetEditorPosition(Vector3 pos) => Data.Center = pos.ToVertex2Dxy();
	}
}
