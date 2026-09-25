// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
// SPDX-License-Identifier: GPL-3.0-or-later

namespace VisualPinball.Unity
{
	/// <summary>
	/// A thin line engraved into an insert's top surface: a trough with a
	/// rounded floor running along a path inside the outline, the way a flash
	/// has a lightning bolt drawn into it a little way in from its edge.
	/// </summary>
	/// <remarks>
	/// The path is an outline of its own, the insert's outline eroded by the
	/// line's inset: convex corners sharpen or vanish, concave ones widen, so
	/// the line follows the shape the way an engraver's stylus would.
	/// </remarks>
	public sealed class InsertGroove
	{
		/// <summary>The line's middle, as an outline in VPX units.</summary>
		public InsertOutline Outline;

		/// <summary>How wide the trough is across, in VPX units.</summary>
		public float Width;

		/// <summary>How deep its middle goes below the surface, in VPX units.</summary>
		public float Depth;

		/// <summary>How many steps each side of the trough's round is drawn with, edge to middle.</summary>
		public int Segments;
	}
}
