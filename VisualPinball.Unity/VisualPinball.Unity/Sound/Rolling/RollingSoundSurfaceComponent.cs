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

// ReSharper disable InconsistentNaming

using System;
using UnityEngine;

namespace VisualPinball.Unity
{
	public enum RollingSurfaceMode { Inherit, Override, Silent }

	/// <summary>Optional item-level acoustic override. Does not modify physical coefficients.</summary>
	[DisallowMultipleComponent, PackAs("RollingSoundSurface")]
	public sealed class RollingSoundSurfaceComponent : MonoBehaviour, IPackable
	{
		public RollingSurfaceMode Mode;
		public RollingSoundMaterialAsset Material;
		[Tooltip("Use this item's local support direction instead of table gravity, for wall or inverted mechanisms.")]
		public bool UseLocalSupport;
		public Vector3 LocalSupportAcceleration = Vector3.down;

		[Serializable] private struct Data { public RollingSurfaceMode Mode; public bool Local; public float X, Y, Z; }
		public byte[] Pack() => PackageApi.Packer.Pack(new Data { Mode = Mode, Local = UseLocalSupport, X = LocalSupportAcceleration.x, Y = LocalSupportAcceleration.y, Z = LocalSupportAcceleration.z });
		public void Unpack(byte[] bytes) { var data = PackageApi.Packer.Unpack<Data>(bytes); Mode = data.Mode; UseLocalSupport = data.Local; LocalSupportAcceleration = new Vector3(data.X, data.Y, data.Z); }
		public byte[] PackReferences(Transform root, PackagedRefs refs, PackagedFiles files) => PackageApi.Packer.Pack(files.Rolling.AddSurface(Material));
		public void UnpackReferences(byte[] bytes, Transform root, PackagedRefs refs, PackagedFiles files) => Material = files.Rolling.Surface(PackageApi.Packer.Unpack<int>(bytes));
	}
}
