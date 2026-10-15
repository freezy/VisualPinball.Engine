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

using UnityEngine;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Marks the table's cabinet. The component carries no behaviour - it only lets tooling
	/// locate the cabinet. The package screenshot generator hides every GameObject tagged
	/// with this component (remembering and restoring its active state) so the cabinet does
	/// not appear in the top-down playfield screenshots, and the player's cabinet view hides
	/// it, which is why it is packaged too.
	/// </summary>
	[AddComponentMenu("Pinball/Cabinet")]
	[DisallowMultipleComponent]
	[PackAs("Cabinet")]
	public class CabinetComponent : MonoBehaviour, IPackable
	{
		#region Packaging

		public byte[] Pack() => PackageApi.Packer.Empty;

		public byte[] PackReferences(Transform root, PackagedRefs refs, PackagedFiles files) => null;

		public void Unpack(byte[] bytes) { }

		public void UnpackReferences(byte[] bytes, Transform root, PackagedRefs refs, PackagedFiles files) { }

		#endregion
	}
}
