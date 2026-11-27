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
using System.Linq;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// The controls of the Insert Hybrid shader's opt-in Traced mode, in the
	/// order the inspectors show them, with what each one does. HDRP and URP
	/// draw them with their own controls; the words live here once.
	/// </summary>
	public static class InsertTracedFields
	{
		public const string Keyword = "_INSERT_TRACED";
		public const string Title = "Traced Mode";

		public static readonly GUIContent KeywordLabel = new("Traced Mode",
			"Lights the body from a traced render instead of the optical response map. Off, the material is the live one, unchanged.");

		public const string Note = "The body is shaded as a flat card at the top: the cover's refraction is undone, so nothing of the traced light moves with the mesh's facets. Colour, brightness and the lamp drive stay the Lamp section's.";

		public readonly struct Entry
		{
			public readonly string Name;
			public readonly GUIContent Label;
			public readonly bool IsTexture;

			public Entry(string name, string label, string tooltip, bool isTexture = false)
			{
				Name = name;
				Label = new GUIContent(label, tooltip);
				IsTexture = isTexture;
			}
		}

		public readonly struct Section
		{
			public readonly string Title;
			public readonly Entry[] Entries;

			public Section(string title, params Entry[] entries)
			{
				Title = title;
				Entries = entries;
			}
		}

		public static readonly Section[] Sections = {
			new("Bake",
				new Entry("_TracedLight", "Traced Light", "The offline render of the lit insert: what the eye sees through each point of the top from four elevations, one per channel (Elevations below).", true),
				new Entry("_TracedFacets", "Traced Facets", "The facet normals of the same render, X and Z in red and green, the faceted floor in alpha. The bulb and the glints refract through them.", true),
				new Entry("_TracedFrame", "Frame", "Where the bake's square sits in insert space: its centre X and Z in metres, its turn about Y in degrees, its width in metres."),
				new Entry("_TracedAngles", "Elevations (deg)", "The view elevations the four channels of the traced light were rendered from, off vertical."),
				new Entry("_TracedGeometry", "Depths (mm)", "X: the cover's slab thickness, whose refraction the card undoes. Y: how deep the facets sit under the top. Z: how deep the glow's parallax reaches. W: how far in from the rim the traced light fades out.")),
			new("Look",
				new Entry("_TracedGlow", "Glow", "Strength of the traced glow, in units of Baked Scatter: one is as bright as the live glow would be."),
				new Entry("_TracedGlowSpread", "Glow Spread", "One is the render as baked. More lifts the dim reaches toward the bright core, so the glow fills the insert instead of pooling over the bulb; less tightens it."),
				new Entry("_TracedLiveMix", "Live Glow Mix", "How much of the live soft and edge glow comes back in place of the traced one."),
				new Entry("_TracedGlint", "Glints", "Strength of the facets' highlights. A wide highlight is a dim one, so this sets how bright they are whatever their shape."),
				new Entry("_TracedGlintParams", "Glint Shape", "X: facet tilt in degrees. Y: angle sensitivity, how small a turn of the eye moves a highlight across a facet (more is tighter and sharper). Z: number of highlights. W: colour fringe in degrees."),
				new Entry("_TracedGlintRot", "Glint Turn (deg)", "Turns the highlights about the insert."),
				new Entry("_TracedGlintOrganic", "Glint Organic", "X: how many degrees the moulding's facets waver, so glints slide instead of switching whole facets. Y: how far each insert's highlights turn either way, from where it sits, so a row of the same insert doesn't flash in step. Z: the waviness' wavelength in millimetres.")),
			new("Lamp Off",
				new Entry("_TracedEnv", "Lamp-Off Env", "The same render with the lamp off under the room: four elevations in a 2x2 atlas (15, 45 / 60, 70 degrees), multiplied by the body mask.", true),
				new Entry("_TracedEnvGain", "Lamp-Off Look", "How far the traced lamp-off render takes the place of the live unlit body. Zero keeps the live body, one is the render as baked. The lamp's glow adds over it. Players dim or raise all of them together with the room brightness."),
				new Entry("_TracedEnvVary", "Lamp-Off Vary", "Each insert reads the lamp-off render a little differently, from where it sits: X degrees of elevation, Y brightness (a fraction either way), Z a refraction wobble in texels. Zero is the render exactly, the same on every insert."),
				new Entry("_TracedOffGlint", "Lamp-Off Glints", "The room glinting in the facets with the lamp off, as strong as the room light the render shows there, with the glints' own shape and per-insert variation.")),
			new("Bulb",
				new Entry("_TracedBulbSource", "Through Traced Facets", "One draws the bulb through the traced facets, from the card; zero keeps the live material's own image."),
				new Entry("_TracedBulbGain", "Gain", "Strength of the bulb seen through the facets, on top of Lamp Strength."),
				new Entry("_TracedBulbHalo", "Halo", "How much of the bulb's broad halo, the part that moves with the eye, comes through with its tight image."),
				new Entry("_TracedBulbDepth", "Depth", "Scales how deep the bulbs sit for their image: more depth, more parallax."),
				new Entry("_TracedMeshNormals", "Mesh Facet Normals", "One lights the body with the live mesh's facet normals; zero gives it a flat polished top, as a real insert has, and leaves the facets to the traced maps."),
				new Entry("_TracedDebug", "Alignment View", "Shows the bake's square on the insert: a 2 mm grid, red outside it, the traced light in green.")),
		};

		private static readonly HashSet<string> Names = new(Sections.SelectMany(section => section.Entries).Select(entry => entry.Name).Append(Keyword));

		/// <summary>Whether a property is one of the Traced controls, so a generic list can leave it to them.</summary>
		public static bool Owns(string property) => Names.Contains(property);
	}
}
