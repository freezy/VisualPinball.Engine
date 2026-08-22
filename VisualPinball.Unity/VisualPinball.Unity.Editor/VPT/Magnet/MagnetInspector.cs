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

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(MagnetComponent))]
	public class MagnetInspector : ItemInspector
	{
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
		private SerializedProperty _heightRangeProperty;
		private SerializedProperty _isEnabledOnStartProperty;
		private SerializedProperty _isKinematicProperty;
		private SerializedProperty _drawDebugForcesProperty;

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
			_heightRangeProperty = serializedObject.FindProperty(nameof(MagnetComponent.HeightRange));
			_isEnabledOnStartProperty = serializedObject.FindProperty(nameof(MagnetComponent.IsEnabledOnStart));
			_isKinematicProperty = serializedObject.FindProperty(nameof(MagnetComponent.IsKinematic));
			_drawDebugForcesProperty = serializedObject.FindProperty(nameof(MagnetComponent.DrawDebugForces));
		}

		public override void OnInspectorGUI()
		{
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
				DrawColliderFit();
			} else if (!isSpatial) {
				PropertyField(_heightRangeProperty);
			}
			PropertyField(_strengthProperty);
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
			PropertyField(_isEnabledOnStartProperty);
			// kinematic registration is fixed at startup; toggling during play would silently do nothing
			using (new EditorGUI.DisabledScope(Application.isPlaying)) {
				PropertyField(_isKinematicProperty);
			}
			PropertyField(_drawDebugForcesProperty);

			base.OnInspectorGUI();
			EndEditing();
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
