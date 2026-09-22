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
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace VisualPinball.Unity
{
	/// <summary>
	/// A procedurally generated pinball insert.
	/// </summary>
	/// <remarks>
	/// Every parameter stays editable for the life of the object: the mesh is
	/// thrown away and rebuilt from scratch whenever anything changes, so there is
	/// no baked state for a later edit to contradict. Hand edits to the generated
	/// mesh do not survive, which is the point.
	/// <para>
	/// Parameters are in VPX units, like the rest of the engine. The generated
	/// mesh is converted to world units on the way out, so the object is the right
	/// size at a scale of one. <see cref="InsertShapes.VpxUnitsPerInch"/> converts
	/// from the inches insert parts are catalogued in.
	/// </para>
	/// </remarks>
	[AddComponentMenu("Visual Pinball/Insert")]
	[RequireComponent(typeof(MeshFilter))]
	[RequireComponent(typeof(MeshRenderer))]
	public class InsertComponent : MonoBehaviour
	{
		private string MeshName => $"Insert (generated) {GetEntityId()}";

		#region Data

		[SerializeField]
		[Tooltip("The outline the insert is built from.")]
		private InsertShape _shape = InsertShape.Triangle;

		[SerializeField]
		[Min(0.01f)]
		[Tooltip("Width in VPX units, measured on the finished shape with its rounded corners applied.")]
		private float _width = InsertShapes.VpxUnitsPerInch;

		[SerializeField]
		[Min(0.01f)]
		[Tooltip("Height in VPX units, measured on the finished shape with its rounded corners applied.")]
		private float _height = 2f * InsertShapes.VpxUnitsPerInch;

		[SerializeField]
		[Tooltip("The two equally angled corners either end of the X aligned base.")]
		private InsertCornerSettings _triangleBase = new(4f, 8);

		[SerializeField]
		[Tooltip("The remaining corner, opposite the base.")]
		private InsertCornerSettings _triangleApex = new(4f, 8);

		[SerializeField]
		[Tooltip("The fillet shared by all four corners.")]
		private InsertCornerSettings _rectangleCorner = new(4f, 8);

		[SerializeField]
		[Range(1, 32)]
		[Tooltip("Subdivisions along each straight run. Raise it to even out the vertex spacing against heavily rounded corners.")]
		private int _sideSegments = 1;

		[SerializeField]
		[Min(0.01f)]
		[Tooltip("How far the bucket is sunk below the rim, in VPX units.")]
		private float _depth = 20f;

		[SerializeField]
		[Min(0f)]
		[Tooltip("How far the floor is inset before being raised again, in VPX units. Zero leaves a plain flat floor.")]
		private float _channelWidth = 3f;

		[SerializeField]
		[Min(0f)]
		[Tooltip("How far the inset floor is raised, leaving a channel around it, in VPX units.")]
		private float _channelDepth = 4f;

		[SerializeField]
		[Range(0, 128)]
		[Tooltip("Bursts around the whole wheel, twice this many rim vertices. Counts the whole circle, not just the part landing on the insert. Below two there is no starburst and the raised floor stays flat.")]
		private int _starburstBursts = 12;

		[SerializeField]
		[Tooltip("Where the starburst's centre sits relative to the insert's origin, in VPX units. Usually outside the insert, off past its narrow end.")]
		private Vector2 _starburstCenter = Vector2.zero;

		[SerializeField]
		[Min(0f)]
		[Tooltip("How far the starburst's rim rises above and drops below its centre, in VPX units.")]
		private float _starburstOffset = 1.5f;

		[SerializeField]
		[Tooltip("Round cut-outs through the raised floor, down to the channel's floor. Positions are relative to the insert's origin, in VPX units.")]
		private List<InsertHole> _holes = new();

		[SerializeField]
		[Range(3, 64)]
		[Tooltip("How many straight sides each hole's circle is drawn with.")]
		private int _holeSegments = 16;

		[SerializeField]
		[Min(0.01f)]
		[Tooltip("The radius new holes are given, from the presets or when added one by one, in VPX units.")]
		private float _holeRadius = 2f;

		#endregion

		#region Generated state

		/// <summary>
		/// The outline the current mesh was built from. Later build steps read their
		/// input from here rather than re-deriving it from the parameters.
		/// </summary>
		public InsertOutline Outline { get; private set; }

		/// <summary>
		/// Non-fatal complaints about the current parameters, such as a corner
		/// radius that had to be clamped to fit.
		/// </summary>
		public IReadOnlyList<string> Warnings => _warnings;

		/// <summary>
		/// Why the last rebuild failed, or null. The previous mesh is kept when a
		/// rebuild fails, so dragging a slider through an impossible value does not
		/// blank the object out.
		/// </summary>
		public string Error { get; private set; }

		/// <summary>The holes cut through the raised floor, as set.</summary>
		public IReadOnlyList<InsertHole> Holes => _holes;

		/// <summary>
		/// Replaces one hole and rebuilds, for tools that move holes without going
		/// through the serialized properties, such as scene handles.
		/// </summary>
		public void SetHole(int index, InsertHole hole)
		{
			_holes[index] = hole;
			RebuildMesh();
		}

		private string[] _warnings = Array.Empty<string>();

		#endregion

		public void RebuildMesh()
		{
			try {
				var warnings = new List<string>();
				var outline = BuildOutline(warnings);
				warnings.AddRange(outline.Warnings);
				var body = BuildBody(outline, warnings);

				Outline = outline;
				_warnings = warnings.ToArray();
				Error = null;

				InsertMeshGenerator.Generate(outline, body, GetOrCreateMesh());

			} catch (InsertOutlineException exception) {
				Error = exception.Message;
			}
		}

		private InsertOutline BuildOutline(List<string> warnings)
		{
			// Each shape keeps its own corner settings, so switching type and back
			// does not quietly overwrite what was there.
			var corners = _shape switch {
				InsertShape.Triangle => InsertShapes.TriangleCorners(_width, _height, _triangleBase, _triangleApex, warnings),
				InsertShape.Rectangle => InsertShapes.RectangleCorners(_width, _height, _rectangleCorner),
				_ => throw new InsertOutlineException($"Shape {_shape} has no outline builder yet."),
			};
			return InsertOutlineBuilder.Build(corners, _sideSegments);
		}

		private InsertBody BuildBody(InsertOutline outline, List<string> warnings)
		{
			// How far the floor can be inset before the ring folds through itself
			// depends on the outline, so it is the outline that works it out.
			var channelWidth = outline.ClampInset(_channelWidth);
			if (channelWidth < _channelWidth - 1e-4f) {
				warnings.Add($"Channel width reduced to {channelWidth:0.##} to fit inside the outline.");
			}

			// A raised floor standing proud of the rim would poke through whatever
			// the insert is seated in.
			var channelDepth = math.min(_channelDepth, _depth);
			if (channelDepth < _channelDepth - 1e-4f) {
				warnings.Add($"Channel depth reduced to {channelDepth:0.##} to stay below the rim.");
			}

			var starburstOffset = _starburstOffset;
			if (_starburstBursts >= InsertStarburst.MinBursts) {
				if (channelWidth <= InsertOutlineBuilder.Epsilon) {
					warnings.Add("The starburst replaces the raised floor, which needs a channel to stand above.");
				}

				// The zigzag straddles the raised floor, so it can reach no further
				// down than the channel is deep, nor further up than the rim. It is
				// the size that is held: a negative offset only swaps which spokes
				// are ridges, and reaches just as far.
				var limit = InsertStarburst.OffsetLimit(_depth, channelDepth);
				starburstOffset = math.clamp(_starburstOffset, -limit, limit);
				if (math.abs(starburstOffset) < math.abs(_starburstOffset) - 1e-4f) {
					warnings.Add($"Starburst offset reduced to {starburstOffset:0.##} to stay between the channel floor and the rim.");
				}
			}

			if (_holes.Count > 0 && channelWidth <= InsertOutlineBuilder.Epsilon) {
				warnings.Add("Holes cut through the raised floor, which needs a channel to stand above.");
			}

			return new InsertBody {
				Depth = _depth,
				ChannelWidth = channelWidth,
				ChannelDepth = channelDepth,
				StarburstBursts = _starburstBursts,
				StarburstCenter = new float2(_starburstCenter.x, _starburstCenter.y),
				StarburstOffset = starburstOffset,
				Holes = _holes.ToArray(),
				HoleSegments = _holeSegments,
			};
		}

		/// <summary>
		/// The generated mesh, reused across rebuilds so they do not pile up
		/// orphans, but only ever one this component made for itself.
		/// </summary>
		/// <remarks>
		/// The name carries this component's entity id, because a bare name is not
		/// evidence of ownership. Duplicating an insert hands the copy a reference
		/// to the original's mesh, and rebuilding through that reference would
		/// redraw both objects at once; a saved asset that happens to share the name
		/// would simply be overwritten. Neither survives the entity id, and a
		/// persistent asset is refused outright whatever it is called.
		/// </remarks>
		private Mesh GetOrCreateMesh()
		{
			var filter = GetComponent<MeshFilter>();
			var mesh = filter.sharedMesh;
			if (mesh != null && mesh.name == MeshName && !IsAsset(mesh)) {
				return mesh;
			}

			mesh = new Mesh { name = MeshName };
			filter.sharedMesh = mesh;
			return mesh;
		}

		private static bool IsAsset(Mesh mesh)
		{
#if UNITY_EDITOR
			return UnityEditor.EditorUtility.IsPersistent(mesh);
#else
			return false;
#endif
		}

		private void Reset() => RebuildMesh();

#if UNITY_EDITOR
		private void OnValidate()
		{
			// OnValidate runs mid-serialization, where touching sibling components
			// is not allowed, so the rebuild waits for the next editor tick.
			UnityEditor.EditorApplication.delayCall += () => {
				if (this != null) {
					RebuildMesh();
				}
			};
		}
#endif
	}
}
