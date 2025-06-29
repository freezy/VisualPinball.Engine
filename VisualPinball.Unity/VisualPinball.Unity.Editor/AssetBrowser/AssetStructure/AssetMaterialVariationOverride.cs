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

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// This class represents an Override with a link to its Variation.
	///
	/// (We used tuples for this in the past which turned out to be not very readable.)
	/// </summary>
	public class AssetMaterialVariationOverride : IEquatable<AssetMaterialVariationOverride>
	{
		public readonly AssetMaterialVariation Variation;
		public readonly AssetMaterialOverride Override;
		public bool IsDecal => Variation is { IsDecal: true };

		public AssetMaterialVariationOverride(AssetMaterialVariation variation, AssetMaterialOverride @override)
		{
			Variation = variation;
			Override = @override;
		}

		public override string ToString() => $"{Variation?.Name ?? "<null>"}: {Override?.Name ?? "<null>"}";

		#region IEquatable

		public bool Equals(AssetMaterialVariationOverride other) => Equals(Variation, other?.Variation) && Override?.Id == other?.Override?.Id;

		public override bool Equals(object obj) => obj is AssetMaterialVariationOverride other && Equals(other);

		public override int GetHashCode() => HashCode.Combine(Variation, Override?.Id);

		#endregion
	}
}
