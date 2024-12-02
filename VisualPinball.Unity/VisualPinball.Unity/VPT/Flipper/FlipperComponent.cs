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
// ReSharper disable CompareOfFloatsByEqualityOperator
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable InconsistentNaming
#endregion

using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.Game.Engines;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Flipper;
using VisualPinball.Engine.VPT.Table;
using Color = UnityEngine.Color;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Game Item/Flipper")]
	[HelpURL("https://docs.visualpinball.org/creators-guide/manual/mechanisms/flippers.html")]
	public class FlipperComponent : MainRenderableComponent<FlipperData>,
		IFlipperData, ISwitchDeviceComponent, ICoilDeviceComponent,
		IRotatableComponent
	{
		#region Data

		public Vector3 Position {
			get => transform.localPosition.TranslateToVpx();
			set => transform.localPosition = value.TranslateToWorld();
		}

		public float PosX => Position.x;
		public float PosY => Position.y;

		public float StartAngle {
			get => transform.localEulerAngles.y > 180 ? transform.localEulerAngles.y - 360 : transform.localEulerAngles.y;
			set => transform.SetLocalYRotation(math.radians(value));
		}

		[Range(-180f, 180f)]
		[Tooltip("Angle of the flipper in end position (flipped)")]
		public float EndAngle = 70.0f;

		// todo implement
		[Tooltip("This does nothing yet!")]
		public bool IsEnabled = true;

		[Tooltip("If set, the flipper relies on two coils, the power coil and the hold coil. Only enable if your gamelogic engine supports it (PinMAME abstracts dual-would flippers).")]
		public bool IsDualWound;

		[Range(0, 100f)]
		[Tooltip("Height of the flipper plastic.")]
		public float _height = 50.0f;
		public float Height => _height;

		[Range(0, 50f)]
		[Tooltip("Radius of the flipper's larger end.")]
		public float _baseRadius = 21.5f;
		public float BaseRadius => _baseRadius;

		[Range(0, 50f)]
		[Tooltip("Radius of the flipper's smaller end.")]
		public float _endRadius = 13.0f;
		public float EndRadius => _endRadius;

		[Min(0)]
		[Tooltip("It's not clear what TF this does. Relict from VPX.")]
		public float FlipperRadiusMin;

		[Range(10f, 250f)]
		[Tooltip("The length of the flipper")]
		public float FlipperRadiusMax = 130.0f;
		public float FlipperRadius => FlipperRadiusMax;

		[Range(0f, 50f)]
		[Tooltip("Thickness of the rubber")]
		public float _rubberThickness = 7.0f;
		public float RubberThickness => _rubberThickness;

		[Range(0f, 50f)]
		[Tooltip("Vertical position of the rubber")]
		public float _rubberHeight = 19.0f;
		public float RubberHeight => _rubberHeight;

		[Range(0, 100f)]
		[Tooltip("Vertical size of the rubber")]
		public float _rubberWidth = 24.0f;
		public float RubberWidth => _rubberWidth;

		[HideInInspector]
		public bool InstantiateAsPrefab;

		#endregion

		#region Overrides and Constants

		public override ItemType ItemType => ItemType.Flipper;
		public override string ItemName => "Flipper";

		public override FlipperData InstantiateData() => new FlipperData();

		public override bool HasProceduralMesh => !InstantiateAsPrefab;

		protected override Type MeshComponentType { get; } = typeof(MeshComponent<FlipperData, FlipperComponent>);
		protected override Type ColliderComponentType { get; } = typeof(ColliderComponent<FlipperData, FlipperComponent>);

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

		/// <summary>
		/// Returns the local-to-world matrix of the flipper, but without the rotation around the Y-axis.
		/// </summary>
		public float4x4 LocalToWorldPhysicsMatrix
		{
			get
			{
				var t = transform;
				var m = t.localToWorldMatrix;
				var r = t.localRotation.eulerAngles;
				return math.mul(m, math.inverse(float4x4.RotateY(math.radians(r.y))));
			}
		}

		private FlipperApi _flipperApi;
		public float _originalRotateZ;

		public float RotateZ {
			set {
				StartAngle = _originalRotateZ + value;
				_flipperApi.StartAngle = _originalRotateZ + value;
				UpdateTransforms();
			}
		}

		public float2 RotatedPosition {
			get => new(Position.x, Position.y);
			set {
				Position = new Vector3(value.x, value.y, Position.z);
				UpdateTransforms();
			}
		}

		#endregion

		#region Conversion

		public override IEnumerable<MonoBehaviour> SetData(FlipperData data)
		{
			var updatedComponents = new List<MonoBehaviour> { this };

			// transforms
			Position = new Vector3(data.Center.X, data.Center.Y, 0);
			transform.localEulerAngles = Vector3.zero;
			StartAngle = data.StartAngle > 180f ? data.StartAngle - 360f : data.StartAngle;

			// geometry
			_height = data.Height;
			_baseRadius = data.BaseRadius;
			_endRadius = data.EndRadius;
			EndAngle = data.EndAngle > 180f ? data.EndAngle - 360f : data.EndAngle;
			FlipperRadiusMin = data.FlipperRadiusMin;
			FlipperRadiusMax = data.FlipperRadiusMax;
			_rubberThickness = data.RubberThickness;
			_rubberHeight = data.RubberHeight;
			_rubberWidth = data.RubberWidth;

			// states
			IsEnabled = data.IsEnabled;
			IsDualWound = data.IsDualWound;

			// collider data
			var colliderComponent = gameObject.GetComponent<FlipperColliderComponent>();
			if (colliderComponent) {
				colliderComponent.Mass = data.Mass;
				colliderComponent.Strength = data.Strength;
				colliderComponent.Elasticity = data.Elasticity;
				colliderComponent.ElasticityFalloff = data.ElasticityFalloff;
				colliderComponent.Friction = data.Friction;
				colliderComponent.Return = data.Return;
				colliderComponent.RampUp = data.RampUp;
				colliderComponent.TorqueDamping = data.TorqueDamping;
				colliderComponent.TorqueDampingAngle = data.TorqueDampingAngle;
				colliderComponent.Scatter = data.Scatter;
				updatedComponents.Add(colliderComponent);
			}

			return updatedComponents;
		}

		public override IEnumerable<MonoBehaviour> SetReferencedData(FlipperData data, Table table, IMaterialProvider materialProvider, ITextureProvider textureProvider, Dictionary<string, IMainComponent> components)
		{
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
			data.Center = new Vertex2D(Position.x, Position.y);
			data.StartAngle = StartAngle;

			// geometry
			data.Height = _height;
			data.BaseRadius = _baseRadius;
			data.EndRadius = _endRadius;
			data.EndAngle = EndAngle;
			data.FlipperRadiusMin = FlipperRadiusMin;
			data.FlipperRadiusMax = FlipperRadiusMax;
			data.RubberThickness = _rubberThickness;
			data.RubberHeight = _rubberHeight;
			data.RubberWidth = _rubberWidth;

			// states
			data.IsEnabled = IsEnabled;
			data.IsDualWound = IsDualWound;

			// children visibility
			var baseMesh = GetComponentInChildren<FlipperBaseMeshComponent>();
			data.IsVisible = baseMesh && baseMesh.gameObject.activeInHierarchy;

			// collider data
			var colliderComponent = gameObject.GetComponent<FlipperColliderComponent>();
			if (colliderComponent) {
				data.Mass = colliderComponent.Mass;
				data.Strength = colliderComponent.Strength;
				data.Elasticity = colliderComponent.Elasticity;
				data.ElasticityFalloff = colliderComponent.ElasticityFalloff;
				data.Friction = colliderComponent.Friction;
				data.Return = colliderComponent.Return;
				data.RampUp = colliderComponent.RampUp;
				data.TorqueDamping = colliderComponent.TorqueDamping;
				data.TorqueDampingAngle = colliderComponent.TorqueDampingAngle;
				data.Scatter = colliderComponent.Scatter;
			}

			return data;
		}

		public override void CopyFromObject(GameObject go)
		{
			var flipperComponent = go.GetComponent<FlipperComponent>();
			if (flipperComponent != null) {
				Position = flipperComponent.Position;
				StartAngle = flipperComponent.StartAngle;
				EndAngle = flipperComponent.EndAngle;
				IsDualWound = flipperComponent.IsDualWound;
				_height = flipperComponent._height;
				_baseRadius = flipperComponent._baseRadius;
				_endRadius = flipperComponent._endRadius;
				FlipperRadiusMin = flipperComponent.FlipperRadiusMin;
				FlipperRadiusMax = flipperComponent.FlipperRadiusMax;
				_rubberThickness = flipperComponent._rubberThickness;
				_rubberHeight = flipperComponent._rubberHeight;
				_rubberWidth = flipperComponent._rubberWidth;

			} else {
				Position = go.transform.localPosition.TranslateToVpx();
			}

			UpdateTransforms();
		}

		#endregion

		#region Editor Tooling

		#if UNITY_EDITOR

		protected void OnDrawGizmosSelected()
		{
			const int height = 0;
			var poly = GetEnclosingPolygon(height: height);
			if (poly == null) {
				return;
			}
			
			Gizmos.matrix = Matrix4x4.identity;
			Handles.matrix = Matrix4x4.identity;

			// Draw enclosing polygon
			Gizmos.color = Color.cyan;
			if (IsLeft) {
				Gizmos.color = new Color(Gizmos.color.g, Gizmos.color.b, Gizmos.color.r, Gizmos.color.a);
			}
			for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++) {
				var a0 = transform.TransformPoint(poly[j].TranslateToWorld());
				var b0 = transform.TransformPoint(poly[i].TranslateToWorld());
				Gizmos.DrawLine(a0, b0);
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
				Gizmos.DrawLine(transform.TransformPoint(arrow[j].TranslateToWorld()), transform.TransformPoint(arrow[i].TranslateToWorld()));
			}

			if (start == end) {
				return;
			}
			var last = IsLeft ? arrow[0] : arrow[arrow.Count-1];
			var tmpA = IsLeft ? start + 90F + 3F : end +90F - 3F;
			var a = Quaternion.Euler(0, 0, tmpA) * new Vector3(0, -FlipperRadiusMax + 15F, height);
			var b = Quaternion.Euler(0, 0, tmpA) * new Vector3(0F, -FlipperRadiusMax + 25F, height);
			Gizmos.DrawLine(transform.TransformPoint(last.TranslateToWorld()), transform.TransformPoint(a.TranslateToWorld()));
			Gizmos.DrawLine(transform.TransformPoint(last.TranslateToWorld()), transform.TransformPoint(b.TranslateToWorld()));
			Gizmos.color = Color.white;
		}

		#endif

		#endregion

		#region Flipper Correction

		private bool IsLeft => EndAngle < StartAngle;

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

			float baseRadius = _baseRadius + margin;
			float tipRadius = _endRadius + margin;
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

		#endregion

		#region Runtime

		public FlipperApi FlipperApi { get; private set; }

		private void Awake()
		{
			_originalRotateZ = StartAngle;
			var player = GetComponentInParent<Player>();
			var physicsEngine = GetComponentInParent<PhysicsEngine>();
			FlipperApi = new FlipperApi(gameObject, player, physicsEngine);

			player.Register(FlipperApi, this);
			RegisterPhysics(physicsEngine);
		}

		private void Start()
		{
			_flipperApi = GetComponentInParent<Player>().TableApi.Flipper(this);
		}

		#endregion

		#region State

		internal FlipperState CreateState()
		{
			var colliderComponent = gameObject.GetComponent<FlipperColliderComponent>();
			if (colliderComponent) {

				// vpx physics
				var staticData = GetStaticData(colliderComponent);
				var state = new FlipperState(
					staticData,
					GetMovementData(staticData),
					GetVelocityData(staticData),
					GetHitData(),
					GetFlipperTricksData(colliderComponent, staticData),
					new SolenoidState { Value = false }
				);

				// flipper correction (nFozzy)
				if (colliderComponent.FlipperCorrection) {
					SetupFlipperCorrection(colliderComponent);
				}

				return state;
			}
			return default;
		}

		internal FlipperTricksData GetFlipperTricksData(FlipperColliderComponent colliderComponent, FlipperStaticData staticData)
		{
			return new FlipperTricksData
			{
				UseFlipperTricksPhysics = colliderComponent.useFlipperTricksPhysics,
				SOSRampUp = colliderComponent.SOSRampUp,
				SOSEM = colliderComponent.SOSEM,
				EOSReturn = colliderComponent.EOSReturn,
				EOSTNew = colliderComponent.EOSTNew,
				EOSANew = colliderComponent.EOSANew,
				EOSRampup = colliderComponent.EOSRampup,
				Overshoot = math.radians(colliderComponent.Overshoot),
				AngleEnd = staticData.AngleEnd,
				TorqueDamping = staticData.TorqueDamping,
				TorqueDampingAngle = staticData.TorqueDampingAngle,
				RampUpSpeed = staticData.RampUpSpeed,

				UseFlipperLiveCatch = colliderComponent.useFlipperLiveCatch,
				LiveCatchDistanceMin = colliderComponent.LiveCatchDistanceMin, // vp units from base
				LiveCatchDistanceMax = colliderComponent.LiveCatchDistanceMax, // vp units from base
				LiveCatchMinimalBallSpeed = colliderComponent.LiveCatchMinimalBallSpeed,
				LiveCatchPerfectTime = colliderComponent.LiveCatchPerfectTime,
				LiveCatchFullTime = colliderComponent.LiveCatchFullTime,
				LiveCatchInaccurateBounceSpeedMultiplier = colliderComponent.LiveCatchInaccurateBounceSpeedMultiplier,
				LiveCatchMinimalBounceSpeedMultiplier = colliderComponent.LiveCatchMinmalBounceSpeedMultiplier,

		//initialize
		OriginalAngleEnd = staticData.AngleEnd,
				OriginalRampUpSpeed = staticData.RampUpSpeed,
				OriginalTorqueDamping = staticData.TorqueDamping,
				OriginalTorqueDampingAngle = staticData.TorqueDampingAngle
			};
		}

		internal FlipperStaticData GetStaticData(FlipperColliderComponent colliderComponent)
		{
			float flipperRadius;
			if (FlipperRadiusMin > 0 && FlipperRadiusMax > FlipperRadiusMin) {
				flipperRadius = FlipperRadiusMax - (FlipperRadiusMax - FlipperRadiusMin) /* m_ptable->m_globalDifficulty*/;
				flipperRadius = math.max(flipperRadius, _baseRadius - _endRadius + 0.05f);

			} else {
				flipperRadius = FlipperRadiusMax;
			}

			var endRadius = math.max(_endRadius, 0.01f); // radius of flipper end
			flipperRadius = math.max(flipperRadius, 0.01f); // radius of flipper arc, center-to-center radius
			var angleStart = math.radians(StartAngle);
			var angleEnd = math.radians(EndAngle);

			if (angleEnd == angleStart) {
				// otherwise hangs forever in collisions/updates
				angleEnd += 0.0001f;
			}

			// model inertia of flipper as that of rod of length flipper around its end
			var inertia = (float) (1.0 / 3.0) * colliderComponent.Mass * (flipperRadius * flipperRadius);

			return new FlipperStaticData {
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

		internal FlipperMovementState GetMovementData(FlipperStaticData d)
		{
			// store flipper base rotation without starting angle
			var baseRotation = math.normalize(math.mul(
				math.normalize(transform.rotation),
				quaternion.EulerXYZ(0, 0, -d.AngleStart)
			));
			return new FlipperMovementState {
				Angle = d.AngleStart,
				AngleSpeed = 0f,
				AngularMomentum = 0f,
				EnableRotateEvent = 0,
			};
		}

		internal static FlipperVelocityData GetVelocityData(FlipperStaticData d)
		{
			return new FlipperVelocityData {
				AngularAcceleration = 0f,
				ContactTorque = 0f,
				CurrentTorque = 0f,
				Direction = d.AngleEnd >= d.AngleStart,
				IsInContact = false
			};
		}

		internal FlipperHitData GetHitData()
		{
			var ratio = (math.max(_baseRadius, 0.01f) - math.max(_endRadius, 0.01f)) / math.max(FlipperRadiusMax, 0.01f);
			var zeroAngNorm = new float2(
				math.sqrt(1.0f - ratio * ratio), // F2 Norm, used in Green's transform, in FPM time search  // =  sinf(faceNormOffset)
				-ratio                              // F1 norm, change sign of x component, i.e -zeroAngNorm.x // = -cosf(faceNormOffset)
			);

			return new FlipperHitData {
				ZeroAngNorm = zeroAngNorm,
				HitMomentBit = true,
				LastHitFace = false,
			};
		}

		private void SetupFlipperCorrection(FlipperColliderComponent colliderComponent)
		{
			var fc = colliderComponent.FlipperCorrection;
			var ta = GetComponentInParent<TableComponent>();

			if (!ta) {
				throw new InvalidOperationException("Cannot create correction trigger for flipper outside of the table hierarchy.");
			}

			// create new gameobject with the correct components, the gameobject will handle itself after that.
			var go = new GameObject {
				name = $"{name} (Flipper Correction)",
			};
			go.transform.SetParent(gameObject.transform.parent, false);
			var triggerComponent = go.AddComponent<TriggerComponent>();
			var triggerCollider = go.AddComponent<TriggerColliderComponent>();

			triggerCollider.ForFlipper = this;
			triggerCollider.TimeThresholdMs = fc.TimeThresholdMs;

			var localPos = transform.localPosition.TranslateToVpx();
			triggerComponent.Position = new Vector2(localPos.x, localPos.y);

			var poly = GetEnclosingPolygon(23, 12);
			triggerComponent.DragPoints = new DragPointData[poly.Count];
			triggerComponent.IsLocked = true;
			triggerCollider.HitHeight = 150F; // nFozzy's recommendation, but I think 50 should be ok

			// this was taken from the 2021 code when we had the playfield transformed to vpx world:
			// p = ta.transform.InverseTransformPoint(transform.TransformPoint(poly[i]))
			// this is basically (ta.transform.worldToLocalMatrix * transform.localToWorldMatrix)
			// but I couldn't get this transformation correctly from our current transforms.
			// using Matrix4x4.Rotate(quaternion.Euler(new float3(0, 0, -StartAngle))) and transforming
			// to localPos was close, but not close enough.
			var flipperToPlayfield = new Matrix4x4(
				new Vector4(-0.50754f, 0.86163f, 0, 0),
				new Vector4(-0.86163f, -0.50754f, 0, 0),
				new Vector4(0, 0, 1f, 0),
				new Vector4(278.21380f, 1803.27200f, 0, 1f)
			);

			for (var i = 0; i < poly.Count; i++) {
				// Poly points are expressed in flipper's frame: rotate to get it to the correct position.
				var p = flipperToPlayfield.MultiplyPoint3x4(poly[i]);
				triggerComponent.DragPoints[poly.Count - i - 1] = new DragPointData(p.x, p.y);
			}

			// create polarities and velocities curves
			var polarities = new float2[fc.PolaritiesCurveSlicingCount + 1];
			if (fc.Polarities != null) {
				Discretize(fc.Polarities, polarities);

			} else {
				for (var i = 0; i < fc.PolaritiesCurveSlicingCount + 1; i++) {
					polarities[i++] = new float2(i / (float)fc.PolaritiesCurveSlicingCount, 0);
				}
			}
			triggerCollider.FlipperPolarities = polarities;

			var velocities = new float2[fc.VelocitiesCurveSlicingCount + 1];
			if (fc.Velocities != null) {
				Discretize(fc.Velocities, velocities);

			} else {
				for (var i = 0; i < fc.VelocitiesCurveSlicingCount + 1; i++) {
					velocities[i++] = new float2(i / (float)fc.PolaritiesCurveSlicingCount, 1f);
				}
			}
			triggerCollider.FlipperVelocities = velocities;

			// need to explicitly register, since awake was called before the components were added.
			var player = GetComponentInParent<Player>();
			var physicsEngine = GetComponentInParent<PhysicsEngine>();
			triggerComponent.TriggerApi = new TriggerApi(triggerComponent.gameObject, player, physicsEngine);
			player.Register(triggerComponent.TriggerApi, triggerComponent);
			physicsEngine.Register(triggerComponent);
		}

		private static void Discretize(AnimationCurve curve, IList<float2> outArray)
		{
			var stepP = (curve[curve.length - 1].time - curve[0].time) / (outArray.Count - 1);
			var i = 0;
			for (var t = curve[0].time; t <= curve[curve.length - 1].time; t += stepP) {
				outArray[i++] = new float2(t, curve.Evaluate(t));
			}
		}

		#endregion
	}
}
