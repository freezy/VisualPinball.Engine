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
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using VisualPinball.Engine.Game.Engines;
using VisualPinball.Engine.Math;
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

		private static readonly int LerpPosition = Shader.PropertyToID("_LerpPosition");
		private static readonly int UVChannelVertices = Shader.PropertyToID("_UVChannelVertices");
		private static readonly int UVChannelNormals = Shader.PropertyToID("_UVChannelNormals");

		private void Start()
		{
			UpdateParkPosition(1 - Data.ParkPosition);
		}

		public override IEnumerable<Type> ValidParents => PlungerColliderAuthoring.ValidParentTypes
			.Concat(PlungerFlatMeshAuthoring.ValidParentTypes)
			.Concat(PlungerRodMeshAuthoring.ValidParentTypes)
			.Concat(PlungerSpringMeshAuthoring.ValidParentTypes)
			.Distinct();

		public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
		{
			Convert(entity, dstManager);
			var table = gameObject.GetComponentInParent<TableAuthoring>().Item;
			transform.GetComponentInParent<Player>().RegisterPlunger(Item, entity, ParentEntity, analogPlungerAction);

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

			UpdateParkPosition(1 - Data.ParkPosition);
		}

		public void UpdateParkPosition(float pos)
		{
			SetMaterialProperty<PlungerFlatMeshAuthoring>(UVChannelVertices, Engine.VPT.Mesh.AnimationUVChannelVertices);
			SetMaterialProperty<PlungerFlatMeshAuthoring>(UVChannelNormals, Engine.VPT.Mesh.AnimationUVChannelNormals);
			switch (Data.Type) {
				case PlungerType.PlungerTypeFlat: {
					SetMaterialProperty<PlungerFlatMeshAuthoring>(LerpPosition, pos);
					break;
				}
				case PlungerType.PlungerTypeCustom: {
					SetMaterialProperty<PlungerRodMeshAuthoring>(LerpPosition, pos);
					SetMaterialProperty<PlungerSpringMeshAuthoring>(LerpPosition, pos);
					break;
				}
				case PlungerType.PlungerTypeModern: {
					SetMaterialProperty<PlungerRodMeshAuthoring>(LerpPosition, pos);
					break;
				}
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
		public override void SetEditorPosition(Vector3 pos) => Data.Center = pos.ToVertex2Dxy();
	}
}
