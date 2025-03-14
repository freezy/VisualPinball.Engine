﻿// Visual Pinball Engine
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

		private readonly Transform _tableRoot;

		public PackagedRefs(Transform tableRoot)
		{
			_tableRoot = tableRoot;
			var referencedDependencyType = typeof(IReferencedDependency);
			var assemblies = AppDomain.CurrentDomain.GetAssemblies();
			foreach (var assembly in assemblies) {
				foreach (var type in assembly.GetTypes()) {
					// Look for the PackAsAttribute on the class
					var attribute = type.GetCustomAttribute<PackAsAttribute>(inherit: false);
					if (attribute != null) {
						Add(type, attribute.Name);
					}

					if (referencedDependencyType.IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract) {
						(Activator.CreateInstance(type) as IReferencedDependency)!.RegisterTypes(this);
					}
				}
			}
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

		public string GetName(Type type)
		{
			if (!_typeToName.TryGetValue(type, out var name)) {
				Debug.LogError($"No name found for type: {type.FullName}. Missing PackedAs attribute?");
				return null;
			}
			return name;
		}

		public bool HasType(Type t) => _typeToName.ContainsKey(t);

		public IEnumerable<ReferencePackable> PackReferences<T>(IEnumerable<T> comps) where T : Component
			=> comps.Select(PackReference);

		public ReferencePackable PackReference<T>(T comp) where T : Component
			=> comp != null
				? new ReferencePackable(comp.transform.GetPath(_tableRoot), GetName(comp.GetType()))
				: new ReferencePackable(null, null);

		public T Resolve<T>(ReferencePackable packedRef) where T: class
		{
			var transform = _tableRoot.FindByPath(packedRef.Path);
			if (transform == null) {
				Debug.LogError($"Error resolving reference {packedRef.Type}@{packedRef.Path}: No object found at path.");
				return null;
			}
			var type = GetType(packedRef.Type);
			if (type == null) {
				Debug.LogError($"Error resolving type name {packedRef.Type} to type. PackAs[] attribute missing?");
				return null;
			}
			var component = transform.gameObject.GetComponent(type);

			if (component == null) {
				Debug.LogError($"Error resolving reference {packedRef.Type}@{packedRef.Path}: No component of type {type.FullName} on game object {transform.name}");
			}

			if (component is T compT) {
				return compT;
			}

			Debug.LogError($"Error resolving reference {packedRef.Type}@{packedRef.Path}: Component on {transform.name} required to be of type {typeof(T).FullName}, but is {component.GetType().FullName}.");
			return null;
		}

		public IEnumerable<T> Resolve<T>(IEnumerable<ReferencePackable> packedRefs) where T : class
			=> packedRefs.Select(Resolve<T>).Where(c => c != null);

		public IEnumerable<T> Resolve<T, TI>(IEnumerable<ReferencePackable> packedRefs) where T : class
			=> packedRefs.Select(Resolve<T, TI>).Where(c => c != null);

		public T Resolve<T, TI>(ReferencePackable packedRef) where T : class
		{
			var component = Resolve<T>(packedRef);
			if (component is TI) {
				return component;
			}
			Debug.LogError($"Error resolving reference {packedRef.Type}@{packedRef.Path}: Component does not inherit {typeof(TI).FullName}.");
			return null;
		}
	}
}
