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
	/// The authored settings of the deformation renderer, plus what it animates on the
	/// rubber's guided path.
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

			if (rubber.PathSource != RubberPathSource.Guides) {
				EditorGUILayout.HelpBox(
					"Only the free spans of a guide-driven rubber are animated. Bind this rubber to its guides first.",
					MessageType.Info);
				return;
			}

			var freeSpans = 0;
			foreach (var element in rubber.BakedPath) {
				if (element.Type == RubberPathElementType.FreeSpan) {
					freeSpans++;
				}
			}
			EditorGUILayout.LabelField("Animated free spans", freeSpans.ToString());
			EditorGUILayout.HelpBox(
				"A rubber slingshot drives the cord. Without an animated arm it plays the kick curve over its duration.",
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
			EditorGUILayout.LabelField("Kicks / mesh updates",
				$"{renderer.KickCount} / {renderer.MeshUpdateCount}");
			renderer.HeldDeflection = EditorGUILayout.Slider(
				new GUIContent("Hold deflection", "Pins a V of this depth in VPX units on every span, inward positive, until set back to zero."),
				renderer.HeldDeflection, -60f, 60f);
		}
	}
}
