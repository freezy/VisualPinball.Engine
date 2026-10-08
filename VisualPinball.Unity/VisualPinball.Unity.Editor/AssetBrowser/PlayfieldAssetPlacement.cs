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

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using VisualPinball.Unity.Playfield;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Editor
{
	/// <summary>Prefab mounting pivots follow the playfield, independently of colliders and the scene grid.</summary>
	[InitializeOnLoad]
	internal static class PlayfieldAssetPlacement
	{
		private sealed class Surface
		{
			public PlayfieldComponent Playfield;
			public Transform Transform;
			public Bounds Bounds;
		}

		private sealed class PreviewPart
		{
			public int PrefabIndex;
			public Mesh Mesh;
			public bool OwnsMesh;
			public Matrix4x4 RelativeMatrix;
			public Material[] Materials;
			public MaterialPropertyBlock[] Properties;
			public Vector3[] WireEdges;
		}

		private static GameObject[] _prefabs;
		private static readonly List<Surface> Surfaces = new();
		private static readonly List<PreviewPart> PreviewParts = new();
		private static Matrix4x4[] _matrices;
		private static Vector3[] _positions;
		private static Vector3[] _offsets;
		private static SceneView _view;
		private static Scene _previewScene;
		private static Surface _surface;
		private static Vector3 _hit;
		private static bool _valid;

		static PlayfieldAssetPlacement()
		{
#if UNITY_6000_5_OR_NEWER
			DragAndDrop.AddDropHandlerV2(OnDrop);
#else
			DragAndDrop.AddDropHandler(OnDrop);
#endif
			SceneView.beforeSceneGui += BeforeSceneGui;
			SceneView.duringSceneGui += DuringSceneGui;
			AssemblyReloadEvents.beforeAssemblyReload += Clear;
			EditorApplication.quitting += Clear;
			EditorApplication.playModeStateChanged += _ => Clear();
			EditorApplication.update += OnEditorUpdate;
		}

		private static DragAndDropVisualMode OnDrop(Object dropUpon, Vector3 worldPosition, Vector2 mousePosition, Transform parent, bool perform)
		{
			var view = SceneView.currentDrawingSceneView;
			if (!view || EditorApplication.isPlayingOrWillChangePlaymode || view.in2DMode || PrefabStageUtility.GetCurrentPrefabStage() != null) {
				Clear();
				return DragAndDropVisualMode.None;
			}

			var objects = DragAndDrop.objectReferences;
			if (objects.Length == 0 || objects.Any(obj => obj is not GameObject go || go.transform.parent != null || !PrefabUtility.IsPartOfPrefabAsset(go))) {
				Clear();
				return DragAndDropVisualMode.None;
			}

			if (_prefabs == null || !_prefabs.SequenceEqual(objects)) {
				Begin(objects.Cast<GameObject>().ToArray());
			}
			if (Surfaces.Count == 0) {
				Clear();
				return DragAndDropVisualMode.None;
			}

			_view = view;
			_valid = FindSurface(HandleUtility.GUIPointToWorldRay(mousePosition), out _surface, out _hit);
			if (_valid) {
				var root = _surface.Playfield.transform;
				var localHit = root.InverseTransformPoint(_hit);
				for (var i = 0; i < _prefabs.Length; i++) {
					_positions[i] = localHit + _offsets[i] + Vector3.up * _prefabs[i].transform.localPosition.y;
					_matrices[i] = root.localToWorldMatrix * Matrix4x4.TRS(_positions[i], _prefabs[i].transform.localRotation, _prefabs[i].transform.localScale);
					// A multiple selection must fit on the same playfield, rather than spilling into empty space.
					var anchor = root.TransformPoint(localHit + _offsets[i]);
					if (!Contains(_surface, anchor)) {
						_valid = false;
					}
				}
			}
			view.Repaint();
			if (!_valid) {
				return DragAndDropVisualMode.Rejected;
			}
			if (perform) {
				Place();
				Clear();
			}
			return DragAndDropVisualMode.Copy;
		}

		private static void Begin(GameObject[] prefabs)
		{
			Clear();
			_prefabs = prefabs;
			_positions = new Vector3[prefabs.Length];
			_matrices = new Matrix4x4[prefabs.Length];
			_offsets = new Vector3[prefabs.Length];
			var stage = StageUtility.GetCurrentStageHandle();
			foreach (var pf in Object.FindObjectsByType<PlayfieldComponent>(FindObjectsSortMode.None)) {
				if (!pf.enabled || StageUtility.GetStageHandle(pf.gameObject) != stage || SceneVisibilityManager.instance.IsHidden(pf.gameObject)) {
					continue;
				}
				var mesh = pf.GetComponent<MeshFilter>();
				if (!mesh || !mesh.sharedMesh) {
					mesh = pf.GetComponentsInChildren<PlayfieldMeshComponent>()
						.Where(component => component.GetComponentInParent<PlayfieldComponent>() == pf)
						.Select(component => component.GetComponent<MeshFilter>()).FirstOrDefault(filter => filter && filter.sharedMesh);
				}
				if (mesh && mesh.sharedMesh) {
					Surfaces.Add(new Surface { Playfield = pf, Transform = mesh.transform, Bounds = mesh.sharedMesh.bounds });
				} else {
					// A table can be edited with its playfield renderer absent. Its authored rectangle still defines placement.
					var min = new Vector3(Physics.ScaleToWorld(pf.Left), 0, -Physics.ScaleToWorld(pf.Bottom));
					var max = new Vector3(Physics.ScaleToWorld(pf.Right), 0, -Physics.ScaleToWorld(pf.Top));
					Surfaces.Add(new Surface { Playfield = pf, Transform = pf.transform, Bounds = new Bounds((min + max) / 2, max - min) });
				}
			}

			if (Surfaces.Count == 0) {
				return;
			}

			var nextRight = 0f;
			for (var i = 0; i < prefabs.Length; i++) {
				var prefab = prefabs[i];
				var instance = prefab;
				if (prefab.GetComponentsInChildren<MeshFilter>().Any(filter => !filter.sharedMesh) || prefab.GetComponentInChildren<SkinnedMeshRenderer>()) {
					// Missing generated geometry and skinned poses need an instance. Ordinary prefabs use their stored meshes directly.
					if (!_previewScene.IsValid()) {
						_previewScene = EditorSceneManager.NewPreviewScene();
					}
					instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, _previewScene);
					instance.hideFlags = HideFlags.HideAndDontSave;
				}
				var inverseRoot = instance.transform.worldToLocalMatrix;
				var bounds = new Bounds();
				var hasBounds = false;
				foreach (var renderer in instance.GetComponentsInChildren<Renderer>()) {
					if (!renderer.enabled) {
						continue;
					}
					Mesh mesh = null;
					var ownsMesh = false;
					if (renderer is MeshRenderer) {
						mesh = renderer.GetComponent<MeshFilter>()?.sharedMesh;
					} else if (renderer is SkinnedMeshRenderer skinned) {
						mesh = new Mesh { hideFlags = HideFlags.HideAndDontSave };
						skinned.BakeMesh(mesh);
						ownsMesh = true;
					}
					if (!mesh) {
						continue;
					}
					var materials = renderer.sharedMaterials;
					var properties = new MaterialPropertyBlock[materials.Length];
					for (var slot = 0; slot < materials.Length; slot++) {
						var block = new MaterialPropertyBlock();
						renderer.GetPropertyBlock(block, slot);
						if (block.isEmpty) {
							renderer.GetPropertyBlock(block);
						}
						properties[slot] = block;
					}
					PreviewParts.Add(new PreviewPart {
						PrefabIndex = i, Mesh = mesh, OwnsMesh = ownsMesh, Materials = materials,
						RelativeMatrix = inverseRoot * renderer.transform.localToWorldMatrix, Properties = properties, WireEdges = WireEdges(mesh)
					});
					var partMatrix = Matrix4x4.TRS(Vector3.zero, prefab.transform.localRotation, prefab.transform.localScale)
						* inverseRoot * renderer.transform.localToWorldMatrix;
					var meshBounds = mesh.bounds;
					var partBounds = new Bounds(partMatrix.MultiplyPoint3x4(meshBounds.center), Vector3.zero);
					for (var corner = 0; corner < 8; corner++) {
						var offset = Vector3.Scale(meshBounds.extents, new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
						partBounds.Encapsulate(partMatrix.MultiplyPoint3x4(meshBounds.center + offset));
					}
					if (hasBounds) {
						bounds.Encapsulate(partBounds);
					} else {
						bounds = partBounds;
						hasBounds = true;
					}
				}
				var offsetX = i == 0 ? 0 : nextRight + 0.005f - bounds.min.x;
				_offsets[i] = Vector3.right * offsetX;
				nextRight = offsetX + Mathf.Max(bounds.max.x, bounds.min.x + 0.01f);
			}
		}

		private static Vector3[] WireEdges(Mesh mesh)
		{
			// Large meshes still get their shaded preview and bounds, without a costly CPU wire overlay.
			if (mesh.vertexCount > 20000) {
				return null;
			}
			for (var submesh = 0; submesh < mesh.subMeshCount; submesh++) {
				if (mesh.GetTopology(submesh) != MeshTopology.Triangles) {
					return null;
				}
			}
			try {
				var vertices = mesh.vertices;
				var triangles = mesh.triangles;
				var edges = new HashSet<(int, int)>();
				var lines = new List<Vector3>();
				for (var i = 0; i < triangles.Length; i += 3) {
					for (var side = 0; side < 3; side++) {
						var a = triangles[i + side];
						var b = triangles[i + (side + 1) % 3];
						if (edges.Add(a < b ? (a, b) : (b, a))) {
							lines.Add(vertices[a]);
							lines.Add(vertices[b]);
						}
					}
				}
				return lines.ToArray();
			} catch (UnityException) {
				// A non-readable imported mesh can still be rendered and previewed with bounds.
				return null;
			}
		}

		private static bool FindSurface(Ray ray, out Surface surface, out Vector3 hit)
		{
			surface = null;
			hit = default;
			var nearest = float.PositiveInfinity;
			foreach (var candidate in Surfaces) {
				if (!candidate.Playfield || !candidate.Playfield.isActiveAndEnabled || !candidate.Transform) {
					continue;
				}
				if (TryHit(ray, candidate.Transform.localToWorldMatrix, candidate.Bounds, out var point)) {
					var distance = (point - ray.origin).sqrMagnitude;
					if (distance < nearest) {
						nearest = distance;
						surface = candidate;
						hit = point;
					}
				}
			}
			return surface != null;
		}

		// The top plane intentionally spans openings: inserts and kickers belong at the rim, not at the bottom of a hole.
		internal static bool TryHit(Ray ray, Matrix4x4 localToWorld, Bounds bounds, out Vector3 hit)
		{
			hit = default;
			var inverse = localToWorld.inverse;
			var origin = inverse.MultiplyPoint3x4(ray.origin);
			var direction = inverse.MultiplyVector(ray.direction);
			if (Mathf.Abs(direction.y) < 0.000001f) {
				return false;
			}
			var distance = (bounds.max.y - origin.y) / direction.y;
			if (distance < 0) {
				return false;
			}
			var point = origin + direction * distance;
			if (!Contains(bounds, point)) {
				return false;
			}
			hit = localToWorld.MultiplyPoint3x4(point);
			return true;
		}

		private static bool Contains(Surface surface, Vector3 point) => Contains(surface.Bounds, surface.Transform.InverseTransformPoint(point));
		private static bool Contains(Bounds bounds, Vector3 point) => point.x >= bounds.min.x && point.x <= bounds.max.x && point.z >= bounds.min.z && point.z <= bounds.max.z;

		private static void Place()
		{
			Undo.IncrementCurrentGroup();
			var group = Undo.GetCurrentGroup();
			Undo.SetCurrentGroupName("Place assets on playfield");
			var placed = new List<Object>();
			try {
				for (var i = 0; i < _prefabs.Length; i++) {
					var prefab = _prefabs[i];
					var parent = _surface.Playfield.transform;
					var name = GameObjectUtility.GetUniqueNameForSibling(parent, prefab.name);
					var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
					Undo.RegisterCreatedObjectUndo(go, "Place asset on playfield");
					Undo.RecordObject(go.transform, "Position asset on playfield");
					go.name = name;
					go.transform.localPosition = _positions[i];
					go.transform.localRotation = prefab.transform.localRotation;
					go.transform.localScale = prefab.transform.localScale;
					PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
					if (DragAndDrop.GetGenericData("assets") is HashSet<AssetResult> assets && assets.Any(result => result.Asset.Object == prefab && result.Asset.UnpackPrefab)) {
						PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
					}
					placed.Add(go);
				}
				Selection.objects = placed.ToArray();
				AssetBrowser.StopDraggingAssets();
			} finally {
				Undo.CollapseUndoOperations(group);
			}
		}

		private static void BeforeSceneGui(SceneView view)
		{
			if (Event.current.type == EventType.DragExited || Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape) {
				Clear();
				return;
			}
			if (view != _view || !_valid || Event.current.type != EventType.Repaint) {
				return;
			}
			// Draw the preview scene's meshes only in this Scene View; its lights and colliders never enter the user's scene.
			foreach (var part in PreviewParts) {
				for (var submesh = 0; submesh < part.Mesh.subMeshCount; submesh++) {
					if (part.Materials.Length > 0 && part.Materials[Mathf.Min(submesh, part.Materials.Length - 1)] is { } material) {
						Graphics.DrawMesh(part.Mesh, _matrices[part.PrefabIndex] * part.RelativeMatrix, material, 0, view.camera, submesh, part.Properties[Mathf.Min(submesh, part.Properties.Length - 1)], ShadowCastingMode.Off, false);
					}
				}
			}
		}

		private static void DuringSceneGui(SceneView view)
		{
			if (view != _view || _prefabs == null) {
				return;
			}
			if (_valid) {
				var previousDepth = Handles.zTest;
				Handles.zTest = CompareFunction.Always;
				foreach (var part in PreviewParts) {
					using (new Handles.DrawingScope(Color.cyan, _matrices[part.PrefabIndex] * part.RelativeMatrix)) {
						// Below-playfield parts must remain visible while hovering over solid wood.
						if (part.WireEdges != null) {
							Handles.DrawLines(part.WireEdges);
						} else {
							Handles.DrawWireCube(part.Mesh.bounds.center, part.Mesh.bounds.size);
						}
					}
				}
				using (new Handles.DrawingScope(Color.cyan)) {
					Handles.DrawWireDisc(_hit, _surface.Transform.up, HandleUtility.GetHandleSize(_hit) * 0.04f);
				}
				Handles.zTest = previousDepth;
			}
			Handles.BeginGUI();
			GUI.Label(new Rect(70, 12, 380, 25), _valid ? $"Place on {_surface.Playfield.name}" : "Move the cursor over the playfield", EditorStyles.helpBox);
			Handles.EndGUI();
		}

		private static void OnEditorUpdate()
		{
			if (_view && EditorWindow.mouseOverWindow != _view) {
				Clear();
			}
		}

		private static void Clear()
		{
			foreach (var part in PreviewParts) {
				if (part.OwnsMesh && part.Mesh) {
					Object.DestroyImmediate(part.Mesh);
				}
			}
			PreviewParts.Clear();
			if (_previewScene.IsValid()) {
				EditorSceneManager.ClosePreviewScene(_previewScene);
				_previewScene = default;
			}
			Surfaces.Clear();
			_prefabs = null;
			_surface = null;
			_valid = false;
			if (_view) {
				_view.Repaint();
				_view = null;
			}
		}
	}
}
