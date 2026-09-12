// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
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

using UnityEditor;
using UnityEngine;
using float3 = global::Unity.Mathematics.float3;
using VisualPinball.Engine.Common;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(MagnetComponent))]
	public class MagnetInspector : ItemInspector
	{
		private const float OrdinaryShotMinimumSpeed = 8f;

		private SerializedProperty _radiusProperty;
		private SerializedProperty _strengthProperty;
		private SerializedProperty _magnetTypeProperty;
		private SerializedProperty _forceProfileProperty;
		private SerializedProperty _coilRiseTimeProperty;
		private SerializedProperty _coilFallTimeProperty;
		private SerializedProperty _poleRadiusProperty;
		private SerializedProperty _grabBallProperty;
		private SerializedProperty _grabRadiusProperty;
		private SerializedProperty _cylinderRadiusProperty;
		private SerializedProperty _cylinderHeightProperty;
		private SerializedProperty _cylindricalDampingProperty;
		private SerializedProperty _generateCylinderColliderProperty;
		private SerializedProperty _heightRangeProperty;
		private SerializedProperty _isEnabledOnStartProperty;
		private SerializedProperty _isKinematicProperty;
		private SerializedProperty _drawDebugForcesProperty;
		private SerializedProperty _hitThresholdProperty;
		private SerializedProperty _coupleToParentHingeProperty;
		private SerializedProperty _heldBallCentreOffsetProperty;
		private SerializedProperty _holdStiffnessProperty;
		private SerializedProperty _holdDampingProperty;
		private SerializedProperty _maxHoldForceProperty;
		private IApiCoil _runtimeCoil;
		private bool? _lastRuntimeCoilStatus;

		protected override MonoBehaviour UndoTarget => target as MonoBehaviour;

		protected override void OnEnable()
		{
			base.OnEnable();

			_radiusProperty = serializedObject.FindProperty(nameof(MagnetComponent.Radius));
			_strengthProperty = serializedObject.FindProperty(nameof(MagnetComponent.Strength));
			_magnetTypeProperty = serializedObject.FindProperty(nameof(MagnetComponent.MagnetType));
			_forceProfileProperty = serializedObject.FindProperty(nameof(MagnetComponent.ForceProfile));
			_coilRiseTimeProperty = serializedObject.FindProperty(nameof(MagnetComponent.CoilRiseTime));
			_coilFallTimeProperty = serializedObject.FindProperty(nameof(MagnetComponent.CoilFallTime));
			_poleRadiusProperty = serializedObject.FindProperty(nameof(MagnetComponent.PoleRadius));
			_grabBallProperty = serializedObject.FindProperty(nameof(MagnetComponent.GrabBall));
			_grabRadiusProperty = serializedObject.FindProperty(nameof(MagnetComponent.GrabRadius));
			_cylinderRadiusProperty = serializedObject.FindProperty(nameof(MagnetComponent.CylinderRadius));
			_cylinderHeightProperty = serializedObject.FindProperty(nameof(MagnetComponent.CylinderHeight));
			_cylindricalDampingProperty = serializedObject.FindProperty(nameof(MagnetComponent.CylindricalDamping));
			_generateCylinderColliderProperty = serializedObject.FindProperty(nameof(MagnetComponent.GenerateCylinderCollider));
			_heightRangeProperty = serializedObject.FindProperty(nameof(MagnetComponent.HeightRange));
			_isEnabledOnStartProperty = serializedObject.FindProperty(nameof(MagnetComponent.IsEnabledOnStart));
			_isKinematicProperty = serializedObject.FindProperty(nameof(MagnetComponent.IsKinematic));
			_drawDebugForcesProperty = serializedObject.FindProperty(nameof(MagnetComponent.DrawDebugForces));
			_hitThresholdProperty = serializedObject.FindProperty(nameof(MagnetComponent.HitThreshold));
			_coupleToParentHingeProperty = serializedObject.FindProperty(nameof(MagnetComponent.CoupleToParentHinge));
			_heldBallCentreOffsetProperty = serializedObject.FindProperty(nameof(MagnetComponent.HeldBallCentreOffset));
			_holdStiffnessProperty = serializedObject.FindProperty(nameof(MagnetComponent.HoldStiffness));
			_holdDampingProperty = serializedObject.FindProperty(nameof(MagnetComponent.HoldDamping));
			_maxHoldForceProperty = serializedObject.FindProperty(nameof(MagnetComponent.MaxHoldForce));
		}

		protected override void OnDisable()
		{
			SetRuntimeCoil(null, null);
			base.OnDisable();
		}

		public override void OnInspectorGUI()
		{
			UpdateRuntimeCoilSubscription();
			BeginEditing();
			OnPreInspectorGUI();
			if (Application.isPlaying) {
				var magnet = target as MagnetComponent;
				var isOn = magnet && magnet.MagnetApi != null ? magnet.MagnetApi.IsEnabled : magnet && magnet.IsEnabledOnStart;
				EditorGUILayout.HelpBox(isOn ? "Runtime Coil Status: ON" : "Runtime Coil Status: OFF", isOn ? MessageType.Info : MessageType.Warning);
			}

			using (new EditorGUI.DisabledScope(Application.isPlaying)) {
				PropertyField(_magnetTypeProperty);
			}
			var isSpatial = _magnetTypeProperty.enumValueIndex == (int)MagnetType.Spatial;
			var isCylindrical = _magnetTypeProperty.enumValueIndex == (int)MagnetType.Cylindrical;
			var isThreeDimensional = isSpatial || isCylindrical;
			PropertyField(_radiusProperty, isCylindrical ? "Influence Distance" : "Influence Radius");
			if (isCylindrical) {
				PropertyField(_cylinderRadiusProperty);
				PropertyField(_cylinderHeightProperty);
				using (new EditorGUI.DisabledScope(Application.isPlaying)) {
					PropertyField(_generateCylinderColliderProperty, updateColliders: true);
					if (_generateCylinderColliderProperty.boolValue) {
						PropertyField(_hitThresholdProperty, "Hit Threshold");
					}
				}
				if (!_generateCylinderColliderProperty.boolValue) {
					EditorGUILayout.HelpBox("Enable Generate Cylinder Collider to emit Hit switch pulses.", MessageType.Info);
				}
				DrawColliderFit();
				DrawOverlappingColliderWarning();
			} else if (!isSpatial) {
				PropertyField(_heightRangeProperty);
			}
			if (!isCylindrical) {
				EditorGUILayout.HelpBox("The Hit switch requires a Cylindrical magnet with Generate Cylinder Collider enabled.", MessageType.Info);
			}
			PropertyField(_strengthProperty);
			if (isCylindrical) {
				PropertyField(_cylindricalDampingProperty, "Damping");
			}
			if (!isThreeDimensional) {
				PropertyField(_forceProfileProperty);
			}
			var usesPhysicalResponse = isThreeDimensional || _forceProfileProperty.enumValueIndex == (int)MagnetForceProfile.Physical;
			if (usesPhysicalResponse) {
				if (!isCylindrical) {
					PropertyField(_poleRadiusProperty);
				}
				PropertyField(_coilRiseTimeProperty);
				PropertyField(_coilFallTimeProperty);
			}

			EditorGUILayout.Space(8f);
			PropertyField(_grabBallProperty);
			if (_grabBallProperty.boolValue && !isCylindrical) {
				PropertyField(_grabRadiusProperty);
			}

			EditorGUILayout.Space(8f);
			EditorGUILayout.LabelField("Spring Hinge Ownership", EditorStyles.boldLabel);
			using (new EditorGUI.DisabledScope(Application.isPlaying)) {
				PropertyField(_coupleToParentHingeProperty);
			}
			if (_coupleToParentHingeProperty.hasMultipleDifferentValues || _coupleToParentHingeProperty.boolValue) {
				PropertyField(_heldBallCentreOffsetProperty);
				PropertyField(_holdStiffnessProperty);
				PropertyField(_holdDampingProperty);
				PropertyField(_maxHoldForceProperty);
				DrawOwnedModeValidation(isSpatial);
			}

			EditorGUILayout.Space(8f);
			PropertyField(_isEnabledOnStartProperty);
			// kinematic registration is fixed at startup; toggling during play would silently do nothing
			using (new EditorGUI.DisabledScope(Application.isPlaying)) {
				PropertyField(_isKinematicProperty);
			}
			PropertyField(_drawDebugForcesProperty);

			base.OnInspectorGUI();
			EndEditing();
		}

		private void UpdateRuntimeCoilSubscription()
		{
			var magnet = target as MagnetComponent;
			if (!Application.isPlaying || !magnet || magnet.MagnetApi == null) {
				SetRuntimeCoil(null, null);
				return;
			}

			var coil = ((ICoilDeviceComponent)magnet).CoilDevice(MagnetComponent.MagnetCoilItem);
			SetRuntimeCoil(coil, magnet.MagnetApi.IsEnabled);
		}

		private void SetRuntimeCoil(IApiCoil coil, bool? isEnabled)
		{
			if (ReferenceEquals(_runtimeCoil, coil)) {
				return;
			}
			if (_runtimeCoil != null) {
				_runtimeCoil.CoilStatusChanged -= OnRuntimeCoilStatusChanged;
			}
			_runtimeCoil = coil;
			_lastRuntimeCoilStatus = isEnabled;
			if (_runtimeCoil != null) {
				_runtimeCoil.CoilStatusChanged += OnRuntimeCoilStatusChanged;
			}
		}

		private void OnRuntimeCoilStatusChanged(object sender, NoIdCoilEventArgs eventArgs)
		{
			if (_lastRuntimeCoilStatus == eventArgs.IsEnergized) {
				return;
			}
			_lastRuntimeCoilStatus = eventArgs.IsEnergized;
			Repaint();
		}

		private void DrawOwnedModeValidation(bool isSpatial)
		{
			var magnet = target as MagnetComponent;
			var owner = magnet ? magnet.GetComponentInParent<SpringHingeComponent>() : null;
			using (new EditorGUI.DisabledScope(true)) {
				EditorGUILayout.ObjectField("Resolved Owner", owner,
					typeof(SpringHingeComponent), true);
			}
			if (!owner) {
				EditorGUILayout.HelpBox("Owned mode requires a parent Spring Hinge.", MessageType.Error);
			}
			if (!isSpatial) {
				EditorGUILayout.HelpBox("Owned mode requires a Spatial magnet.", MessageType.Error);
				if (GUILayout.Button("Use Spatial Mode")) {
					_magnetTypeProperty.enumValueIndex = (int)MagnetType.Spatial;
				}
			}
			if (owner) {
				var ownedCount = 0;
				foreach (var candidate in owner.GetComponentsInChildren<MagnetComponent>(true)) {
					if (candidate.CoupleToParentHinge) {
						ownedCount++;
					}
				}
				if (ownedCount > 1) {
					EditorGUILayout.HelpBox("Only one owned magnet is supported per spring hinge.", MessageType.Error);
				} else if (isSpatial) {
					DrawCaptureEstimate();
				}

				var proxy = owner.GetComponent<SpringHingeColliderComponent>();
				if (!proxy) {
					EditorGUILayout.HelpBox("The parent Spring Hinge needs a Spring Hinge Collider before its hold point can be checked.", MessageType.Error);
				} else {
					if (SpringHingeAuthoring.TryGetHeldBallCentreGap(owner, proxy, magnet, out var gap)
					    && Mathf.Abs(gap) > PhysicsConstants.PhysTouch) {
						var message = gap < 0f
							? $"The hold point puts a standard ball {-gap:0.##} units inside the hinge collider, so it cannot be grabbed."
							: $"The hold point leaves a standard ball {gap:0.##} units away from the hinge collider, so it cannot be grabbed.";
						EditorGUILayout.HelpBox(message, MessageType.Warning);
					}
					using (new EditorGUI.DisabledScope(Application.isPlaying
					           || _heldBallCentreOffsetProperty.hasMultipleDifferentValues)) {
						if (GUILayout.Button("Fit Hold Point to Collider")
						    && SpringHingeAuthoring.TryGetFittedHeldBallCentreOffset(owner, proxy,
							    magnet, out var offset)) {
							_heldBallCentreOffsetProperty.vector3Value = offset;
						}
					}
				}
			}
		}

		private void DrawCaptureEstimate()
		{
			if (_radiusProperty.hasMultipleDifferentValues
			    || _strengthProperty.hasMultipleDifferentValues
			    || _poleRadiusProperty.hasMultipleDifferentValues
			    || _grabBallProperty.hasMultipleDifferentValues
			    || _grabRadiusProperty.hasMultipleDifferentValues
			    || _heldBallCentreOffsetProperty.hasMultipleDifferentValues
			    || _maxHoldForceProperty.hasMultipleDifferentValues) {
				return;
			}
			var state = new MagnetState {
				Radius = _radiusProperty.floatValue,
				Strength = _strengthProperty.floatValue,
				EffectiveCurrent = 1f,
				EffectiveStrength = _strengthProperty.floatValue,
				PoleRadius = _poleRadiusProperty.floatValue,
				GrabRadius = _grabBallProperty.boolValue ? _grabRadiusProperty.floatValue : 0f,
				MaxHoldForce = _maxHoldForceProperty.floatValue
			};
			var pole = float3.zero;
			var target = (float3)_heldBallCentreOffsetProperty.vector3Value;
			var speed = OwnedMagnetPhysics.EstimateStationaryHingeCaptureSpeed(in state,
				in pole, in target);
			var message = $"Best-case full-power capture speed at the hold point: about {speed:0.#} VPE ball-speed units for a standard ball while the toy is stationary. Actual capture may be lower because the ball enters the grab area away from this point, the coil takes time to energize, and the toy may be moving.";
			EditorGUILayout.HelpBox(message,
				speed < OrdinaryShotMinimumSpeed ? MessageType.Warning : MessageType.Info);
		}

		private void DrawColliderFit()
		{
			if (!TryGetChildColliderSize(out var radius, out var height, out var colliderName, out var error)) {
				EditorGUILayout.HelpBox(error, MessageType.Info);
				return;
			}

			var doesNotMatch = Mathf.Abs(_cylinderRadiusProperty.floatValue - radius) > 0.5f ||
			                   Mathf.Abs(_cylinderHeightProperty.floatValue - height) > 0.5f;
			if (doesNotMatch) {
				EditorGUILayout.HelpBox($"The magnetic surface does not match '{colliderName}'. Its mesh suggests Radius {radius:0.##} and Height {height:0.##} VPX.", MessageType.Warning);
			}
			if (GUILayout.Button("Fit Cylinder to Child Collider Mesh")) {
				_cylinderRadiusProperty.floatValue = radius;
				_cylinderHeightProperty.floatValue = height;
			}
		}

		private void DrawOverlappingColliderWarning()
		{
			if (!_generateCylinderColliderProperty.boolValue) {
				return;
			}
			var magnet = target as MagnetComponent;
			var colliders = magnet ? magnet.GetComponentsInChildren<ICollidableComponent>(true) : null;
			if (colliders == null) {
				return;
			}
			foreach (var collider in colliders) {
				if (ReferenceEquals(collider, magnet) || collider is not Component component) {
					continue;
				}
				if (component.gameObject == magnet.gameObject) {
					EditorGUILayout.HelpBox($"Move the other collider '{component.GetType().Name}' to its own GameObject. Enabling it overlaps the generated cylinder, while disabling a collider on the Magnet GameObject disables their shared physics item ID.", MessageType.Error);
					return;
				}
				if (component is Behaviour behaviour && behaviour.isActiveAndEnabled) {
					EditorGUILayout.HelpBox($"Disable the overlapping collider '{component.name}' ({component.GetType().Name}). The generated smooth cylinder must be the only collider for this surface.", MessageType.Warning);
					return;
				}
			}
		}

		private bool TryGetChildColliderSize(out float radius, out float height, out string colliderName, out string error)
		{
			radius = 0f;
			height = 0f;
			colliderName = null;
			error = null;
			var magnet = target as MagnetComponent;
			var colliders = magnet ? magnet.GetComponentsInChildren<PrimitiveColliderComponent>(true) : null;
			if (colliders == null || colliders.Length == 0) {
				error = "Set Cylinder Radius and Height to the solid collider's dimensions in VPX units.";
				return false;
			}
			if (colliders.Length > 1) {
				error = "More than one child Primitive Collider was found. Set Cylinder Radius and Height manually so the magnetic surface matches the intended collider.";
				return false;
			}
			var collider = colliders[0];
			var meshFilter = collider ? collider.GetComponent<MeshFilter>() : null;
			if (!meshFilter || !meshFilter.sharedMesh) {
				error = $"Child collider '{collider.name}' has no readable mesh bounds. Set Cylinder Radius and Height manually.";
				return false;
			}

			var bounds = meshFilter.sharedMesh.bounds;
			var playfield = magnet.GetComponentInParent<PlayfieldComponent>();
			var origin = playfield
				? magnet.transform.position.TranslateToVpx(playfield.transform)
				: magnet.transform.localPosition.TranslateToVpx();
			var center = MeshPointToVpx(meshFilter, bounds.center, playfield);
			var xExtent = MeshPointToVpx(meshFilter, bounds.center + new Vector3(bounds.extents.x, 0f, 0f), playfield);
			var zExtent = MeshPointToVpx(meshFilter, bounds.center + new Vector3(0f, 0f, bounds.extents.z), playfield);
			var top = MeshPointToVpx(meshFilter, bounds.center + new Vector3(0f, bounds.extents.y, 0f), playfield);
			radius = Mathf.Max(
				Vector2.Distance(new Vector2(center.x, center.y), new Vector2(xExtent.x, xExtent.y)),
				Vector2.Distance(new Vector2(center.x, center.y), new Vector2(zExtent.x, zExtent.y))
			);
			height = top.z - origin.z;
			colliderName = collider.name;
			if (radius > 0f && height > 0f) {
				return true;
			}
			error = $"Child collider '{collider.name}' does not have usable upright cylinder bounds. Set Cylinder Radius and Height manually.";
			return false;
		}

		private static Vector3 MeshPointToVpx(MeshFilter meshFilter, Vector3 localPoint, PlayfieldComponent playfield)
		{
			var worldPoint = meshFilter.transform.TransformPoint(localPoint);
			return playfield ? worldPoint.TranslateToVpx(playfield.transform) : worldPoint.TranslateToVpx();
		}
	}
}
