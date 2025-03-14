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

using UnityEngine;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Data that is saved in the .vpe file must implement this interface.
	/// </summary>
	public interface IPackable
	{
		/// <summary>
		/// Packs the component data into a byte array.
		///
		/// Returning null will not even create the component for the game object. For components
		/// with no data, return <see cref="PackageApi.Packer.Empty"/> instead.
		/// </summary>
		/// <returns>Component data in binary</returns>
		byte[] Pack();
		byte[] PackReferences(Transform root, PackagedRefs refs, PackagedFiles files);

		void Unpack(byte[] bytes);
		void UnpackReferences(byte[] bytes, Transform root, PackagedRefs refs, PackagedFiles files);
	}
}
