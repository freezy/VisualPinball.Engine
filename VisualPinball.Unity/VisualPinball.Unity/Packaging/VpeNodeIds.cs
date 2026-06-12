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

using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Stable node identity for .vpe packages.
	///
	/// At export, every packaged GameObject gets a GUID that is written into the corresponding
	/// glTF node's <c>extras.vpeId</c>. Everything else in the package (items, refs, mappings,
	/// renderer states, light profiles) references nodes by that id, so the binding survives
	/// node reordering, glTFast's synthetic helper nodes (<c>*_Orientation</c>) and importer-side
	/// hierarchy additions (multi-primitive mesh children).
	///
	/// The glTF node tree in table.glb is the single source of truth for the scene hierarchy;
	/// <c>extras</c> is glTF's standard extension point for application-specific per-node data.
	/// </summary>
	public static class VpeNodeIds
	{
		/// <summary>Key of the node id inside glTF <c>node.extras</c>.</summary>
		public const string ExtrasKey = "vpeId";

		public sealed class GltfNode
		{
			public int Index;
			public string Name;
			/// <summary>Name after glTFast's OriginalUnique sibling de-duplication (editor import).</summary>
			public string UniqueName;
			public int[] Children = Array.Empty<int>();
			public string VpeId;
		}

		public sealed class GltfNodeTree
		{
			public List<GltfNode> Nodes;
			public int[] SceneRootIndices = Array.Empty<int>();

			public GltfNode Root => SceneRootIndices.Length > 0 ? Nodes[SceneRootIndices[0]] : null;
		}

		public static string NewId() => Guid.NewGuid().ToString("N");

		#region Parsing

		/// <summary>
		/// Parses the node tree (names, children, vpe ids) of a GLB. Returns null when the data
		/// is not a GLB or carries no nodes.
		/// </summary>
		public static GltfNodeTree TryParse(byte[] glbData)
		{
			var root = GlbJsonUtil.TryParseRoot(glbData);
			if (root == null) {
				return null;
			}
			return TryParse(root);
		}

		public static GltfNodeTree TryParse(JObject root)
		{
			if (root["nodes"] is not JArray nodeArray || nodeArray.Count == 0) {
				return null;
			}

			var meshNames = ParseMeshNames(root);
			var nodes = new List<GltfNode>(nodeArray.Count);
			for (var i = 0; i < nodeArray.Count; i++) {
				var node = new GltfNode { Index = i };
				if (nodeArray[i] is JObject obj) {
					node.Name = obj.Value<string>("name");
					if (string.IsNullOrWhiteSpace(node.Name)) {
						// glTFast falls back to the mesh name for unnamed nodes; mirror that so
						// the computed unique names match the instantiated hierarchy.
						var meshIndex = obj.Value<int?>("mesh") ?? -1;
						if (meshIndex >= 0 && meshIndex < meshNames.Count) {
							node.Name = meshNames[meshIndex];
						}
					}
					if (obj["children"] is JArray children) {
						var childIndices = new int[children.Count];
						for (var c = 0; c < children.Count; c++) {
							childIndices[c] = children[c].Value<int>();
						}
						node.Children = childIndices;
					}
					node.VpeId = obj["extras"]?[ExtrasKey]?.Value<string>();
				}
				nodes.Add(node);
			}

			var sceneIndex = root.Value<int?>("scene") ?? 0;
			var tree = new GltfNodeTree { Nodes = nodes };
			if (root["scenes"] is JArray scenes && sceneIndex >= 0 && sceneIndex < scenes.Count
				&& scenes[sceneIndex] is JObject scene && scene["nodes"] is JArray sceneNodes) {
				var roots = new int[sceneNodes.Count];
				for (var i = 0; i < sceneNodes.Count; i++) {
					roots[i] = sceneNodes[i].Value<int>();
				}
				tree.SceneRootIndices = roots;
			}

			ComputeUniqueNames(tree);
			return tree;
		}

		private static List<string> ParseMeshNames(JObject root)
		{
			var names = new List<string>();
			if (root["meshes"] is JArray meshes) {
				foreach (var mesh in meshes) {
					names.Add((mesh as JObject)?.Value<string>("name"));
				}
			}
			return names;
		}

		// Reproduces glTFast's NameImportMethod.OriginalUnique de-duplication (see
		// GltfImport.CreateUniqueNames/GetUniqueNodeName): duplicate names within one sibling
		// group get "_0", "_1", ... suffixes. The editor import path instantiates with these
		// names, so the editor binder must compare against them, not the raw node names.
		private static void ComputeUniqueNames(GltfNodeTree tree)
		{
			foreach (var node in tree.Nodes) {
				node.UniqueName = string.IsNullOrWhiteSpace(node.Name) ? $"Node-{node.Index}" : node.Name;
			}

			var childNames = new HashSet<string>();
			foreach (var node in tree.Nodes) {
				if (node.Children.Length == 0) {
					continue;
				}
				childNames.Clear();
				foreach (var childIndex in node.Children) {
					tree.Nodes[childIndex].UniqueName = MakeUnique(tree.Nodes[childIndex].UniqueName, childNames);
				}
			}

			childNames.Clear();
			foreach (var rootIndex in tree.SceneRootIndices) {
				tree.Nodes[rootIndex].UniqueName = MakeUnique(tree.Nodes[rootIndex].UniqueName, childNames);
			}
		}

		private static string MakeUnique(string name, ISet<string> excludeNames)
		{
			if (!excludeNames.Contains(name)) {
				excludeNames.Add(name);
				return name;
			}
			var i = 0;
			string extName;
			do {
				extName = $"{name}_{i++}";
			} while (excludeNames.Contains(extName));
			excludeNames.Add(extName);
			return extName;
		}

		#endregion

		#region Export

		/// <summary>
		/// Assigns a fresh GUID to every transform that the package export walks (active
		/// hierarchy, same notion as the glTF export).
		/// </summary>
		public static Dictionary<Transform, string> AssignIds(Transform root)
		{
			var ids = new Dictionary<Transform, string>();
			foreach (var t in root.GetComponentsInChildren<Transform>(includeInactive: false)) {
				ids[t] = NewId();
			}
			return ids;
		}

		/// <summary>
		/// Writes the per-node ids into the GLB's <c>node.extras.vpeId</c> by walking the glTF
		/// node tree and the Unity hierarchy in parallel. glTF-side synthetic nodes that have no
		/// Unity counterpart (light/camera orientation helpers) are skipped. Throws when the two
		/// trees cannot be reconciled — a package with mis-bound ids must never be written.
		/// </summary>
		public static byte[] InjectIds(byte[] glbData, Transform unityRoot, IReadOnlyDictionary<Transform, string> idByTransform)
		{
			var root = GlbJsonUtil.TryParseRoot(glbData);
			if (root == null) {
				throw new InvalidOperationException("Cannot inject node ids: scene GLB could not be parsed.");
			}
			var tree = TryParse(root);
			if (tree?.Root == null) {
				throw new InvalidOperationException("Cannot inject node ids: scene GLB has no scene nodes.");
			}
			if (root["nodes"] is not JArray nodeArray) {
				throw new InvalidOperationException("Cannot inject node ids: scene GLB has no node array.");
			}

			if (tree.SceneRootIndices.Length != 1) {
				throw new InvalidOperationException(
					$"Cannot inject node ids: expected a single scene root node, found {tree.SceneRootIndices.Length}.");
			}

			var boundCount = 0;
			BindExport(tree, tree.Root, unityRoot, idByTransform, nodeArray, ref boundCount);

			if (boundCount != idByTransform.Count) {
				throw new InvalidOperationException(
					$"Node id injection bound {boundCount} of {idByTransform.Count} exported transforms. " +
					"The glTF node tree does not match the exported hierarchy.");
			}

			return GlbJsonUtil.ReplaceJsonChunk(glbData, root);
		}

		private static void BindExport(
			GltfNodeTree tree,
			GltfNode gltfNode,
			Transform transform,
			IReadOnlyDictionary<Transform, string> idByTransform,
			JArray nodeArray,
			ref int boundCount)
		{
			if (!string.Equals(gltfNode.Name, transform.name, StringComparison.Ordinal)) {
				throw new InvalidOperationException(
					$"Node id injection failed: glTF node '{gltfNode.Name}' (index {gltfNode.Index}) does not match " +
					$"GameObject '{transform.name}'.");
			}

			if (idByTransform.TryGetValue(transform, out var id)) {
				var nodeObj = (JObject)nodeArray[gltfNode.Index];
				var extras = nodeObj["extras"] as JObject ?? new JObject();
				extras[ExtrasKey] = id;
				nodeObj["extras"] = extras;
				boundCount++;
			}

			// Unity children that were exported (active), in order.
			var unityChildren = new List<Transform>();
			for (var i = 0; i < transform.childCount; i++) {
				var child = transform.GetChild(i);
				if (child.gameObject.activeInHierarchy) {
					unityChildren.Add(child);
				}
			}

			// Two-pointer walk: every exported Unity child must match a glTF child in order;
			// glTF children without a Unity counterpart (synthetic orientation helpers, appended
			// by the exporter) are skipped.
			var g = 0;
			foreach (var unityChild in unityChildren) {
				var bound = false;
				while (g < gltfNode.Children.Length) {
					var gltfChild = tree.Nodes[gltfNode.Children[g]];
					g++;
					if (string.Equals(gltfChild.Name, unityChild.name, StringComparison.Ordinal)) {
						BindExport(tree, gltfChild, unityChild, idByTransform, nodeArray, ref boundCount);
						bound = true;
						break;
					}
				}
				if (!bound) {
					throw new InvalidOperationException(
						$"Node id injection failed: no glTF node found for GameObject '{unityChild.name}' " +
						$"(child of '{transform.name}').");
				}
			}
		}

		#endregion

		#region Import

		/// <summary>
		/// Builds the id → transform map from an exact glTF-node-index → GameObject mapping, as
		/// produced by hooking GLTFast.GameObjectInstantiator.NodeCreated at runtime.
		/// </summary>
		public static Dictionary<string, Transform> MapInstantiatedNodes(
			GltfNodeTree tree,
			IReadOnlyDictionary<uint, GameObject> gameObjectsByNodeIndex)
		{
			var map = new Dictionary<string, Transform>(StringComparer.Ordinal);
			foreach (var node in tree.Nodes) {
				if (string.IsNullOrEmpty(node.VpeId)) {
					continue;
				}
				if (gameObjectsByNodeIndex.TryGetValue((uint)node.Index, out var go) && go) {
					map[node.VpeId] = go.transform;
				}
			}
			return map;
		}

		/// <summary>
		/// Binds the glTF node tree against an already-instantiated hierarchy by structure and
		/// (de-duplicated) name — used by the editor import, where the prefab comes out of the
		/// asset pipeline and no node-index hook exists. Instantiated-side extra children
		/// (multi-primitive mesh objects, which glTFast appends after all hierarchy children)
		/// are skipped. Throws when a node carrying a vpe id cannot be bound.
		/// </summary>
		public static Dictionary<string, Transform> BindInstantiated(GltfNodeTree tree, Transform instantiatedRoot)
		{
			if (tree?.Root == null) {
				throw new InvalidOperationException("Cannot bind node ids: glTF tree has no scene root.");
			}

			var anchor = FindAnchor(tree.Root, instantiatedRoot);
			if (anchor == null) {
				throw new InvalidOperationException(
					$"Cannot bind node ids: no instantiated transform matches scene root node '{tree.Root.UniqueName}'.");
			}

			var map = new Dictionary<string, Transform>(StringComparer.Ordinal);
			BindImport(tree, tree.Root, anchor, map);
			return map;
		}

		// The asset importer may wrap the scene root in a file-level prefab root; accept the
		// given transform itself or a (grand-)child whose name matches the scene root node.
		private static Transform FindAnchor(GltfNode sceneRoot, Transform instantiatedRoot)
		{
			if (string.Equals(instantiatedRoot.name, sceneRoot.UniqueName, StringComparison.Ordinal)) {
				return instantiatedRoot;
			}
			for (var i = 0; i < instantiatedRoot.childCount; i++) {
				var child = instantiatedRoot.GetChild(i);
				if (string.Equals(child.name, sceneRoot.UniqueName, StringComparison.Ordinal)) {
					return child;
				}
				for (var j = 0; j < child.childCount; j++) {
					var grandChild = child.GetChild(j);
					if (string.Equals(grandChild.name, sceneRoot.UniqueName, StringComparison.Ordinal)) {
						return grandChild;
					}
				}
			}
			return null;
		}

		private static void BindImport(GltfNodeTree tree, GltfNode gltfNode, Transform transform, Dictionary<string, Transform> map)
		{
			if (!string.IsNullOrEmpty(gltfNode.VpeId)) {
				map[gltfNode.VpeId] = transform;
			}

			// Hierarchy children are instantiated first and in glTF children order; primitive
			// children are appended afterwards (second instantiation pass). The two-pointer walk
			// therefore matches hierarchy children before any same-named primitive extra.
			var u = 0;
			foreach (var childIndex in gltfNode.Children) {
				var gltfChild = tree.Nodes[childIndex];
				Transform boundChild = null;
				var scanStart = u;
				while (u < transform.childCount) {
					var unityChild = transform.GetChild(u);
					u++;
					if (string.Equals(unityChild.name, gltfChild.UniqueName, StringComparison.Ordinal)) {
						boundChild = unityChild;
						break;
					}
				}
				if (boundChild == null) {
					if (HasAnyVpeId(tree, gltfChild)) {
						throw new InvalidOperationException(
							$"Cannot bind node ids: no instantiated child of '{transform.name}' matches " +
							$"glTF node '{gltfChild.UniqueName}' (index {gltfChild.Index}).");
					}
					// Id-free glTF node without an instantiated counterpart: don't consume the
					// sibling window, later siblings may still match earlier children.
					u = scanStart;
					continue;
				}
				BindImport(tree, gltfChild, boundChild, map);
			}
		}

		private static bool HasAnyVpeId(GltfNodeTree tree, GltfNode node)
		{
			if (!string.IsNullOrEmpty(node.VpeId)) {
				return true;
			}
			foreach (var childIndex in node.Children) {
				if (HasAnyVpeId(tree, tree.Nodes[childIndex])) {
					return true;
				}
			}
			return false;
		}

		#endregion
	}
}
