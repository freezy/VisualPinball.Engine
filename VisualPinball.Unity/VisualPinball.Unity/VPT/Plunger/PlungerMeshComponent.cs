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

using NLog;
using UnityEngine;
using VisualPinball.Engine.VPT.Plunger;
using Logger = NLog.Logger;
using PlungerType = VisualPinball.Engine.VPT.PlungerType;

namespace VisualPinball.Unity
{
	public abstract class PlungerMeshComponent : MeshComponent<PlungerData, PlungerComponent>
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
		private Mesh _runtimeMesh;

		internal static void RestorePackagedMeshes(Transform table)
		{
			foreach (var part in table.GetComponentsInChildren<PlungerMeshComponent>(true)) {
				part.RestorePackagedMesh();
			}
		}

		private void RestorePackagedMesh()
		{
			if (GetComponent<SkinnedMeshRenderer>() || !TryGetComponent<MeshRenderer>(out var renderer)
				|| !TryGetComponent<MeshFilter>(out var filter) || !filter.sharedMesh) {
				return;
			}
			var plunger = MainComponent;
			if (!plunger) {
				return;
			}

			var data = plunger.CopyDataTo(plunger.InstantiateData(), null, null, false);
			// The package reader keeps the table inactive until its components have been restored.
			var spring = plunger.GetComponentInChildren<PlungerSpringMeshComponent>(true);
			if (spring && spring.enabled && spring.gameObject.activeSelf) {
				data.Type = PlungerType.PlungerTypeCustom;
			}

			// glTFast exports the base mesh without blend shapes. Recreate the procedural mesh
			// from the unpacked settings and leave the imported mesh asset untouched.
			var generated = GetMesh(data);
			if (generated == null || !generated.IsSet || generated.Vertices.Length == 0 || generated.AnimationFrames.Count == 0) {
				return;
			}
			var imported = filter.sharedMesh;
			if (imported.vertexCount != generated.Vertices.Length || imported.subMeshCount != 1
				|| imported.GetIndexCount(0) != generated.Indices.Length) {
				Logger.Warn("Could not restore plunger animation on '{0}': the imported mesh does not match the procedural mesh.", name);
				return;
			}
			var mesh = generated.ToUnityMesh(imported.name);
			// VPX import can bake a surface-relative height into the mesh.
			var heightOffset = imported.bounds.center.y - mesh.bounds.center.y;
			if (!Mathf.Approximately(heightOffset, 0f)) {
				var vertices = mesh.vertices;
				for (var i = 0; i < vertices.Length; i++) {
					vertices[i].y += heightOffset;
				}
				mesh.vertices = vertices;
				mesh.RecalculateBounds();
			}
			if (Application.isPlaying) {
				_runtimeMesh = mesh;
			}
			var materials = renderer.sharedMaterials;
			var wasEnabled = renderer.enabled;
			// Replace the renderer before the package reader resolves renderer states and materials.
			DestroyImmediate(renderer);
			var skinned = gameObject.AddComponent<SkinnedMeshRenderer>();
			filter.sharedMesh = mesh;
			skinned.sharedMesh = mesh;
			skinned.sharedMaterials = materials;
			skinned.enabled = wasEnabled;
			var collider = plunger.GetComponent<PlungerColliderComponent>();
			skinned.SetBlendShapeWeight(0, collider ? collider.ParkPosition : generated.AnimationDefaultPosition);
			CalculateBoundingBox();
		}

		public override void RebuildMeshes()
		{
			base.RebuildMeshes();
			// When duplicating a plunger, the mesh filter gets a fresh generated mesh.
			if (TryGetComponent<SkinnedMeshRenderer>(out var skinned)
				&& TryGetComponent<MeshFilter>(out var filter) && filter.sharedMesh
				&& filter.sharedMesh.blendShapeCount > 0 && filter.sharedMesh != skinned.sharedMesh) {
				var weight = skinned.sharedMesh && skinned.sharedMesh.blendShapeCount > 0
					? skinned.GetBlendShapeWeight(0) : 0f;
				skinned.sharedMesh = filter.sharedMesh;
				skinned.SetBlendShapeWeight(0, weight);
			}
			CalculateBoundingBox();
		}

		public void CalculateBoundingBox()
		{
			if (!TryGetComponent<SkinnedMeshRenderer>(out var skinned) || !skinned.sharedMesh) {
				return;
			}
			var mesh = skinned.sharedMesh;
			var bounds = mesh.bounds;
			if (mesh.isReadable && mesh.blendShapeCount > 0) {
				var vertices = mesh.vertices;
				var deltas = new Vector3[vertices.Length];
				// Include the fully pulled pose as well as the base mesh.
				mesh.GetBlendShapeFrameVertices(0, mesh.GetBlendShapeFrameCount(0) - 1, deltas, null, null);
				for (var i = 0; i < vertices.Length; i++) {
					bounds.Encapsulate(vertices[i] + deltas[i]);
				}
			}
			skinned.localBounds = bounds;
		}

		private void OnDestroy()
		{
			if (_runtimeMesh) {
				Destroy(_runtimeMesh);
			}
		}
	}
}
