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

#region ReSharper
// ReSharper disable InconsistentNaming
#endregion

using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using VisualPinball.Engine.Game.Engines;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Plunger;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Game Item/Plunger")]
	public class PlungerComponent : MainRenderableComponent<PlungerData>, ICoilDeviceComponent
	{
		#region Data

		public Vector3 Position {
			get => transform.localPosition.TranslateToVpx();
			set => transform.localPosition = value.TranslateToWorld();
		}
		public float Width = 25f;
		public float Height = 20f;

		#endregion

		public InputActionReference analogPlungerAction;

		#region Overrides and Constants

		public override ItemType ItemType => ItemType.Plunger;
		public override string ItemName => "Plunger";

		public override PlungerData InstantiateData() => new PlungerData();

		public override bool HasProceduralMesh => true;

		protected override Type MeshComponentType { get; } = typeof(MeshComponent<PlungerData, PlungerComponent>);
		protected override Type ColliderComponentType { get; } = typeof(ColliderComponent<PlungerData, PlungerComponent>);

		public const string PullCoilId = "c_pull";
		public const string FireCoilId = "c_autofire";

		#endregion

		#region Runtime

		public PlungerApi PlungerApi { get; private set; }

		private void Awake()
		{
			var player = GetComponentInParent<Player>();
			var physicsEngine = GetComponentInParent<PhysicsEngine>();
			PlungerApi = new PlungerApi(gameObject, player, physicsEngine);

			player.Register(PlungerApi, this, analogPlungerAction);
			if (GetComponent<PlungerColliderComponent>()) {
				RegisterPhysics(physicsEngine);
			}
		}

		// public float4x4 TransformationWithinPlayfield
		// 	=> math.mul(Physics.VpxToWorld, transform.worldToLocalMatrix.WorldToLocalTranslateWithinPlayfield(_playfieldToWorld));
		public float4x4 TransformationWithinPlayfield
			=> transform.worldToLocalMatrix.WorldToLocalTranslateWithinPlayfield(_playfieldToWorld);

		#endregion

		#region Wiring

		public IEnumerable<GamelogicEngineCoil> AvailableCoils => new[] {
			new GamelogicEngineCoil(PullCoilId) { Description = "Pull back" },
			new GamelogicEngineCoil(FireCoilId) { Description = "Auto-fire" },
		};

		IEnumerable<GamelogicEngineCoil> IDeviceComponent<GamelogicEngineCoil>.AvailableDeviceItems => AvailableCoils;
		IEnumerable<IGamelogicEngineDeviceItem> IWireableComponent.AvailableWireDestinations => AvailableCoils;
		IEnumerable<IGamelogicEngineDeviceItem> IDeviceComponent<IGamelogicEngineDeviceItem>.AvailableDeviceItems => AvailableCoils;

		#endregion

		#region Transformation

		[NonSerialized]
		private float4x4 _playfieldToWorld;

		public override void UpdateTransforms()
		{
			base.UpdateTransforms();

			GetComponent<PlungerRodMeshComponent>()?.CalculateBoundingBox();
			GetComponent<PlungerSpringMeshComponent>()?.CalculateBoundingBox();
		}

		private void Start()
		{
			_playfieldToWorld = Player.PlayfieldToWorldMatrix;
		}

		#endregion

		#region Conversion

		public override IEnumerable<MonoBehaviour> SetData(PlungerData data)
		{
			var updatedComponents = new List<MonoBehaviour> { this };

			// geometry and position
			Position = new Vector3(data.Center.X, data.Center.Y, data.ZAdjust);
			Width = data.Width;
			Height = data.Height;

			// collider data
			var collComponent = GetComponent<PlungerColliderComponent>();
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
			var rodMesh = GetComponentInChildren<PlungerRodMeshComponent>(true);
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
			var springMesh = GetComponentInChildren<PlungerSpringMeshComponent>(true);
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

		public override IEnumerable<MonoBehaviour> SetReferencedData(PlungerData data, Table table, IMaterialProvider materialProvider, ITextureProvider textureProvider, Dictionary<string, IMainComponent> components)
		{
			// surface
			ParentToSurface(data.Surface, data.Center, components);

			// rod mesh
			var rodMesh = GetComponentInChildren<PlungerRodMeshComponent>(true);
			if (rodMesh) {
				rodMesh.CreateMesh(data, table, textureProvider, materialProvider);
				rodMesh.CalculateBoundingBox();
			}

			// spring mesh
			var springMesh = GetComponentInChildren<PlungerSpringMeshComponent>(true);
			if (springMesh && data.Type == PlungerType.PlungerTypeCustom) {
				springMesh.CreateMesh(data, table, textureProvider, materialProvider);
				springMesh.CalculateBoundingBox();
			}

			return Array.Empty<MonoBehaviour>();
		}

		public override PlungerData CopyDataTo(PlungerData data, string[] materialNames, string[] textureNames, bool forExport)
		{
			// name, geometry and position
			data.Name = name;
			data.Center = new Vertex2D(Position.x, Position.y);
			data.Width = Width;
			data.Height = Height;
			data.ZAdjust = Position.z;

			// collider data
			var collComponent = GetComponent<PlungerColliderComponent>();
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
			var rodMesh = GetComponentInChildren<PlungerRodMeshComponent>(true);
			if (rodMesh) {
				data.TipShape = rodMesh.TipShape;
				data.RodDiam = rodMesh.RodDiam;
				data.RingGap = rodMesh.RingGap;
				data.RingDiam = rodMesh.RingDiam;
				data.RingWidth = rodMesh.RingWidth;
			}

			// spring mesh
			var springMesh = GetComponentInChildren<PlungerSpringMeshComponent>(true);
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

		public override void CopyFromObject(GameObject go)
		{
			var plungerComponent = go.GetComponent<PlungerComponent>();
			if (plungerComponent != null) {
				Position = plungerComponent.Position;
				Width = plungerComponent.Width;
				Height = plungerComponent.Height;

			} else {
				Position = go.transform.localPosition.TranslateToVpx();
			}

			UpdateTransforms();
		}

		#endregion

		#region State

		internal PlungerState CreateState()
		{
			var collComponent = GetComponent<PlungerColliderComponent>();
			if (!collComponent) {
				// without collider, the plunger is only a dead mesh.
				return default;
			}

			var zHeight = Position.z;
			var x = -Width;
			var x2 = Width;
			var y = Height;

			var frameTop = -collComponent.Stroke;
			var frameBottom = 0;
			var frameLen = frameBottom - frameTop;
			var restPos = collComponent.ParkPosition;
			var position = frameTop + restPos * frameLen;

			var info = new ColliderInfo {
				ItemId = GetInstanceID(),
				FireEvents = true,
				ItemType = ItemType.Plunger,
			};

			return new PlungerState(
				new PlungerStaticState {
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
				},
				new PlungerColliderState {
					LineSegSide0 = new LineCollider(new float2(x + 0.0001f, position), new float2(x, y), zHeight, zHeight + Plunger.PlungerHeight, info),
					LineSegSide1 = new LineCollider(new float2(x2, y), new float2(x2 + 0.0001f, position), zHeight, zHeight + Plunger.PlungerHeight, info),
					LineSegEnd = new LineCollider(new float2(x2, position), new float2(x, position), zHeight, zHeight + Plunger.PlungerHeight, info),
					JointEnd0 = new LineZCollider(new float2(x, position), zHeight, zHeight + Plunger.PlungerHeight, info),
					JointEnd1 = new LineZCollider(new float2(x2, position), zHeight, zHeight + Plunger.PlungerHeight, info),
				},
				new PlungerMovementState {
					FireBounce = 0f,
					Position = position,
					RetractMotion = false,
					ReverseImpulse = 0f,
					Speed = 0f,
					TravelLimit = frameTop,
					FireSpeed = 0f,
					FireTimer = 0
				},
				new PlungerVelocityState {
					Mech0 = 0f,
					Mech1 = 0f,
					Mech2 = 0f,
					PullForce = 0f,
					InitialSpeed = 0f,
					AutoFireTimer = 0,
					AddRetractMotion = false,
					RetractWaitLoop = 0,
					MechStrength = collComponent.MechStrength
				},
				new PlungerAnimationState {
					Position = collComponent.ParkPosition
				}
			);
		}

		#endregion

		public void UpdateParkPosition(float pos)
		{
			foreach (var skinnedMeshRenderer in GetComponentsInChildren<SkinnedMeshRenderer>()) {
				skinnedMeshRenderer.SetBlendShapeWeight(0, pos);
			}
		}
	}
}
