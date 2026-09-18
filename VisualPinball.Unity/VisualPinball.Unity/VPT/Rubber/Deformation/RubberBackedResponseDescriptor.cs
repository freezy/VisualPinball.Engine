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

using System;

namespace VisualPinball.Unity
{
	/// <summary>
	/// The rigid contact response of one guide binding of a Physical rubber: the
	/// supported arc that rides on the guide, and the anchor at either end of the
	/// adjacent free spans.
	/// </summary>
	/// <remarks>
	/// <para>
	/// One descriptor is baked per guide binding referenced by the baked path,
	/// whether or not the hull solver emitted a supported arc for it, so the
	/// fully-rounded case, where two free spans share an anchor with nothing between
	/// them, still carries a material for that anchor.
	/// </para>
	/// <para>
	/// The two lookup tables mirror the layout the runtime dictionaries expect. The
	/// engine stores per-item velocity response as
	/// <c>NativeParallelHashMap&lt;int, FixedList512Bytes&lt;float&gt;&gt;</c>, filled by
	/// <c>ColliderComponent.GetPhysicsMaterialData</c> from
	/// <c>PhysicsMaterialAsset.GetElasticityOverVelocityLUT</c> and
	/// <c>GetFrictionOverVelocityLUT</c>. Both produce 127 samples; the elasticity
	/// table samples the curve at <c>i / 2</c> velocity units (0 to 63 in steps of
	/// 0.5) and the friction table at <c>i</c> velocity units (0 to 126 in steps of
	/// 1). The descriptor stores the same 127 values as a managed array, in the same
	/// order, so registration can copy them straight into a
	/// <c>FixedList512Bytes&lt;float&gt;</c> (a 512-byte fixed list holds at most 127
	/// floats plus its length, which is why the engine stops at 127).
	/// </para>
	/// </remarks>
	[Serializable]
	public struct RubberBackedResponseDescriptor
	{
		/// <summary>Number of samples in each velocity lookup table.</summary>
		public const int VelocityLutSampleCount = 127;

		/// <summary>Index into <c>RubberComponent.GuideBindings</c>.</summary>
		public int BindingIndex;

		/// <summary>
		/// Index of the supported arc in <c>RubberComponent.BakedPath</c>, or
		/// <c>-1</c> when the hull solver omitted a zero-length arc for this binding.
		/// </summary>
		public int PathElementIndex;

		/// <summary>
		/// The resolved contact material, exactly as <c>ColliderComponent</c> resolves
		/// it today: the assigned physics material asset when the collider does not
		/// overwrite physics, the collider's own scalars otherwise.
		/// </summary>
		public PhysicsMaterialData Material;

		/// <summary>
		/// Elasticity over velocity samples when
		/// <c>Material.UseElasticityOverVelocity</c> is set, otherwise empty.
		/// </summary>
		public float[] ElasticityOverVelocityLut;

		/// <summary>
		/// Friction over velocity samples when
		/// <c>Material.UseFrictionOverVelocity</c> is set, otherwise empty.
		/// </summary>
		public float[] FrictionOverVelocityLut;

		/// <summary>
		/// The guide elasticity scale in effect for this binding: the rubber's guide
		/// elasticity scale while its guide-distance response profile is active, one
		/// otherwise.
		/// </summary>
		public float ElasticityScale;
	}
}
