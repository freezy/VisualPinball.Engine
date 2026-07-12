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

using UnityEditor;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(DropTargetColliderComponent)), CanEditMultipleObjects]
	public class DropTargetColliderInspector : TargetColliderInspector<DropTargetColliderComponent>
	{
		private SerializedProperty _collisionColliderMeshProperty;
		private SerializedProperty _physicsModeProperty;
		private SerializedProperty _mechanicalProfileProperty;
		private SerializedProperty _overrideMechanicalProfileProperty;
		private SerializedProperty _mechanicalOverridesProperty;
		private SerializedProperty _rothConfigProperty;
		private bool _runtimeDiagnosticsFoldout = true;

		protected override void OnEnable()
		{
			base.OnEnable();
			_collisionColliderMeshProperty = serializedObject.FindProperty(nameof(DropTargetColliderComponent.CollisionColliderMesh));
			_physicsModeProperty = serializedObject.FindProperty(nameof(DropTargetColliderComponent.PhysicsMode));
			_mechanicalProfileProperty = serializedObject.FindProperty(nameof(DropTargetColliderComponent.MechanicalProfile));
			_overrideMechanicalProfileProperty = serializedObject.FindProperty(nameof(DropTargetColliderComponent.OverrideMechanicalProfile));
			_mechanicalOverridesProperty = serializedObject.FindProperty(nameof(DropTargetColliderComponent.MechanicalOverrides));
			_rothConfigProperty = serializedObject.FindProperty(nameof(DropTargetColliderComponent.RothConfig));
		}

		protected override void OnTargetInspectorGUI()
		{
			PropertyField(_physicsModeProperty, updateColliders: true);
			PropertyField(_collisionColliderMeshProperty, "Collision Collider", updateColliders: true);

			var mode = (DropTargetPhysicsMode)_physicsModeProperty.intValue;
			if (mode == DropTargetPhysicsMode.RothCompatible) {
				EditorGUILayout.PropertyField(_rothConfigProperty, true);
			} else if (mode == DropTargetPhysicsMode.Mechanical) {
				PropertyField(_mechanicalProfileProperty);
				PropertyField(_overrideMechanicalProfileProperty);
				if (_mechanicalProfileProperty.objectReferenceValue == null || _overrideMechanicalProfileProperty.boolValue) {
					EditorGUILayout.PropertyField(_mechanicalOverridesProperty, true);
				}
			}

			if (targets.Length == 1) {
				DrawValidation((DropTargetColliderComponent)target, mode);
				DrawRuntimeDiagnostics((DropTargetColliderComponent)target, mode);
			}
		}

		private void DrawRuntimeDiagnostics(DropTargetColliderComponent component,
			DropTargetPhysicsMode mode)
		{
			if (!Application.isPlaying || mode != DropTargetPhysicsMode.Mechanical || !TableComponent) {
				return;
			}
			var player = TableComponent.GetComponent<Player>();
			var mainComponent = component.GetComponent<DropTargetComponent>();
			var api = player && mainComponent ? player.TableApi.DropTarget(mainComponent) : null;
			if (api == null || !api.TryGetMechanicalDiagnostics(out var diagnostics)) {
				return;
			}

			_runtimeDiagnosticsFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(
				_runtimeDiagnosticsFoldout, "Mechanical Runtime Diagnostics");
			if (_runtimeDiagnosticsFoldout) {
				using (new EditorGUI.DisabledScope(true)) {
					EditorGUILayout.TextField("State", diagnostics.State);
					EditorGUILayout.TextField("Last Outcome", diagnostics.LastImpactOutcome);
					EditorGUILayout.FloatField("Rear Travel", diagnostics.RearTravel);
					EditorGUILayout.FloatField("Rear Velocity", diagnostics.RearVelocity);
					EditorGUILayout.FloatField("Drop Travel", diagnostics.DropTravel);
					EditorGUILayout.FloatField("Drop Velocity", diagnostics.DropVelocity);
					EditorGUILayout.Toggle("Dropped Switch", diagnostics.DroppedSwitchClosed);
				}
			}
			EditorGUILayout.EndFoldoutHeaderGroup();
			Repaint();
		}

		private static void DrawValidation(DropTargetColliderComponent component, DropTargetPhysicsMode mode)
		{
			if (mode == DropTargetPhysicsMode.Legacy) {
				return;
			}
			if (!component.FrontColliderMesh) {
				EditorGUILayout.HelpBox("Advanced drop-target physics requires a front collider mesh.", MessageType.Error);
			}
			if (mode == DropTargetPhysicsMode.RothCompatible && !component.CollisionColliderMesh) {
				EditorGUILayout.HelpBox(
					"Without a dedicated Collision Collider, Roth mode uses the front mesh as a solid fallback and cannot reproduce the sensor-plus-offset-wall arrangement exactly.",
					MessageType.Warning);
				return;
			}
			if (mode != DropTargetPhysicsMode.Mechanical) {
				return;
			}

			var config = component.ResolvedMechanicalConfig;
			if (component.MechanicalProfile && !component.OverrideMechanicalProfile) {
				var profile = component.MechanicalProfile;
				var calibration = profile.Calibration == DropTargetProfileCalibration.Measured
					? $"Measured profile: {profile.MechanismName}"
					: $"Provisional profile: {profile.MechanismName}";
				EditorGUILayout.HelpBox(calibration, profile.Calibration == DropTargetProfileCalibration.Measured
					? MessageType.Info : MessageType.Warning);
				if (profile.Calibration == DropTargetProfileCalibration.Measured
					&& string.IsNullOrWhiteSpace(profile.CalibrationSource)) {
					EditorGUILayout.HelpBox("Measured profiles must identify their source data and validation.", MessageType.Error);
				}
			} else {
				EditorGUILayout.HelpBox(
					"Local mechanical values are provisional until they are fitted and validated against a real mechanism.",
					MessageType.Warning);
			}

			if (config.EffectiveFaceMass <= 0f || config.DropMass <= 0f || config.ResetEffectiveMass <= 0f) {
				EditorGUILayout.HelpBox("Face, drop, and reset effective masses must be greater than zero.", MessageType.Error);
			}
			if (config.DropTravel <= 0f || config.RearStopTravel < config.LatchReleaseTravel) {
				EditorGUILayout.HelpBox(
					"Drop travel must be positive and rear-stop travel must not precede latch release.",
					MessageType.Error);
			}
			if (config.LatchRelatchTravel > config.LatchReleaseTravel
				|| config.LatchEscapeDrop > config.DropTravel) {
				EditorGUILayout.HelpBox(
					"Relatch travel must not exceed release travel, and latch escape must occur before the down stop.",
					MessageType.Error);
			}
			if (config.DroppedSwitchTravel > config.DropTravel
				|| config.RaisedSwitchTravel > config.DropTravel) {
				EditorGUILayout.HelpBox("Switch travel thresholds must lie within the drop stroke.", MessageType.Error);
			}
			if (config.DeflectionKind == DropTargetDeflectionKind.HingedBlade) {
				var axis = config.DeflectionAxis;
				var lever = Vector3.Cross(axis.normalized,
					config.ReferenceContactPoint - config.DeflectionPivot);
				if (axis.sqrMagnitude < 1e-6f || lever.sqrMagnitude < 1e-6f) {
					EditorGUILayout.HelpBox(
						"A hinged blade requires a non-zero local hinge axis and a reference contact point away from that axis.",
						MessageType.Error);
				}
				if (Mathf.Max(config.LatchReleaseTravel, config.RearStopTravel) > Mathf.PI * 0.5f) {
					EditorGUILayout.HelpBox(
						"Hinged travel thresholds are radians; release or rear-stop travel exceeds 90 degrees.",
						MessageType.Warning);
				}
			}
		}

		[DrawGizmo(GizmoType.Selected | GizmoType.Active)]
		private static void DrawMechanicalTravelGizmo(DropTargetColliderComponent component, GizmoType _)
		{
			if (component.PhysicsMode != DropTargetPhysicsMode.Mechanical) {
				return;
			}
			var config = component.ResolvedMechanicalConfig;
			var origin = component.transform.position;
			var up = component.transform.up;
			var rear = component.transform.forward;
			var dropEnd = origin - up * VisualPinball.Unity.Physics.ScaleToWorld(config.DropTravel);
			var resetEnd = origin + up * VisualPinball.Unity.Physics.ScaleToWorld(config.ResetOvershootTravel);
			var release = origin + rear * VisualPinball.Unity.Physics.ScaleToWorld(config.LatchReleaseTravel);
			var rearStop = origin + rear * VisualPinball.Unity.Physics.ScaleToWorld(config.RearStopTravel);

			Handles.color = new Color(0.2f, 0.8f, 1f, 1f);
			Handles.DrawLine(origin, dropEnd, 2f);
			Handles.DrawLine(origin, resetEnd, 2f);
			Handles.Label(dropEnd, "Down stop");
			Handles.Label(resetEnd, "Reset overshoot");
			Handles.color = new Color(1f, 0.65f, 0.15f, 1f);
			if (config.DeflectionKind == DropTargetDeflectionKind.HingedBlade) {
				var localPivot = VisualPinball.Unity.Physics.VpxToWorld.MultiplyPoint(config.DeflectionPivot);
				var localAxis = VisualPinball.Unity.Physics.VpxToWorld.MultiplyVector(config.DeflectionAxis);
				var localReference = VisualPinball.Unity.Physics.VpxToWorld.MultiplyPoint(
					config.ReferenceContactPoint);
				var pivot = component.transform.TransformPoint(localPivot);
				var axis = component.transform.TransformDirection(localAxis).normalized;
				var reference = component.transform.TransformPoint(localReference);
				var referenceArm = reference - pivot;
				if (axis.sqrMagnitude > 1e-6f && referenceArm.sqrMagnitude > 1e-8f) {
					Handles.DrawWireArc(pivot, axis, referenceArm,
						config.RearStopTravel * Mathf.Rad2Deg, referenceArm.magnitude, 2f);
					var releaseArm = Quaternion.AngleAxis(config.LatchReleaseTravel * Mathf.Rad2Deg, axis)
						* referenceArm;
					Handles.DrawLine(pivot, pivot + releaseArm, 2f);
					Handles.Label(pivot + releaseArm, "Latch release");
					Handles.Label(pivot, "Hinge pivot");
				}
				return;
			}
			Handles.DrawLine(origin, rearStop, 2f);
			Handles.DrawWireDisc(release, up, VisualPinball.Unity.Physics.ScaleToWorld(1f));
			Handles.Label(release, "Latch release");
			Handles.Label(rearStop, "Rear stop");
		}
	}
}
