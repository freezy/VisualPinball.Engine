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
using System.Reflection;
using UnityEngine;

namespace VisualPinball.Unity
{
	public class PackagedRefs
	{
		private readonly Dictionary<Type, string> _typeToName = new();
		private readonly Dictionary<string, Type> _nameToType = new();

		private readonly Transform _tableRoot;

		public PackagedRefs(Transform tableRoot)
		{
			_tableRoot = tableRoot;
			var assembly = Assembly.GetExecutingAssembly();
			foreach (var type in assembly.GetTypes()) {
				// Look for the PackAsAttribute on the class
				var attribute = type.GetCustomAttribute<PackAsAttribute>(inherit: false);
				if (attribute != null) {
					_nameToType.Add(attribute.Name, type);
					_typeToName.Add(type, attribute.Name);
				}
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
