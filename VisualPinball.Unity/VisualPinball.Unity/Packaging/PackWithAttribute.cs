// Visual Pinball Engine
// Copyright (C) 2025 freezy and VPE Team
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
using System.Reflection;
using UnityEngine;

namespace VisualPinball.Unity
{
	/// <summary>
	/// This is used for packing scriptable objects that reference files (and in the future, other scriptable objects).
	///
	/// It allows specifying a packer class that is instantiated during packaging and unpacking
	/// and that handles these references correctly. It's currently not used for anything else, such
	/// as components. (We already have full control over these, but it might save us from IPackable.
	/// To be followed up).
	/// </summary>
	[AttributeUsage(AttributeTargets.Class, Inherited = false)]
	public class PackWithAttribute : Attribute
	{
		public Type PackerType { get; }

		public PackWithAttribute(Type packerType)
		{
			PackerType = packerType;
		}
	}

	public interface IPacker<in T>
	{
		MetaPackable Pack(int instanceId, T obj, PackagedFiles files);
		MetaPackable Unpack(byte[] bytes, T obj, PackagedFiles files);
	}

	public static class PackerFactory
	{
		public static IPacker<T> GetPacker<T>() => GetPacker<T>(typeof(T));
		public static IPacker<ScriptableObject> GetPacker(Type t) => GetPacker<ScriptableObject>(t);

		private static IPacker<T> GetPacker<T>(Type t)
		{
			// look for the PackWithAttribute on T
			var attr = t.GetCustomAttribute<PackWithAttribute>();
			if (attr == null) {
				return null;
			}

			// check whether the PackerType implements IPacker<T>
			var packerType = attr.PackerType;
			if (!typeof(IPacker<T>).IsAssignableFrom(packerType)) {
				throw new InvalidOperationException($"Type {packerType.FullName} does not implement IPacker<{typeof(T).Name}>.");
			}

			// construct an instance of the PackerType
			var packer = (IPacker<T>)Activator.CreateInstance(packerType);
			return packer;
		}
	}
}
