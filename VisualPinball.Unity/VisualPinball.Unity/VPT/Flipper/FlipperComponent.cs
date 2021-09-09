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
// ReSharper disable CompareOfFloatsByEqualityOperator
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable InconsistentNaming
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using VisualPinball.Engine.Game.Engines;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Flipper;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Engine.VPT.Trigger;
using Color = UnityEngine.Color;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Game Item/Flipper")]
	[HelpURL("https://docs.visualpinball.org/creators-guide/manual/mechanisms/flippers.html")]
	public class FlipperComponent : ItemMainRenderableComponent<FlipperData>,
		ISwitchDeviceComponent, ICoilDeviceComponent, IOnSurfaceComponent, IConvertGameObjectToEntity
	{
		#region Data

		[Tooltip("Position of the flipper on the playfield.")]
		public Vector2 Position;

		[Range(-180f, 180f)]
		[Tooltip("Angle of the flipper in start position (not flipped)")]
		public float StartAngle = 121.0f;

		[Range(-180f, 180f)]
		[Tooltip("Angle of the flipper in end position (flipped)")]
		public float EndAngle = 70.0f;

		public ISurfaceComponent Surface { get => _surface as ISurfaceComponent; set => _surface = value as MonoBehaviour; }
		[SerializeField]
		[TypeRestriction(typeof(ISurfaceComponent), PickerLabel = "Walls & Ramps", UpdateTransforms = true)]
		[Tooltip("On which surface this flipper is attached to. Updates Z-translation.")]
		public MonoBehaviour _surface;

		public bool IsEnabled = true;

		public bool IsDualWound;

		[Range(0, 100f)]
		[Tooltip("Height of the flipper plastic.")]
		public float Height = 50.0f;

		[Range(0, 50f)]
		[Tooltip("Radius of the flipper's larger end.")]
		public float BaseRadius = 21.5f;

		[Range(0, 50f)]
		[Tooltip("Radius of the flipper's smaller end.")]
		public float EndRadius = 13.0f;

		[Min(0)]
		[Tooltip("It's not clear what TF this does. Relict from VPX.")]
		public float FlipperRadiusMin;

		[Range(10f, 250f)]
		[Tooltip("The length of the flipper")]
		public float FlipperRadiusMax = 130.0f;

		[Range(0f, 50f)]
		[Tooltip("Thickness of the rubber")]
		public float RubberThickness = 7.0f;

		[Range(0f, 50f)]
		[Tooltip("Vertical position of the rubber")]
		public float RubberHeight = 19.0f;

		[Range(0, 100f)]
		[Tooltip("Vertical size of the rubber")]
		public float RubberWidth = 24.0f;

		#endregion

		#region Overrides and Constants

		public override ItemType ItemType => ItemType.Flipper;
		public override string ItemName => "Flipper";

		public override IEnumerable<Type> ValidParents => FlipperColliderComponent.ValidParentTypes
			.Concat(FlipperBaseMeshComponent.ValidParentTypes)
			.Concat(FlipperRubberMeshComponent.ValidParentTypes)
			.Distinct();

		public override FlipperData InstantiateData() => new FlipperData();

		protected override Type MeshAuthoringType { get; } = typeof(ItemMeshComponent<FlipperData, FlipperComponent>);
		protected override Type ColliderAuthoringType { get; } = typeof(ItemColliderComponent<FlipperData, FlipperComponent>);

		public const string MainCoilItem = "main_coil";
		public const string HoldCoilItem = "hold_coil";
		public const string EosSwitchItem = "eos_switch";

		#endregion

		#region Wiring

		public IEnumerable<GamelogicEngineSwitch> AvailableSwitches => new[] {
			new GamelogicEngineSwitch(EosSwitchItem) {
				Description = "EOS Switch",
				IsPulseSwitch = false,
			}
		};

		public SwitchDefault SwitchDefault => SwitchDefault.Configurable;

		public IEnumerable<GamelogicEngineCoil> AvailableCoils => IsDualWound
			? new[] {
				new GamelogicEngineCoil(MainCoilItem) { Description = "Main Coil" },
				new GamelogicEngineCoil(HoldCoilItem) { Description = "Hold Coil" },
			}
			: new[] { new GamelogicEngineCoil(MainCoilItem) };

		IEnumerable<GamelogicEngineCoil> IDeviceComponent<GamelogicEngineCoil>.AvailableDeviceItems => AvailableCoils;
		IEnumerable<GamelogicEngineSwitch> IDeviceComponent<GamelogicEngineSwitch>.AvailableDeviceItems => AvailableSwitches;
		IEnumerable<IGamelogicEngineDeviceItem> IWireableComponent.AvailableWireDestinations => AvailableCoils;
		IEnumerable<IGamelogicEngineDeviceItem> IDeviceComponent<IGamelogicEngineDeviceItem>.AvailableDeviceItems => AvailableCoils;

		#endregion

		#region Transformation

		public void OnSurfaceUpdated() => UpdateTransforms();

		public float PositionZ => SurfaceHeight(Surface, Position);

		public override void UpdateTransforms()
		{
			var t = transform;

			// position
			t.localPosition = new Vector3(Position.x, Position.y, PositionZ);

			// rotation
			t.localEulerAngles = new Vector3(0, 0, StartAngle);
		}

		#endregion

		#region Conversion

			public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
		{
			Convert(entity, dstManager);

			var player = transform.GetComponentInParent<Player>();
			var colliderAuthoring = gameObject.GetComponent<FlipperColliderComponent>();

			// collision
			if (colliderAuthoring) {

				// vpx physics
				var d = GetMaterialData(colliderAuthoring);
				dstManager.AddComponentData(entity, d);
				dstManager.AddComponentData(entity, GetMovementData(d));
				dstManager.AddComponentData(entity, GetVelocityData(d));
				dstManager.AddComponentData(entity, GetHitData());
				dstManager.AddComponentData(entity, new SolenoidStateData { Value = false });

				// flipper correction (nFozzy)
				if (colliderAuthoring.FlipperCorrection) {
					SetupFlipperCorrection(entity, dstManager, player, colliderAuthoring);
				}
			}

			// register
			player.RegisterFlipper(this, entity, ParentEntity);
		}

		public override IEnumerable<MonoBehaviour> SetData(FlipperData data)
		{
			var updatedComponents = new List<MonoBehaviour> { this };

			// transforms
			Position = data.Center.ToUnityVector2();
			StartAngle = data.StartAngle > 180f ? data.StartAngle - 360f : data.StartAngle;

			// geometry
			Height = data.Height;
			BaseRadius = data.BaseRadius;
			EndRadius = data.EndRadius;
			EndAngle = data.EndAngle > 180f ? data.EndAngle - 360f : data.EndAngle;
			FlipperRadiusMin = data.FlipperRadiusMin;
			FlipperRadiusMax = data.FlipperRadiusMax;
			RubberThickness = data.RubberThickness;
			RubberHeight = data.RubberHeight;
			RubberWidth = data.RubberWidth;

			// states
			IsEnabled = data.IsEnabled;
			IsDualWound = data.IsDualWound;

			// collider data
			var colliderAuthoring = gameObject.GetComponent<FlipperColliderComponent>();
			if (colliderAuthoring) {
				colliderAuthoring.Mass = data.Mass;
				colliderAuthoring.Strength = data.Strength;
				colliderAuthoring.Elasticity = data.Elasticity;
				colliderAuthoring.ElasticityFalloff = data.ElasticityFalloff;
				colliderAuthoring.Friction = data.Friction;
				colliderAuthoring.Return = data.Return;
				colliderAuthoring.RampUp = data.RampUp;
				colliderAuthoring.TorqueDamping = data.TorqueDamping;
				colliderAuthoring.TorqueDampingAngle = data.TorqueDampingAngle;
				colliderAuthoring.Scatter = data.Scatter;
				updatedComponents.Add(colliderAuthoring);
			}

			return updatedComponents;
		}

		public override IEnumerable<MonoBehaviour> SetReferencedData(FlipperData data, Table table, IMaterialProvider materialProvider, ITextureProvider textureProvider, Dictionary<string, IItemMainComponent> components)
		{
			Surface = GetAuthoring<SurfaceComponent>(components, data.Surface);
			UpdateTransforms();

			// children mesh creation and visibility
			var baseMesh = GetComponentInChildren<FlipperBaseMeshComponent>();
			if (baseMesh) {
				baseMesh.CreateMesh(data, table, textureProvider, materialProvider);
				baseMesh.gameObject.SetActive(data.IsVisible);
			}

			var rubberMesh = GetComponentInChildren<FlipperRubberMeshComponent>();
			if (rubberMesh) {
				rubberMesh.CreateMesh(data, table, textureProvider, materialProvider);
				rubberMesh.gameObject.SetActive(data.IsVisible);
			}

			return Array.Empty<MonoBehaviour>();
		}

		public override FlipperData CopyDataTo(FlipperData data, string[] materialNames, string[] textureNames, bool forExport)
		{
			// name and transforms
			data.Name = name;
			data.Center = Position.ToVertex2D();
			data.StartAngle = StartAngle;
			data.Surface = Surface != null ? Surface.name : string.Empty;

			// geometry
			data.Height = Height;
			data.BaseRadius = BaseRadius;
			data.EndRadius = EndRadius;
			data.EndAngle = EndAngle;
			data.FlipperRadiusMin = FlipperRadiusMin;
			data.FlipperRadiusMax = FlipperRadiusMax;
			data.RubberThickness = RubberThickness;
			data.RubberHeight = RubberHeight;
			data.RubberWidth = RubberWidth;

			// states
			data.IsEnabled = IsEnabled;
			data.IsDualWound = IsDualWound;

			// children visibility
			var baseMesh = GetComponentInChildren<FlipperBaseMeshComponent>();
			data.IsVisible = baseMesh && baseMesh.gameObject.activeInHierarchy;

			// collider data
			var colliderAuthoring = gameObject.GetComponent<FlipperColliderComponent>();
			if (colliderAuthoring) {
				data.Mass = colliderAuthoring.Mass;
				data.Strength = colliderAuthoring.Strength;
				data.Elasticity = colliderAuthoring.Elasticity;
				data.ElasticityFalloff = colliderAuthoring.ElasticityFalloff;
				data.Friction = colliderAuthoring.Friction;
				data.Return = colliderAuthoring.Return;
				data.RampUp = colliderAuthoring.RampUp;
				data.TorqueDamping = colliderAuthoring.TorqueDamping;
				data.TorqueDampingAngle = colliderAuthoring.TorqueDampingAngle;
				data.Scatter = colliderAuthoring.Scatter;
			}

			return data;
		}

		#endregion

		#region Editor Tooling

		public override ItemDataTransformType EditorPositionType => ItemDataTransformType.TwoD;
		public override Vector3 GetEditorPosition() => Surface != null
			? new Vector3(Position.x, Position.y, Surface.Height(Position))
			: new Vector3(Position.x, Position.y, 0);
		public override void SetEditorPosition(Vector3 pos) => Position = ((float3)pos).xy;

		public override ItemDataTransformType EditorRotationType => ItemDataTransformType.OneD;
		public override Vector3 GetEditorRotation() => new Vector3(StartAngle, 0f, 0f);
		public override void SetEditorRotation(Vector3 rot) => StartAngle = rot.x;

		public override ItemDataTransformType EditorScaleType => ItemDataTransformType.None;

		#if UNITY_EDITOR

		protected void OnDrawGizmosSelected()
		{
			const int height = 0;
			var poly = GetEnclosingPolygon(height: height);
			if (poly == null) {
				return;
			}

			// Draw enclosing polygon
			Gizmos.color = Color.cyan;
			if (IsLeft) {
				Gizmos.color = new Color(Gizmos.color.g, Gizmos.color.b, Gizmos.color.r, Gizmos.color.a);
			}
			for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++) {
				Gizmos.DrawLine(transform.TransformPoint(poly[j]), transform.TransformPoint(poly[i]));
			}

			// Draw arc arrow
			List<Vector3> arrow = new List<Vector3>();
			float start = -90F;
			float end = -90F + EndAngle - StartAngle;
			if (IsLeft) {
				(start, end) = (end, start);
			}
			AddPolyArc(arrow, Vector3.zero, FlipperRadiusMax - 20F, start, end, height: height);
			for (int i = 1, j = 0; i < arrow.Count; j = i++) {
				Gizmos.DrawLine(transform.TransformPoint(arrow[j]), transform.TransformPoint(arrow[i]));
			}
			var last = IsLeft ? arrow[0] : arrow[arrow.Count-1];
			var tmpA = IsLeft ? start + 90F + 3F : end +90F - 3F;
			var a = Quaternion.Euler(0, 0, tmpA) * new Vector3(0, -FlipperRadiusMax + 15F, height);
			var b = Quaternion.Euler(0, 0, tmpA) * new Vector3(0F, -FlipperRadiusMax + 25F, height);
			Gizmos.DrawLine(transform.TransformPoint(last) , transform.TransformPoint(a));
			Gizmos.DrawLine(transform.TransformPoint(last), transform.TransformPoint(b));
			Gizmos.color = Color.white;
		}

		#endif

		#endregion

		#region Flipper Correction

		private bool IsLeft => EndAngle < StartAngle;

		private void SetupFlipperCorrection(Entity entity, EntityManager dstManager, Player player, FlipperColliderComponent colliderComponent)
		{
			var fc = colliderComponent.FlipperCorrection;

				// create trigger
				var triggerData = CreateCorrectionTriggerData();
				var triggerEntity = dstManager.CreateEntity(typeof(TriggerStaticData));
				dstManager.AddComponentData(triggerEntity, new TriggerStaticData());
				player.RegisterTrigger(triggerData, triggerEntity, gameObject);

				using (var builder = new BlobBuilder(Allocator.Temp)) {

					ref var root = ref builder.ConstructRoot<FlipperCorrectionBlob>();
					root.FlipperEntity = entity;
					root.TimeDelayMs = fc.TimeThresholdMs;

					// Discretize the curves
					var polarities = builder.Allocate(ref root.Polarities, fc.PolaritiesCurveSlicingCount + 1);
					if (fc.Polarities != null)
					{
						var curve = fc.Polarities;
						float stepP = (curve[curve.length - 1].time - curve[0].time) / fc.PolaritiesCurveSlicingCount;
						int i = 0;
						for (var t = curve[0].time; t <= curve[curve.length - 1].time; t += stepP)
						{
							polarities[i].x = t;
							polarities[i++].y = curve.Evaluate(t);
						}
					}
					else
					{
						for (int i = 0; i < fc.PolaritiesCurveSlicingCount + 1; i++)
						{
							polarities[i].x = i / (float)fc.PolaritiesCurveSlicingCount;
							polarities[i].y = 0F;
						}
					}

					var velocities = builder.Allocate(ref root.Velocities, fc.VelocitiesCurveSlicingCount + 1);
					if (fc.Velocities != null)
					{
						var curve = fc.Velocities;
						float stepP = (curve[curve.length - 1].time - curve[0].time) / fc.VelocitiesCurveSlicingCount;
						int i = 0;
						for (var t = curve[0].time; t <= curve[curve.length - 1].time; t += stepP)
						{
							velocities[i].x = t;
							velocities[i++].y = curve.Evaluate(t);
						}
					}
					else
					{
						for (int i = 0; i < fc.VelocitiesCurveSlicingCount + 1; i++)
						{
							velocities[i].x = i / (float)fc.PolaritiesCurveSlicingCount;
							velocities[i].y = 1F;
						}
					}

					var blobAssetRef = builder.CreateBlobAssetReference<FlipperCorrectionBlob>(Allocator.Persistent);

					// add correction data
					dstManager.AddComponentData(triggerEntity, new FlipperCorrectionData {
						Value = blobAssetRef
					});
				}
		}

		//! Add a circle arc on a given polygon (used for enclosing poygon)
		public static void AddPolyArc(List<Vector3> poly, Vector3 center, float radius, float angleFrom, float angleTo, float stepSize = 1F, float height = 0f)
		{
			angleFrom %= 360;
			angleTo %= 360;

			angleFrom = angleFrom < 0 ? angleFrom + 360 : angleFrom;
			angleTo = angleTo < 0 ? angleTo + 360 : angleTo;
			angleFrom *= Mathf.PI / 180F;
			angleTo *= Mathf.PI / 180F;
			float angleDiffRad = angleTo - angleFrom;
			if (angleDiffRad < 0)
				angleDiffRad += Mathf.PI * 2;

			float arcLength = Mathf.Abs(angleDiffRad) * radius;
			int num = Mathf.CeilToInt(arcLength / Mathf.Abs(stepSize));
			if (num <= 0) {
				return;
			}
			float stepA = angleDiffRad / num;
			if (stepSize < 0) {
				stepA = -stepA;
			}

			float a = angleFrom;
			for (int i = 0; i <= num; i++) {
				poly.Add(new Vector3(center.x + Mathf.Cos(a) * radius, center.y + Mathf.Sin(a) * radius, height));
				a += stepA;
			}
		}

		public List<Vector3> GetEnclosingPolygon(float margin = 0.0F, float stepSize = 5F, float height = 0f)
		{
			var swing = EndAngle - StartAngle;
			swing = Mathf.Abs(swing);

			List<Vector3> ret = new List<Vector3>(); // TODO: caching

			float baseRadius = BaseRadius + margin;
			float tipRadius = EndRadius + margin;
			Vector3 baseLocalPos = Vector3.zero;
			float length = FlipperRadiusMax;
			Vector3 tipLocalPos = Vector3.up * -length;

			if (swing < 180F) {
				AddPolyArc(ret, baseLocalPos, baseRadius, swing, 180F, stepSize, height);
			} else {
				if (IsLeft) {
					ret.Add(Quaternion.Euler(0, 0, swing) * new Vector3(baseRadius, 0F, 0F));
				} else {
					ret.Add(new Vector3(-baseRadius, 0F, 0F));
				}
			}
			AddPolyArc(ret, tipLocalPos, tipRadius, 180F, 270F, stepSize, height);
			AddPolyArc(ret, baseLocalPos, length + tipRadius, 270F, 270F + swing, stepSize, height);
			Vector3 swingTipLocalPos = baseLocalPos + Quaternion.Euler(0, 0, swing) * new Vector3(0, -length, 0);
			AddPolyArc(ret, swingTipLocalPos, tipRadius, 270F + swing, swing, stepSize, height);

			if (IsLeft) { // left
				var rot = Quaternion.Euler(0, 0, -swing);
				for (int i = 0; i < ret.Count; i++) {
					ret[i] = rot * ret[i];
				}
			}

			return ret;
		}

		public TriggerData CreateCorrectionTriggerData()
		{
			// Get table reference
			var ta = GetComponentInParent<TableComponent>();
			if (ta != null) {

				var localPos = transform.localPosition;
				var data = new TriggerData(name + " (Correction Trigger)", localPos.x, localPos.y);
				var poly = GetEnclosingPolygon(23, 12);
				data.DragPoints = new DragPointData[poly.Count];
				data.IsLocked = true;
				data.HitHeight = 150F; // nFozzy's recommendation, but I think 50 should be ok

				for (var i = 0; i < poly.Count; i++) {
					// Poly points are expressed in flipper's frame: transpose to Table's frame as this is the basis uses for drag points
					var p = ta.transform.InverseTransformPoint(transform.TransformPoint(poly[i]));
					data.DragPoints[poly.Count - i - 1] = new DragPointData(p.x, p.y);
				}
				return data;
			}
			throw new InvalidOperationException("Cannot create correction trigger for flipper outside of the table hierarchy.");
		}

		#endregion

		#region DOTS Data

		private FlipperStaticData GetMaterialData(FlipperColliderComponent colliderComponent)
		{
			float flipperRadius;
			if (FlipperRadiusMin > 0 && FlipperRadiusMax > FlipperRadiusMin) {
				flipperRadius = FlipperRadiusMax - (FlipperRadiusMax - FlipperRadiusMin) /* m_ptable->m_globalDifficulty*/;
				flipperRadius = math.max(flipperRadius, BaseRadius - EndRadius + 0.05f);

			} else {
				flipperRadius = FlipperRadiusMax;
			}

			var endRadius = math.max(EndRadius, 0.01f); // radius of flipper end
			flipperRadius = math.max(flipperRadius, 0.01f); // radius of flipper arc, center-to-center radius
			var angleStart = math.radians(StartAngle);
			var angleEnd = math.radians(EndAngle);

			if (angleEnd == angleStart) {
				// otherwise hangs forever in collisions/updates
				angleEnd += 0.0001f;
			}

			// model inertia of flipper as that of rod of length flipper around its end
			var inertia = (float) (1.0 / 3.0) * colliderComponent.Mass * (flipperRadius * flipperRadius);
			var localPos = transform.localPosition;

			return new FlipperStaticData {
				Position = new float3(localPos.x, localPos.y, 0F), // TODO: surface height?
				Inertia = inertia,
				AngleStart = angleStart,
				AngleEnd = angleEnd,
				Strength = colliderComponent.Strength,
				ReturnRatio = colliderComponent.Return,
				TorqueDamping = colliderComponent.TorqueDamping,
				TorqueDampingAngle = colliderComponent.TorqueDampingAngle,
				RampUpSpeed = colliderComponent.RampUp,

				EndRadius = endRadius,
				FlipperRadius = flipperRadius
			};
		}

		private FlipperMovementData GetMovementData(FlipperStaticData d)
		{
			// store flipper base rotation without starting angle
			var baseRotation = math.normalize(math.mul(
				math.normalize(transform.rotation),
				quaternion.EulerXYZ(0, 0, -d.AngleStart)
			));
			return new FlipperMovementData {
				Angle = d.AngleStart,
				AngleSpeed = 0f,
				AngularMomentum = 0f,
				EnableRotateEvent = 0,
			};
		}

		private static FlipperVelocityData GetVelocityData(FlipperStaticData d)
		{
			return new FlipperVelocityData {
				AngularAcceleration = 0f,
				ContactTorque = 0f,
				CurrentTorque = 0f,
				Direction = d.AngleEnd >= d.AngleStart,
				IsInContact = false
			};
		}

		private FlipperHitData GetHitData()
		{
			var ratio = (math.max(BaseRadius, 0.01f) - math.max(EndRadius, 0.01f)) / math.max(FlipperRadiusMax, 0.01f);
			var zeroAngNorm = new float2(
				math.sqrt(1.0f - ratio * ratio), // F2 Norm, used in Green's transform, in FPM time search  // =  sinf(faceNormOffset)
				-ratio                              // F1 norm, change sign of x component, i.e -zeroAngNorm.x // = -cosf(faceNormOffset)
			);

			return new FlipperHitData {
				ZeroAngNorm = zeroAngNorm,
				HitMomentBit = true,
				HitVelocity = new float2(),
				LastHitFace = false,
			};
		}

		#endregion
	}
}
