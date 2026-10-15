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

// ReSharper disable MemberCanBePrivate.Global

using NLog;
using UnityEngine;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	public struct CabinetScreenPackable
	{
		public string Role;
		public int MaterialIndex;
		public string TextureProperty;

		public static byte[] Pack(CabinetScreenComponent comp)
		{
			return PackageApi.Packer.Pack(new CabinetScreenPackable {
				Role = comp.RoleName,
				MaterialIndex = comp.MaterialIndex,
				TextureProperty = comp.TextureProperty,
			});
		}

		public static void Unpack(byte[] bytes, CabinetScreenComponent comp)
		{
			var data = PackageApi.Packer.Unpack<CabinetScreenPackable>(bytes);
			comp.SetRoleName(data.Role);
			comp.MaterialIndex = data.MaterialIndex;
			comp.TextureProperty = data.TextureProperty ?? string.Empty;
		}
	}

	/// <summary>
	/// The target renderer and the hide root, as node IDs. Renderers and plain GameObjects carry no packed type name, so
	/// they are referenced by their node only.
	/// </summary>
	public struct CabinetScreenReferencesPackable
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public string TargetNode;
		public string HideRootNode;

		public static byte[] Pack(CabinetScreenComponent comp, PackagedRefs refs)
		{
			return PackageApi.Packer.Pack(new CabinetScreenReferencesPackable {
				TargetNode = NodeId(comp.Target != null ? comp.Target.transform : null, refs),
				HideRootNode = NodeId(comp.HideRoot != null ? comp.HideRoot.transform : null, refs),
			});
		}

		public static void Unpack(byte[] bytes, CabinetScreenComponent comp, PackagedRefs refs)
		{
			var data = PackageApi.Packer.Unpack<CabinetScreenReferencesPackable>(bytes);
			var target = Node(data.TargetNode, refs);
			comp.Target = target != null ? target.GetComponent<Renderer>() : null;
			var hideRoot = Node(data.HideRootNode, refs);
			comp.HideRoot = hideRoot != null ? hideRoot.gameObject : null;
		}

		private static string NodeId(Transform transform, PackagedRefs refs)
		{
			if (transform == null) {
				return null;
			}
			var id = refs.GetNodeId(transform);
			if (id == null) {
				Logger.Warn("Cabinet screen reference to {Name} points to a non-exported object, writing a null reference.", transform.name);
			}
			return id;
		}

		private static Transform Node(string id, PackagedRefs refs) => string.IsNullOrEmpty(id) ? null : refs.GetNode(id);
	}
}
