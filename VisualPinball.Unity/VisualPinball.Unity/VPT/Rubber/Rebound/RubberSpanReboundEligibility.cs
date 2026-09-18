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

using System.Collections.Generic;
using VisualPinball.Engine.VPT.Rubber;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Version of the serialized deformation bake, in the runtime assembly so collider
	/// generation can decide staleness without the editor.
	/// </summary>
	/// <remarks>
	/// Raising either number makes every bake written by an older editor read stale, so
	/// a rubber falls back to the static response until it is re-baked.
	/// </remarks>
	public static class RubberDeformationBakeFormat
	{
		/// <summary>
		/// Version of the serialized bake layout. Version 2 adds the per-span
		/// <c>ReboundEligible</c> flag and the compliant-rebound material domain check.
		/// </summary>
		public const int BakeVersion = 2;

		/// <summary>Version of the material compiler, part of the bake hash.</summary>
		public const int CompilerVersion = 2;
	}

	/// <summary>
	/// The runtime side of the plan's section 2.5: which Physical rubbers and which of
	/// their baked free spans get the compliant rebound.
	/// </summary>
	/// <remarks>
	/// Nothing here recomputes a bake. Staleness is decided by the presence of a stored
	/// hash and the stored bake version alone; the kinematic flag, the guided path, the
	/// materials and every other bake input are already inputs of that hash, so a
	/// changed authoring state invalidates the bake in the editor and the runtime only
	/// has to notice the version.
	/// </remarks>
	internal static class RubberSpanReboundEligibility
	{
		/// <summary>
		/// Whether the rubber as a whole may use the compliant rebound, with the reason
		/// it may not.
		/// </summary>
		internal static bool TryValidate(RubberColliderComponent collider, out string reason)
		{
			reason = null;
			if (!collider) {
				reason = "there is no rubber collider component.";
				return false;
			}
			if (collider.Mode != RubberColliderMode.Physical) {
				reason = "the collider model is not Physical.";
				return false;
			}
			if (collider.IsKinematic) {
				reason = "the rubber is kinematic; the compliant rebound does not support moving rubbers.";
				return false;
			}
			if (!IsBakeCurrent(collider)) {
				reason = collider.DeformationBakeHash.Length == 0
					? "the compliant rebound has not been baked."
					: "the compliant rebound bake is stale.";
				return false;
			}
			if (collider.BakeOutcome != RubberDeformationBakeOutcome.Deformable) {
				reason = $"the bake outcome is {collider.BakeOutcome}: {collider.BakeReason}";
				return false;
			}
			if (!collider.CompiledDeformationMaterial.IsPopulated) {
				reason = "the compiled deformation material is missing.";
				return false;
			}
			return true;
		}

		/// <summary>
		/// Staleness without recomputing anything: a bake hash must be present and the
		/// stored layout version must be the one this build reads.
		/// </summary>
		internal static bool IsBakeCurrent(RubberColliderComponent collider)
			=> collider && collider.DeformationBakeHash.Length > 0
				&& collider.DeformationBakeVersion == RubberDeformationBakeFormat.BakeVersion;

		/// <summary>
		/// The baked spans that carry <c>ReboundEligible</c>, ordered by path element
		/// index, which is the order the dense span ids are assigned in.
		/// </summary>
		internal static List<RubberSpanDescriptor> EligibleDescriptors(
			RubberColliderComponent collider)
		{
			var eligible = new List<RubberSpanDescriptor>();
			if (!collider) {
				return eligible;
			}
			var descriptors = collider.SpanDescriptors;
			for (var i = 0; i < descriptors.Count; i++) {
				if (descriptors[i].ReboundEligible) {
					eligible.Add(descriptors[i]);
				}
			}
			eligible.Sort((left, right)
				=> left.PathElementIndex.CompareTo(right.PathElementIndex));
			return eligible;
		}
	}
}
