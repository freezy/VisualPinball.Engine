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
	/// <summary>
	/// The authored settings of the deformation renderer, plus what it would animate
	/// with the rubber's current bake.
	/// </summary>
	[CustomEditor(typeof(RubberDeformationRenderer)), CanEditMultipleObjects]
	public sealed class RubberDeformationRendererInspector : UnityEditor.Editor
	{
		public override bool RequiresConstantRepaint() => Application.isPlaying;

		public override void OnInspectorGUI()
		{
			DrawDefaultInspector();

			if (targets.Length != 1) {
				return;
			}
			var renderer = (RubberDeformationRenderer)target;
			var rubber = renderer.GetComponent<RubberComponent>();
			if (!rubber) {
				EditorGUILayout.HelpBox(
					"The deformation renderer belongs on a rubber; it animates that rubber's render mesh.",
					MessageType.Error);
				return;
			}
			if (!renderer.GetComponent<MeshFilter>()) {
				EditorGUILayout.HelpBox(
					"This rubber has no mesh filter, so there is nothing to displace.",
					MessageType.Warning);
			}

			var collider = renderer.GetComponent<RubberColliderComponent>();
			if (!collider || collider.Mode != RubberColliderMode.Physical
				|| !collider.DeformationEnabled) {
				EditorGUILayout.HelpBox(
					"Enable the compliant rebound on the rubber's Physical collider, otherwise no span ever deflects and nothing animates.",
					MessageType.Info);
				return;
			}

			var eligible = 0;
			var descriptors = collider.SpanDescriptors;
			for (var i = 0; i < descriptors.Count; i++) {
				if (descriptors[i].ReboundEligible) {
					eligible++;
				}
			}
			EditorGUILayout.LabelField("Animated free spans", eligible.ToString());
			if (eligible == 0) {
				EditorGUILayout.HelpBox(
					"No free span of this rubber is eligible for the compliant rebound; the Rubber Collider says why.",
					MessageType.Warning);
			}
			EditorGUILayout.HelpBox(
				"The kick curve and its duration also drive the arm of a physical slingshot bound to one of this rubber's spans.",
				MessageType.None);

			if (Application.isPlaying || renderer.IsPreviewing) {
				DrawRuntimeState(renderer);
			}
		}

		/// <summary>
		/// What the renderer is actually driving while the game runs, so a cord that
		/// stays still can be diagnosed from the inspector.
		/// </summary>
		private static void DrawRuntimeState(RubberDeformationRenderer renderer)
		{
			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Runtime", EditorStyles.boldLabel);
			var mesh = renderer.InstancedMesh;
			var meshFilter = renderer.GetComponent<MeshFilter>();
			EditorGUILayout.LabelField("Mesh instance", mesh
				? $"{mesh.name}, {mesh.vertexCount} vertices"
				: "none acquired");
			if (mesh && meshFilter && meshFilter.sharedMesh != mesh) {
				EditorGUILayout.HelpBox(
					$"The mesh filter renders '{(meshFilter.sharedMesh ? meshFilter.sharedMesh.name : "nothing")}', not the deformed instance.",
					MessageType.Warning);
			}
			var meshRenderer = renderer.GetComponent<MeshRenderer>();
			EditorGUILayout.LabelField("Mesh renderer", meshRenderer
				? $"{(meshRenderer.enabled ? "enabled" : "DISABLED")}, {(meshRenderer.isVisible ? "visible" : "not visible")}, {meshRenderer.sharedMaterials.Length} material(s)"
				: "none on this object");
			if (mesh && renderer.RestVertices != null) {
				var vertices = mesh.vertices;
				var rest = renderer.RestVertices;
				var maximum = 0f;
				var count = Mathf.Min(vertices.Length, rest.Count);
				for (var i = 0; i < count; i++) {
					maximum = Mathf.Max(maximum, (vertices[i] - rest[i]).magnitude);
				}
				EditorGUILayout.LabelField("Max displacement in instance", $"{maximum * 1000f:F2} mm");
			}
			foreach (var child in renderer.GetComponentsInChildren<Renderer>(true)) {
				if (child.gameObject == renderer.gameObject) {
					continue;
				}
				EditorGUILayout.LabelField("Child renderer",
					$"{child.gameObject.name}: {child.GetType().Name}, {(child.enabled && child.gameObject.activeInHierarchy ? "drawn" : "off")}");
			}
			if (renderer.gameObject.isStatic) {
				EditorGUILayout.HelpBox(
					"The rubber is marked static. Static batching renders the combined mesh, so per-vertex displacement is not visible. Clear the static flag on the rubber.",
					MessageType.Warning);
			}
			EditorGUILayout.LabelField("Bound spans / vertices",
				$"{renderer.SpanCount} / {renderer.BindingCount}");
			foreach (var line in renderer.DescribeBindings()) {
				EditorGUILayout.LabelField(" ", line);
			}
			EditorGUILayout.LabelField("Impacts / kicks / mesh updates",
				$"{renderer.ImpactCount} / {renderer.KickCount} / {renderer.MeshUpdateCount}");
			EditorGUILayout.LabelField("Dropped impacts", renderer.DroppedImpactCount.ToString());
			var rubber = renderer.GetComponent<RubberComponent>();
			if (GUILayout.Button("Pluck every span")) {
				renderer.Pluck(rubber ? rubber.Thickness * 2f : 10f);
			}
			renderer.HeldDeflection = EditorGUILayout.Slider(
				new GUIContent("Hold deflection", "Pins a V of this depth in VPX units on every span, inward positive, until set back to zero."),
				renderer.HeldDeflection, -60f, 60f);
		}
	}
}
