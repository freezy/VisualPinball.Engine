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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace VisualPinball.Unity
{
	public interface IReferencedDependency
	{
		void RegisterTypes(PackagedRefs refs);
	}

	public class PackagedRefs
	{
		private readonly Dictionary<Type, string> _typeToName = new();
		private readonly Dictionary<string, Type> _nameToType = new();
		private readonly List<Type> _nativeTypes = new();

		// Node identity: references are written and resolved by stable node id (the glTF
		// extras.vpeId of table.glb). See VpeNodeIds.
		private IReadOnlyDictionary<Transform, string> _nodeIdByTransform;
		private IReadOnlyDictionary<string, Transform> _transformByNodeId;

		// Scanning every loaded assembly for PackAsAttribute takes hundreds of milliseconds and its
		// result only changes on domain reload, so it is computed once and shared. WarmUpTypeScan is
		// safe to call from a worker thread to overlap the scan with other import work.
		private static readonly object TypeScanLock = new();
		private static List<(Type Type, string Name)> _packAsTypes;
		private static List<Type> _referencedDependencyTypes;

		public static void WarmUpTypeScan()
		{
			lock (TypeScanLock) {
				if (_packAsTypes != null) {
					return;
				}

				var packAsTypes = new List<(Type, string)>();
				var dependencyTypes = new List<Type>();
				var referencedDependencyType = typeof(IReferencedDependency);
				var assemblies = AppDomain.CurrentDomain.GetAssemblies();
				foreach (var assembly in assemblies) {
					foreach (var type in assembly.GetTypes()) {
						// Look for the PackAsAttribute on the class
						var attribute = type.GetCustomAttribute<PackAsAttribute>(inherit: false);
						if (attribute != null) {
							packAsTypes.Add((type, attribute.Name));
						}

						if (referencedDependencyType.IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract) {
							dependencyTypes.Add(type);
						}
					}
				}

				_referencedDependencyTypes = dependencyTypes;
				_packAsTypes = packAsTypes;
			}
		}

		public PackagedRefs(Transform tableRoot)
		{
			WarmUpTypeScan();
			foreach (var (type, name) in _packAsTypes) {
				Add(type, name);
			}
			foreach (var dependencyType in _referencedDependencyTypes) {
				(Activator.CreateInstance(dependencyType) as IReferencedDependency)!.RegisterTypes(this);
			}
			// native Unity types that components reference directly
			Add(typeof(Transform), "Transform");
			// add third parties
		}

		public void Add(Type type, string name)
		{
			_nameToType.Add(name, type);
			_typeToName.Add(type, name);
			if (!type.IsAssignableFrom(typeof(IPackable))) {
				_nativeTypes.Add(type);
			}
		}

		public Type GetType(string name)
		{
			if (!_nameToType.TryGetValue(name, out var type)) {
				Debug.LogError("No type found for name: " + name);
				return null;
			}
			return type;
		}

		/// <summary>Non-logging type lookup, for callers that handle unknown types themselves.</summary>
		public bool TryGetType(string name, out Type type) => _nameToType.TryGetValue(name, out type);

		public string GetName(Type type)
		{
			if (!_typeToName.TryGetValue(type, out var name)) {
				Debug.LogError($"No name found for type: {type.FullName}. Missing PackedAs attribute?");
				return null;
			}
			return name;
		}

		public bool HasType(Type t) => _typeToName.ContainsKey(t);

		/// <summary>
		/// Switches reference writing to stable node ids (format v2). The map must cover every
		/// exported transform.
		/// </summary>
		public void SetNodeIdsForWrite(IReadOnlyDictionary<Transform, string> nodeIdByTransform)
		{
			_nodeIdByTransform = nodeIdByTransform;
		}

		/// <summary>
		/// Switches reference resolution to stable node ids (format v2), as bound from the
		/// imported scene.
		/// </summary>
		public void SetNodeIdsForRead(IReadOnlyDictionary<string, Transform> transformByNodeId)
		{
			_transformByNodeId = transformByNodeId;
		}

		/// <summary>Node id of a transform when writing a v2 package, null otherwise.</summary>
		public string GetNodeId(Transform transform)
		{
			return transform != null && _nodeIdByTransform != null && _nodeIdByTransform.TryGetValue(transform, out var id)
				? id
				: null;
		}

		/// <summary>Transform for a node id when reading a v2 package, null otherwise.</summary>
		public Transform GetNode(string nodeId)
		{
			return !string.IsNullOrEmpty(nodeId) && _transformByNodeId != null && _transformByNodeId.TryGetValue(nodeId, out var t)
				? t
				: null;
		}

		public IEnumerable<ReferencePackable> PackReferences<T>(IEnumerable<T> comps) where T : Component
			=> comps.Select(PackReference);

		public ReferencePackable PackReference<T>(T comp) where T : Component
		{
			if (comp == null) {
				return new ReferencePackable(null, null);
			}
			var typeName = GetName(comp.GetType());
			var id = GetNodeId(comp.transform);
			if (id == null) {
				// Only exported (active) nodes carry ids; a reference to anything else cannot be
				// restored and is written as a null reference.
				Debug.LogWarning($"Reference to {typeName} on '{comp.name}' points to a non-exported object; writing null reference.");
				return new ReferencePackable(null, typeName);
			}
			return new ReferencePackable(id, typeName);
		}

		public T Resolve<T>(ReferencePackable packedRef) where T: class
		{
			if (string.IsNullOrEmpty(packedRef.Id)) {
				return null;
			}
			var transform = GetNode(packedRef.Id);
			if (transform == null) {
				Debug.LogError($"Error resolving reference {packedRef.Type}@{packedRef.Id}: No node with this id.");
				return null;
			}
			var type = GetType(packedRef.Type);
			if (type == null) {
				Debug.LogError($"Error resolving type name {packedRef.Type} to type. PackAs[] attribute missing?");
				return null;
			}
			var component = transform.gameObject.GetComponent(type);

			if (component == null) {
				Debug.LogError($"Error resolving reference {packedRef.Type}@{packedRef.Id}: No component of type {type.FullName} on game object {transform.name}");
			}

			if (component is T compT) {
				return compT;
			}

			Debug.LogError($"Error resolving reference {packedRef.Type}@{packedRef.Id}: Component on {transform.name} required to be of type {typeof(T).FullName}, but is {component.GetType().FullName}.");
			return null;
		}

		public IEnumerable<T> Resolve<T>(IEnumerable<ReferencePackable> packedRefs) where T : class
			=> packedRefs.Select(Resolve<T>).Where(c => c != null);

		public IEnumerable<T> Resolve<T, TI>(IEnumerable<ReferencePackable> packedRefs) where T : class
			=> packedRefs.Select(Resolve<T, TI>).Where(c => c != null);

		public T Resolve<T, TI>(ReferencePackable packedRef) where T : class
		{
			var component = Resolve<T>(packedRef);
			if (component == null) {
				return null;
			}
			if (component is TI) {
				return component;
			}
			Debug.LogError($"Error resolving reference {packedRef.Type}@{packedRef.Id}: Component does not inherit {typeof(TI).FullName}.");
			return null;
		}
	}
}
