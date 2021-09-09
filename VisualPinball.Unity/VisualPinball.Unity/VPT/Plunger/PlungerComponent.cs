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
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Game Item/Plunger")]
	public class PlungerAuthoring : ItemMainRenderableAuthoring<PlungerData>,
		ICoilDeviceAuthoring, IOnSurfaceAuthoring, IConvertGameObjectToEntity
	{
		#region Data

		[Tooltip("The position of the plunger on the playfield.")]
		public Vector2 Position;

		public float Width = 25f;

		public float Height = 20f;

		public float ZAdjust;

		public ISurfaceAuthoring Surface { get => _surface as ISurfaceAuthoring; set => _surface = value as MonoBehaviour; }
		[SerializeField]
		[TypeRestriction(typeof(ISurfaceAuthoring), PickerLabel = "Walls & Ramps", UpdateTransforms = true)]
		[Tooltip("On which surface this plunger is attached to. Updates Z-translation.")]
		public MonoBehaviour _surface;

		#endregion

		public InputActionReference analogPlungerAction;

		#region Overrides and Constants

		public override ItemType ItemType => ItemType.Plunger;
		public override string ItemName => "Plunger";

		public override PlungerData InstantiateData() => new PlungerData();

		public override IEnumerable<Type> ValidParents => PlungerColliderAuthoring.ValidParentTypes
			.Concat(PlungerFlatMeshAuthoring.ValidParentTypes)
			.Concat(PlungerRodMeshAuthoring.ValidParentTypes)
			.Concat(PlungerSpringMeshAuthoring.ValidParentTypes)
			.Distinct();

		protected override Type MeshAuthoringType { get; } = typeof(ItemMeshAuthoring<PlungerData, PlungerAuthoring>);
		protected override Type ColliderAuthoringType { get; } = typeof(ItemColliderAuthoring<PlungerData, PlungerAuthoring>);

		public const string PullCoilId = "c_pull";
		public const string FireCoilId = "c_autofire";

		#endregion

		#region Wiring

		public IEnumerable<GamelogicEngineCoil> AvailableCoils => new[] {
			new GamelogicEngineCoil(PullCoilId) { Description = "Pull back" },
			new GamelogicEngineCoil(FireCoilId) { Description = "Auto-fire" },
		};

		IEnumerable<GamelogicEngineCoil> IDeviceAuthoring<GamelogicEngineCoil>.AvailableDeviceItems => AvailableCoils;
		IEnumerable<IGamelogicEngineDeviceItem> IWireableAuthoring.AvailableWireDestinations => AvailableCoils;
		IEnumerable<IGamelogicEngineDeviceItem> IDeviceAuthoring<IGamelogicEngineDeviceItem>.AvailableDeviceItems => AvailableCoils;

		#endregion

		#region Transformation

		public void OnSurfaceUpdated() => RebuildMeshes();

		public override void OnPlayfieldHeightUpdated() => RebuildMeshes();

		public float PositionZ => SurfaceHeight(Surface, Position);

		#endregion

		#region Conversion

		public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
		{
			Convert(entity, dstManager);
			var go = gameObject;

			var collComponent = GetComponent<PlungerColliderAuthoring>();
			if (!collComponent) {
				// without collider, the plunger is only a dead mesh.
				return;
			}

			var zHeight = PositionZ;
			var x = Position.x - Width;
			var y = Position.y + Height;
			var x2 = Position.x + Width;

			var frameTop = Position.y - collComponent.Stroke;
			var frameBottom = Position.y;
			var frameLen = frameBottom - frameTop;
			var restPos = collComponent.ParkPosition;
			var position = frameTop + restPos * frameLen;

			var info = new ColliderInfo {
				Entity = entity,
				FireEvents = true,
				IsEnabled = true,
				ItemType = ItemType.Plunger,
				ParentEntity = entity
			};

			dstManager.AddComponentData(entity, new PlungerStaticData {
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
				MechStrength = collComponent.MechStrength
			});

			dstManager.AddComponentData(entity, new PlungerAnimationData {
				Position = collComponent.ParkPosition
			});

			// register at player
			GetComponentInParent<Player>().RegisterPlunger(this, entity, ParentEntity, analogPlungerAction);
		}

		public override IEnumerable<MonoBehaviour> SetData(PlungerData data)
		{
			var updatedComponents = new List<MonoBehaviour> { this };

			// geometry and position
			Position = data.Center.ToUnityVector2();
			Width = data.Width;
			Height = data.Height;
			ZAdjust = data.ZAdjust;

			// collider data
			var collComponent = GetComponent<PlungerColliderAuthoring>();
			if (collComponent) {
				collComponent.Stroke = data.Stroke;
				collComponent.SpeedPull = data.SpeedPull;
				collComponent.SpeedFire = data.SpeedFire;
				collComponent.MechStrength = data.MechStrength;
				collComponent.ParkPosition = data.ParkPosition;
				collComponent.ScatterVelocity = data.ScatterVelocity;
				collComponent.MomentumXfer = data.MomentumXfer;
				collComponent.IsMechPlunger = data.IsMechPlunger;
				collComponent.IsAutoPlunger = data.AutoPlunger;

				updatedComponents.Add(collComponent);
			}

			// rod mesh
			var rodMesh = GetComponentInChildren<PlungerRodMeshAuthoring>(true);
			if (rodMesh) {
				rodMesh.TipShape = data.TipShape;
				rodMesh.RodDiam = data.RodDiam;
				rodMesh.RingGap = data.RingGap;
				rodMesh.RingDiam = data.RingDiam;
				rodMesh.RingWidth = data.RingWidth;
				rodMesh.gameObject.SetActive(data.IsVisible);

				updatedComponents.Add(collComponent);
			}

			// spring mesh
			var springMesh = GetComponentInChildren<PlungerSpringMeshAuthoring>(true);
			if (springMesh) {
				springMesh.SpringDiam = data.SpringDiam;
				springMesh.SpringGauge = data.SpringGauge;
				springMesh.SpringLoops = data.SpringLoops;
				springMesh.SpringEndLoops = data.SpringEndLoops;
				springMesh.gameObject.SetActive(data.IsVisible);

				if (data.Type != PlungerType.PlungerTypeCustom) {
					springMesh.gameObject.SetActive(false);
				}

				updatedComponents.Add(collComponent);
			}

			return updatedComponents;
		}

		public override IEnumerable<MonoBehaviour> SetReferencedData(PlungerData data, Table table, IMaterialProvider materialProvider, ITextureProvider textureProvider, Dictionary<string, IItemMainAuthoring> components)
		{
			Surface = GetAuthoring<SurfaceAuthoring>(components, data.Surface);

			// rod mesh
			var rodMesh = GetComponentInChildren<PlungerRodMeshAuthoring>(true);
			if (rodMesh) {
				rodMesh.CreateMesh(data, table, textureProvider, materialProvider);
			}

			// spring mesh
			var springMesh = GetComponentInChildren<PlungerSpringMeshAuthoring>(true);
			if (springMesh && data.Type == PlungerType.PlungerTypeCustom) {
				springMesh.CreateMesh(data, table, textureProvider, materialProvider);
			}

			return Array.Empty<MonoBehaviour>();
		}

		public override PlungerData CopyDataTo(PlungerData data, string[] materialNames, string[] textureNames, bool forExport)
		{
			// name, geometry and position
			data.Name = name;
			data.Center = Position.ToVertex2D();
			data.Width = Width;
			data.Height = Height;
			data.ZAdjust = ZAdjust;
			data.Surface = Surface != null ? Surface.name : string.Empty;

			// collider data
			var collComponent = GetComponent<PlungerColliderAuthoring>();
			if (collComponent) {
				data.Stroke = collComponent.Stroke;
				data.SpeedPull = collComponent.SpeedPull;
				data.SpeedFire = collComponent.SpeedFire;
				data.MechStrength = collComponent.MechStrength;
				data.ParkPosition = collComponent.ParkPosition;
				data.ScatterVelocity = collComponent.ScatterVelocity;
				data.MomentumXfer = collComponent.MomentumXfer;
				data.IsMechPlunger = collComponent.IsMechPlunger;
				data.AutoPlunger = collComponent.IsAutoPlunger;
			}

			// rod mesh
			var rodMesh = GetComponentInChildren<PlungerRodMeshAuthoring>(true);
			if (rodMesh) {
				data.TipShape = rodMesh.TipShape;
				data.RodDiam = rodMesh.RodDiam;
				data.RingGap = rodMesh.RingGap;
				data.RingDiam = rodMesh.RingDiam;
				data.RingWidth = rodMesh.RingWidth;
			}

			// spring mesh
			var springMesh = GetComponentInChildren<PlungerSpringMeshAuthoring>(true);
			if (springMesh) {
				data.SpringDiam = springMesh.SpringDiam;
				data.SpringGauge = springMesh.SpringGauge;
				data.SpringLoops = springMesh.SpringLoops;
				data.SpringEndLoops = springMesh.SpringEndLoops;
			}

			// type
			var hasSpringMesh = springMesh && springMesh.isActiveAndEnabled;
			var hasRodMesh = rodMesh && rodMesh.isActiveAndEnabled;
			data.IsVisible = hasRodMesh;
			data.Type = hasSpringMesh && hasRodMesh ? PlungerType.PlungerTypeCustom : PlungerType.PlungerTypeModern;

			return data;
		}

		#endregion

		public void UpdateParkPosition(float pos)
		{
			foreach (var skinnedMeshRenderer in GetComponentsInChildren<SkinnedMeshRenderer>()) {
				skinnedMeshRenderer.SetBlendShapeWeight(0, pos);
			}
		}

		#region Editor Tooling

		public override ItemDataTransformType EditorPositionType => ItemDataTransformType.TwoD;
		public override Vector3 GetEditorPosition() => Position;
		public override void SetEditorPosition(Vector3 pos)
		{
			Position = ((float3)pos).xy;
			RebuildMeshes();
		}

		#endregion
	}
}
