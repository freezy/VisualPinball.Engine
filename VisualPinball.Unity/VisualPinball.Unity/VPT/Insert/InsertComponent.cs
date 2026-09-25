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
using System.Linq;
using Unity.Mathematics;
using UnityEngine;
using VisualPinball.Unity.Playfield;

namespace VisualPinball.Unity
{
	/// <summary>
	/// A procedurally generated pinball insert.
	/// </summary>
	/// <remarks>
	/// Every parameter stays editable for the life of the object: the mesh is
	/// rebuilt from scratch whenever anything changes. Baked UVs and tangents
	/// apply only while they fit that geometry. Hand edits to the generated mesh
	/// do not survive, which is the point.
	/// <para>
	/// Parameters are in VPX units, like the rest of the engine. The generated
	/// mesh is converted to world units on the way out, so the object is the right
	/// size at a scale of one. <see cref="InsertShapes.VpxUnitsPerInch"/> converts
	/// from the inches insert parts are catalogued in.
	/// </para>
	/// </remarks>
	[AddComponentMenu("Visual Pinball/Insert")]
	[ExecuteAlways]
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
		[Tooltip("The fillet shared by the diamond's left and right points.")]
		private InsertCornerSettings _diamondSide = new(2f, 8);

		[SerializeField]
		[Tooltip("The fillet shared by the diamond's top and bottom points.")]
		private InsertCornerSettings _diamondTip = new(4f, 8);

		[SerializeField]
		[Range(-75f, 75f)]
		[Tooltip("How far the diamond's top and bottom points lean over, in degrees from upright, positive to the right as seen on the table.")]
		private float _diamondShear;

		[SerializeField]
		[Min(0.01f)]
		[Tooltip("Width of the flash in VPX units, spike to spike, measured on the finished shape.")]
		private float _flashWidth = 50f;

		[SerializeField]
		[Min(0.01f)]
		[Tooltip("Height of the flash in VPX units, end to end.")]
		private float _flashHeight = 125f;

		[SerializeField]
		[Min(0.01f)]
		[Tooltip("How wide the flash's flat ends are, at the top and the bottom, in VPX units.")]
		private float _flashTip = 10f;

		[SerializeField]
		[Min(0.01f)]
		[Tooltip("How far each step goes in from its spike to its notch, in VPX units. Less than half the width.")]
		private float _flashNotch = 16f;

		[SerializeField]
		[Tooltip("How far above and below the middle the two steps sit, in VPX units. Zero puts both steps on the middle line; below zero the left step drops below it and the right one rises above, so the arms overlap further.")]
		private float _flashStep = -8f;

		[SerializeField]
		[Tooltip("The fillet shared by the four corners of the flash's flat ends.")]
		private InsertCornerSettings _flashTipCorner = new(5.2f, 4);

		[SerializeField]
		[Tooltip("The fillet shared by the flash's two spikes, where an outer edge turns back into a step.")]
		private InsertCornerSettings _flashSpikeCorner = new(5f, 4);

		[SerializeField]
		[Tooltip("The fillet shared by the flash's two notches, where a step turns into the other arm's outer edge; it rounds inwards.")]
		private InsertCornerSettings _flashNotchCorner = new(3f, 3);

		[SerializeField]
		[Tooltip("A thin line engraved into the flash's raised middle, a little way in from its edge: a lightning bolt drawn into the plastic, its ends sharp and its notches rounded. It takes the starburst's place: the inspector offers the two as one Pattern choice.")]
		private bool _flashGroove;

		[SerializeField]
		[Min(0.1f)]
		[Tooltip("How far in from the raised middle's edge the line's middle runs, in VPX units. Far enough in and the flat ends close up into points.")]
		private float _flashGrooveInset = 6f;

		[SerializeField]
		[Min(0.1f)]
		[Tooltip("How wide the engraved line is, in VPX units.")]
		private float _flashGrooveWidth = 2f;

		[SerializeField]
		[Min(0f)]
		[Tooltip("How deep the line goes into the surface at its middle, in VPX units; its floor is a half round.")]
		private float _flashGrooveDepth = 0.8f;

		[SerializeField]
		[Range(1, 16)]
		[Tooltip("How many steps each side of the line's floor is drawn with, from its edge down to its middle. One is a V cut, which the bake's edge bevel rounds off; more draw the round in the mesh itself.")]
		private int _flashGrooveSegments = 1;

		[SerializeField]
		[Tooltip("The round of the bullet's tip: a circle of this radius touching the top, no wider than the bullet and no taller, drawn with this many segments over the whole arc, rounded up to an even count. A radius of zero makes a pointed tip.")]
		private InsertCornerSettings _bulletTip = new(5f, 8);

		[SerializeField]
		[Min(0f)]
		[Tooltip("How far up from the base the bullet's straight sides run before the flanks turn in towards the tip, in VPX units. Zero starts the flanks at the base corners; the shoulders stay below the tip's centre.")]
		private float _bulletShoulder = 20f;

		[SerializeField]
		[Tooltip("The fillet shared by the bullet's two shoulders, where a straight side turns in towards the tip.")]
		private InsertCornerSettings _bulletShoulderCorner = new(2f, 4);

		[SerializeField]
		[Tooltip("The fillet shared by the bullet's two base corners.")]
		private InsertCornerSettings _bulletBaseCorner = new(3f, 8);

		[SerializeField]
		[Tooltip("The fillet at the arrow's tip.")]
		private InsertCornerSettings _arrowTip = new(4f, 8);

		[SerializeField]
		[Min(0f)]
		[Tooltip("How far the notch cuts up into the arrow from its bottom, in VPX units. Zero leaves the base flat.")]
		private float _arrowNotchDepth = 10f;

		[SerializeField]
		[Min(0f)]
		[Tooltip("How wide the notch is where it opens into the base, between the two feet, in VPX units. The full width leaves no feet: the whole base is the notch.")]
		private float _arrowNotchWidth = 14f;

		[SerializeField]
		[Range(-85f, 85f)]
		[Tooltip("How far the feet tilt, in degrees. Positive lifts their inner corners up towards the notch; negative drops them below the outer corners, which cuts a chevron's feet off square.")]
		private float _arrowFootAngle;

		[SerializeField]
		[Tooltip("The fillet at the notch's apex, which rounds inwards. Large enough, it turns the notch into a round scoop.")]
		private InsertCornerSettings _arrowNotchCorner = new(4f, 8);

		[SerializeField]
		[Tooltip("The fillet shared by the two inner corners, where a foot turns up into the notch.")]
		private InsertCornerSettings _arrowInnerCorner = new(2f, 4);

		[SerializeField]
		[Tooltip("The fillet shared by the two feet, the arrow's outer base corners.")]
		private InsertCornerSettings _arrowFootCorner = new(2.5f, 8);

		[SerializeField]
		[Min(0.01f)]
		[Tooltip("Diameter of the circle, or of the circle a banana's band is bent round, in VPX units.")]
		private float _diameter = InsertShapes.VpxUnitsPerInch;

		[SerializeField]
		[Range(6, 128)]
		[Tooltip("How many straight sides the circle is drawn with, which is all the sampling a circle gets: Side Segments do not apply.")]
		private int _circleVertices = 32;

		[SerializeField]
		[Min(0.01f)]
		[Tooltip("How thick the banana's band is, across its circle, in VPX units. Half of it reaches out either side of the circle.")]
		private float _bananaThickness = 0.5f * InsertShapes.VpxUnitsPerInch;

		[SerializeField]
		[Range(1f, 180f)]
		[Tooltip("How far round its circle the banana reaches, in degrees. The circle's centre is above the band, so its ends turn up.")]
		private float _bananaAngle = 90f;

		[SerializeField]
		[Range(2, 128)]
		[Tooltip("How many straight sides each of the banana's two arcs is drawn with.")]
		private int _bananaSegments = 24;

		[SerializeField]
		[Range(2, 32)]
		[Tooltip("How many straight sides each of the banana's rounded ends is drawn with.")]
		private int _bananaEndSegments = 8;

		[SerializeField]
		[Range(1, 32)]
		[Tooltip("Subdivisions along each straight run. Raise it to even out the vertex spacing against heavily rounded corners.")]
		private int _sideSegments = 1;

		[SerializeField]
		[Min(0.01f)]
		[Tooltip("How far the floor is sunk below the rim, in VPX units. With a channel, that is the raised middle; the channel goes deeper still.")]
		private float _depth = 16f;

		[SerializeField]
		[Tooltip("Whether a channel runs round the floor, sunk further below it, which leaves the floor standing as a raised middle. Off makes the floor reach the wall.")]
		private bool _channel = true;

		[SerializeField]
		[Min(0f)]
		[Tooltip("How far in from the outline the channel reaches, in VPX units.")]
		private float _channelWidth = 3f;

		[SerializeField]
		[Min(0f)]
		[Tooltip("How far below the floor the channel is sunk, in VPX units.")]
		private float _channelDepth = 4f;

		[SerializeField]
		[Min(0f)]
		[Tooltip("How far the rounding where the outer wall meets the channel's floor reaches, up the wall and in across the floor, in VPX units. Zero leaves a sharp edge. Without a channel there is no bevel.")]
		private float _bottomBevel = 1f;

		[SerializeField]
		[Range(1, 16)]
		[Tooltip("How many steps the bottom bevel is drawn with. One is a flat chamfer.")]
		private int _bottomBevelSegments = 4;

		[SerializeField]
		[Tooltip("Whether the raised floor carries a starburst. Off keeps it flat.")]
		private bool _starburst = true;

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
		[Tooltip("Round cut-outs through the raised floor, down to the channel's floor. Positions are relative to the insert's origin, in VPX units. Without a channel there is no raised floor, and no holes.")]
		private List<InsertHole> _holes = new();

		[SerializeField]
		[Range(3, 64)]
		[Tooltip("How many straight sides each hole's circle is drawn with.")]
		private int _holeSegments = 16;

		[SerializeField]
		[Min(0.01f)]
		[Tooltip("The radius new holes are given, from the presets or when added one by one, in VPX units.")]
		private float _holeRadius = 2f;

		[SerializeField]
		[Tooltip("How the UVs are optimized and the normal map baked, in Blender.")]
		private InsertBakeSettings _bakeSettings = InsertBakeSettings.Default;

		[SerializeField]
		[Tooltip("The last bake. Its UVs and tangents apply only while it fits; its material stays as a preview until rebaked.")]
		private InsertBakeData _bake;

		[SerializeField]
		[Tooltip("The original material, restored when Clear Bake removes the bake.")]
		private Material _unbakedMaterial;

		[SerializeField]
		[HideInInspector]
		[Tooltip("Tells this insert from its duplicates, so a bake knows whose it is.")]
		private string _id;

		[SerializeField]
		[Tooltip("Optional transparent top surface at the rim, in material slot 2. Independent of the body bake.")]
		private Material _coverMaterial;

		[SerializeField]
		[Tooltip("Use the playfield's UVs and normal map on an Insert Cover material. Otherwise the cover uses its assigned material unchanged.")]
		private bool _usePlayfieldNormals;

		[SerializeField]
		[Tooltip("Optional surface renderer override. Leave empty to find the playfield in this table automatically.")]
		private MeshRenderer _coverPlayfield;

		#endregion

		#region Generated state

		/// <summary>
		/// The outline the current mesh was built from. Later build steps read their
		/// input from here rather than re-deriving it from the parameters.
		/// </summary>
		public InsertOutline Outline { get; private set; }

		/// <summary>The line engraved into the raised middle's surface, or null without one.</summary>
		public InsertGroove Groove => Body.Groove;

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
		/// Where a banana's arc has its centre, relative to the insert's origin, in
		/// VPX units: where its starburst belongs. Zero for any other shape.
		/// </summary>
		public Vector2 ArcCenter { get; private set; }

		/// <summary>
		/// The body the current mesh was built from, every value clamped as the
		/// generator got it: the channel's width and the bevel as they really are.
		/// </summary>
		public InsertBody Body { get; private set; }

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

		/// <summary>The last bake, whether or not it still fits.</summary>
		public InsertBakeData Bake => _bake;

		/// <summary>
		/// An id of this insert's own, kept across sessions and different from
		/// any duplicate's, which is what a bake records as its owner.
		/// </summary>
		public string Id => _id;

		public Material CoverMaterial => _coverMaterial;
		public MeshRenderer CoverPlayfield => _coverPlayfield != null ? _coverPlayfield : _autoCoverPlayfield;
		public string CoverWarning { get; private set; }
		public bool UsePlayfieldNormals
		{
			get => _usePlayfieldNormals;
			set { _usePlayfieldNormals = value; RebuildMesh(); }
		}

		private MeshRenderer _autoCoverPlayfield;
		private Mesh _coverSourceMesh;
		private int _coverProjectionVersion;
		private InsertCoverUv _coverProjection;
		private bool _coverProjectionValid;
		private int _coverFirstVertex;
		private Matrix4x4 _coverToPlayfield;
		private Vector2 _coverTextureScale;
		private Vector2 _coverTextureOffset;
		private MaterialPropertyBlock _coverProperties;
		private bool _coverMapped;
		private bool _coverPropertiesApplied;
		private bool _coverSlotRead;

		/// <summary>
		/// Puts a cover material on, in the renderer's second slot, or takes the
		/// cover off with null. The slot is the cover: a material put there the
		/// way any renderer takes one is as good as one set here, and neither
		/// overwrites the other.
		/// </summary>
		public void SetCoverMaterial(Material material)
		{
			_coverMaterial = material;
			var renderer = GetComponent<MeshRenderer>();
			var body = renderer.sharedMaterial;
			renderer.sharedMaterials = material != null ? new[] { body, material } : new[] { body };
			RebuildMesh();
		}

		/// <summary>
		/// Whether the baked UVs and tangents fit the generated mesh. Geometry
		/// changes make the bake stale; its material stays as a preview until rebaked.
		/// </summary>
		public bool IsBakeFresh { get; private set; }

		/// <summary>The material the insert wears when it is not showing its bake.</summary>
		public Material UnbakedMaterial
		{
			get {
				var worn = GetComponent<MeshRenderer>().sharedMaterial;
				return _bake != null && worn == _bake.Material ? _unbakedMaterial : worn;
			}
		}

		#endregion

		public void RebuildMesh()
		{
			try {
				var warnings = new List<string>();
				var outline = BuildOutline(warnings);
				warnings.AddRange(outline.Warnings);
				var body = BuildBody(outline, warnings);

				Outline = outline;
				Body = body;
				_warnings = warnings.ToArray();
				Error = null;

				var mesh = GetOrCreateMesh();
				var layout = new InsertMeshLayout();
				InsertMeshGenerator.Generate(outline, body, mesh, layout);
				IsBakeFresh = _bake != null && _bake.TryApply(mesh, layout.Hash);
				// Add the cover only after applying the body's bake. It must never
				// change the bake hash, atlas, or per-corner tangent mapping.
				// The renderer's second slot is the cover: whatever was put there,
				// through the field or straight onto the renderer, stays, and a
				// slot taken away takes the cover off. The field mirrors the slot
				// for the inspector, and seeds it just once, on an insert whose
				// renderer arrives without one.
				var renderer = GetComponent<MeshRenderer>();
				var slots = renderer.sharedMaterials;
				if (_coverSlotRead || slots.Length > 1) {
					_coverMaterial = slots.Length > 1 ? slots[1] : null;
				}
				_coverSlotRead = true;
				if (_coverMaterial != null) {
					_coverFirstVertex = mesh.vertexCount;
					InsertMeshGenerator.AddCover(outline, mesh);
					_coverMapped = false;
					if (slots.Length != 2 || slots[1] != _coverMaterial) {
						renderer.sharedMaterials = new[] { renderer.sharedMaterial, _coverMaterial };
					}
					UpdateCover(true);
				} else if (slots.Length > 1) {
					renderer.sharedMaterials = new[] { renderer.sharedMaterial };
				}

			} catch (InsertOutlineException exception) {
				Error = exception.Message;
			}
		}

		public InsertBakeSettings BakeSettings => _bakeSettings;

		/// <summary>
		/// Replaces the bake and puts its material on, or drops the bake with null
		/// and puts back the material the insert wore before its first bake, and
		/// rebuilds.
		/// </summary>
		/// <remarks>
		/// This is the one place a bake changes what the insert wears. A material
		/// put on by hand stays on through geometry edits and rebuilds, and a bake
		/// made after that is made into that material, or into a copy of it, so
		/// what comes back here is never an older material.
		/// </remarks>
		public void SetBake(InsertBakeData bake)
		{
			var renderer = GetComponent<MeshRenderer>();
			if (bake == null) {
				if (_bake != null && renderer.sharedMaterial == _bake.Material) {
					renderer.sharedMaterial = _unbakedMaterial;
				}
				_unbakedMaterial = null;
			} else {
				if (_bake == null) {
					_unbakedMaterial = renderer.sharedMaterial;
				}
				if (bake.Material != null) {
					renderer.sharedMaterial = bake.Material;
				}
			}
			_bake = bake;
			RebuildMesh();
		}

		/// <summary>
		/// Generates the mesh the parameters describe into <paramref name="target"/>,
		/// as the generator lays it out, and describes it into
		/// <paramref name="layout"/>. This is what a bake starts from.
		/// </summary>
		/// <exception cref="InsertOutlineException">The parameters describe no valid insert.</exception>
		public InsertBody Generate(Mesh target, InsertMeshLayout layout)
		{
			var warnings = new List<string>();
			var outline = BuildOutline(warnings);
			var body = BuildBody(outline, warnings);
			InsertMeshGenerator.Generate(outline, body, target, layout);
			return body;
		}

		private InsertOutline BuildOutline(List<string> warnings)
		{
			// Each shape keeps its own corner settings, so switching type and back
			// does not quietly overwrite what was there.
			var arcCenter = float2.zero;
			var corners = _shape switch {
				InsertShape.Triangle => InsertShapes.TriangleCorners(_width, _height, _triangleBase, _triangleApex, warnings),
				InsertShape.Rectangle => InsertShapes.RectangleCorners(_width, _height, _rectangleCorner),
				InsertShape.Circle => InsertShapes.CircleCorners(_diameter, _circleVertices),
				InsertShape.Banana => InsertShapes.BananaCorners(_diameter, _bananaThickness, _bananaAngle, _bananaSegments, _bananaEndSegments, out arcCenter),
				InsertShape.Diamond => InsertShapes.DiamondCorners(_width, _height, _diamondSide, _diamondTip, _diamondShear, warnings),
				InsertShape.Flash => InsertShapes.FlashCorners(_flashWidth, _flashHeight, _flashTip, _flashNotch, _flashStep, _flashTipCorner, _flashSpikeCorner, _flashNotchCorner, warnings),
				InsertShape.Bullet => InsertShapes.BulletCorners(_width, _height, _bulletTip, _bulletShoulder, _bulletShoulderCorner, _bulletBaseCorner, warnings),
				InsertShape.Arrow => InsertShapes.ArrowCorners(_width, _height, _arrowTip, _arrowNotchDepth, _arrowNotchWidth, _arrowFootAngle, _arrowNotchCorner, _arrowInnerCorner, _arrowFootCorner, warnings),
				_ => throw new InsertOutlineException($"Shape {_shape} has no outline builder yet."),
			};
			ArcCenter = new Vector2(arcCenter.x, arcCenter.y);
			// A circle's or a banana's sides are its sampling already; subdividing
			// them further only multiplies wall charts and lets the inset clamp
			// trip on the samples nearest each corner.
			return InsertOutlineBuilder.Build(corners, Sampled ? 1 : _sideSegments);
		}

		/// <summary>
		/// The flash's engraved line: the outline eroded by the channel's width
		/// and the line's inset, which closes the flat ends up into points and
		/// widens the notches, built into an outline of its own, its corners
		/// sharp where the erosion sharpened them.
		/// </summary>
		private InsertGroove BuildGroove(InsertOutline outline, float channelWidth, List<string> warnings)
		{
			if (_shape != InsertShape.Flash || !_flashGroove) {
				return null;
			}
			var half = math.max(_flashGrooveWidth, 0.1f) * 0.5f;
			var inset = channelWidth + math.max(_flashGrooveInset, half + 0.01f);
			// Sharp all the way: a tip the erosion closes up is a point, and the
			// band's edges meet there in mitres, as the engraver's line does.
			var corners = InsertShapes.Erode(outline, inset, 0f, warnings);
			if (corners == null) {
				warnings.Add("The engraved line does not fit inside the flash: bring its inset down.");
				return null;
			}
			InsertOutline line;
			try {
				// The line's straight runs need no subdividing: the trough along
				// them is flat, and every point costs a column of faces.
				line = InsertOutlineBuilder.Build(corners, 1, center: false);
			} catch (InsertOutlineException e) {
				warnings.Add($"The engraved line cannot be drawn: {e.Message}");
				return null;
			}
			foreach (var warning in line.Warnings) {
				warnings.Add("Engraved line: " + warning);
			}
			// Two parts of the shape far apart along its edge can meet under the
			// erosion, the arms of a flash whose notches nearly touch, say: the
			// line then crosses itself, and there is no drawing it in one piece.
			var path = new List<float2>(line.Points.Count);
			foreach (var point in line.Points) {
				path.Add(point.Position);
			}
			if (InsertGroove.SelfCrossing(path)) {
				warnings.Add("The engraved line crosses itself where the shape pinches: bring its inset down.");
				return null;
			}
			// The band's outer edge, the line offset outwards by half its width,
			// has to stay on the raised middle. A sharp tip's mitre is shortened
			// where it would leave it; what is still outside is a line too close
			// to the edge for its width.
			var surface = new List<float2>(outline.Points.Count);
			for (var i = 0; i < outline.Points.Count; i++) {
				surface.Add(outline.OffsetPoint(i, channelWidth));
			}
			var groove = new InsertGroove {
				Outline = line,
				Surface = surface,
				Width = half * 2f,
				Depth = math.max(0f, _flashGrooveDepth),
				Segments = math.clamp(_flashGrooveSegments, 1, 16),
			};
			foreach (var edge in groove.Ring(-half, line.WeldTolerance, out _)) {
				if (!InsertGroove.Contains(surface, edge)) {
					warnings.Add("The engraved line's edge reaches past the middle's edge: bring its inset up or its width down.");
					return null;
				}
			}
			return groove;
		}

		/// <summary>Whether the shape's own vertex counts are its sampling, with no sides to subdivide.</summary>
		private bool Sampled => _shape is InsertShape.Circle or InsertShape.Banana;

		/// <summary>
		/// How far the outline can be inset at all: a band can take no more than
		/// half its thickness before its two arcs meet, less a little to weld.
		/// </summary>
		private float MaxInset(InsertOutline outline) => _shape == InsertShape.Banana
			? math.max(0f, _bananaThickness * 0.5f - math.max(outline.WeldTolerance * 16f, _bananaThickness * 0.05f))
			: float.MaxValue;

		private InsertBody BuildBody(InsertOutline outline, List<string> warnings)
		{
			// How far the floor can be inset before the ring folds through itself
			// depends on the outline, so it is the outline that works it out.
			var channelWidth = 0f;
			if (_channel) {
				channelWidth = outline.ClampInset(math.min(_channelWidth, MaxInset(outline)));
				if (channelWidth < _channelWidth - 1e-4f) {
					warnings.Add($"Channel width reduced to {channelWidth:0.##} to fit inside the outline.");
				}
			}
			var channel = channelWidth > InsertOutlineBuilder.Epsilon;
			var channelDepth = channel ? _channelDepth : 0f;

			// The body's depth is the bucket's: the channel's floor when there is
			// one, else the floor itself.
			var depth = _depth + channelDepth;

			// The bevel rounds the wall into the channel's floor, so there is none
			// without a channel. It can climb no higher than the wall, and must
			// leave some of the channel's floor between it and the raised middle,
			// or the two collide.
			var bottomBevel = 0f;
			if (channel) {
				bottomBevel = math.min(_bottomBevel, depth);
				bottomBevel = math.min(bottomBevel, channelWidth - math.max(outline.WeldTolerance * 16f, channelWidth * 0.05f));
				bottomBevel = outline.ClampInset(math.max(0f, math.min(bottomBevel, MaxInset(outline))));
				if (bottomBevel < _bottomBevel - 1e-4f) {
					warnings.Add($"Bottom bevel reduced to {bottomBevel:0.##} to fit the wall and the channel.");
				}
			}

			// The engraved line takes the raised middle's surface, where the
			// starburst would be: the two never share it.
			var groove = BuildGroove(outline, channelWidth, warnings);
			if (groove != null && _starburst) {
				warnings.Add("The engraved line takes the starburst's place: the starburst is not built while the line is on.");
			}

			var body = new InsertBody {
				Depth = depth,
				ChannelWidth = channelWidth,
				ChannelDepth = channelDepth,
				Groove = groove,
				StarburstBursts = _starburst && groove == null ? _starburstBursts : 0,
				StarburstCenter = new float2(_starburstCenter.x, _starburstCenter.y),
				StarburstOffset = _starburstOffset,
				Holes = channel ? _holes.ToArray() : null,
				HoleSegments = _holeSegments,
				BottomBevel = bottomBevel,
				BottomBevelSegments = _bottomBevelSegments,
			};

			if (body.StarburstBursts >= InsertStarburst.MinBursts) {
				// The zigzag straddles the floor, so it can reach no further down than
				// what lies below it, nor further up than the rim. It is the size that
				// is held: a negative offset only swaps which spokes are ridges, and
				// reaches just as far.
				var limit = body.StarburstLimit();
				body.StarburstOffset = math.clamp(_starburstOffset, -limit, limit);
				if (math.abs(body.StarburstOffset) < math.abs(_starburstOffset) - 1e-4f) {
					warnings.Add(channel
						? $"Starburst offset reduced to {body.StarburstOffset:0.##} to stay between the channel floor and the rim."
						: $"Starburst offset reduced to {body.StarburstOffset:0.##} to stay below the rim.");
				}
			}
			return body;
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
		private void OnEnable()
		{
#if UNITY_EDITOR
			ClaimId();
#endif
			var mesh = GetComponent<MeshFilter>().sharedMesh;
			// Restore nonserialized state once, but retain the generated mesh when
			// an already initialized insert is simply disabled and enabled again.
			if (Outline == null || mesh == null || mesh.name != MeshName) {
				RebuildMesh();
#if UNITY_EDITOR
				_rebuildPending = false;
#endif
			} else {
				UpdateCover(true);
			}
		}
		private void LateUpdate() => UpdateCover(false);

#if UNITY_EDITOR
		/// <summary>Which living, enabled insert holds which id, so a duplicate shows up on arrival.</summary>
		private static readonly Dictionary<string, InsertComponent> Ids = new();

		/// <summary>
		/// The insert's id, made sure of: one of its own, that no other insert in
		/// the open scenes or the prefab being edited carries as well. A duplicate
		/// arrives with its original's id and an instance with its prefab's, and
		/// an inactive one never passes through OnEnable, so this is asked before
		/// anything is written in the id's name. The insert that holds the id from
		/// having been enabled keeps it and every other carrier is renewed; when
		/// none of them was enabled, nobody can tell the original from the copy,
		/// so the one asking is the one renewed, and its bake goes to a fresh
		/// folder rather than over anyone's.
		/// </summary>
		public string EnsureId()
		{
			if (_persistPending) {
				// What OnEnable put off is written down now, at a safe point.
				Persist();
			}
			if (string.IsNullOrEmpty(_id) || InheritsId()) {
				Renew(true);
				return _id;
			}
			Ids.TryGetValue(_id, out var holder);
			if (holder != null && holder != this) {
				Renew(true);
				return _id;
			}
			var others = LoadedInserts().Where(other => other != this && other._id == _id).ToList();
			if (others.Count > 0) {
				if (holder != this) {
					Renew(true);
					return _id;
				}
				foreach (var other in others) {
					other.Renew(true);
				}
			}
			Ids[_id] = this;
			return _id;
		}

		/// <summary>
		/// On enabling: takes the id if it is free, or a new one if it has none,
		/// is its prefab's, or is held by another enabled insert.
		/// </summary>
		private void ClaimId()
		{
			if (Application.isPlaying) {
				return;
			}
			if (string.IsNullOrEmpty(_id) || InheritsId()) {
				Renew(false);
				return;
			}
			if (Ids.TryGetValue(_id, out var holder) && holder != null && holder != this) {
				Renew(false);
				return;
			}
			Ids[_id] = this;
		}

		/// <summary>
		/// Whether the id is a prefab's, only shown by this instance: the prefab's
		/// bake is the prefab's, and an instance is told from the prefab and from
		/// its siblings by an id of its own, recorded as an override.
		/// </summary>
		private bool InheritsId()
		{
			if (_persistPending || !UnityEditor.PrefabUtility.IsPartOfPrefabInstance(this)) {
				return false;
			}
			var source = UnityEditor.PrefabUtility.GetCorrespondingObjectFromSource(this);
			var modifications = UnityEditor.PrefabUtility.GetPropertyModifications(this);
			return modifications == null || !modifications.Any(m => m.target == source && m.propertyPath == "_id");
		}

		/// <summary>
		/// A new id, written down at once when asked for at a safe point, or on
		/// the next editor tick when it happened while a scene was loading.
		/// </summary>
		private void Renew(bool now)
		{
			_id = Guid.NewGuid().ToString("N");
			Ids[_id] = this;
			if (now) {
				Persist();
			} else if (!_persistPending) {
				_persistPending = true;
				UnityEditor.EditorApplication.delayCall += Persist;
			}
		}

		/// <summary>Whether a renewed id still waits to be written down; until then it counts as the insert's own.</summary>
		[NonSerialized]
		private bool _persistPending;

		private void Persist()
		{
			_persistPending = false;
			if (this == null) {
				return;
			}
			UnityEditor.EditorUtility.SetDirty(this);
			if (UnityEditor.PrefabUtility.IsPartOfPrefabInstance(this)) {
				UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(this);
			}
		}

		/// <summary>Every insert in the open scenes and the prefab being edited, inactive ones included.</summary>
		private static IEnumerable<InsertComponent> LoadedInserts()
		{
			var inserts = FindObjectsByType<InsertComponent>(FindObjectsInactive.Include).AsEnumerable();
			var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
			if (stage != null) {
				inserts = inserts.Concat(stage.prefabContentsRoot.GetComponentsInChildren<InsertComponent>(true));
			}
			return inserts.Distinct();
		}

		private void OnDestroy()
		{
			if (!string.IsNullOrEmpty(_id) && Ids.TryGetValue(_id, out var holder) && holder == this) {
				Ids.Remove(_id);
			}
		}
#endif

		private void UpdateCover(bool force)
		{
			CoverWarning = null;
			if (_coverMaterial == null) { return; }
			var renderer = GetComponent<MeshRenderer>();
			var mesh = GetComponent<MeshFilter>().sharedMesh;
			if (mesh == null || mesh.subMeshCount < 2) { return; }
			if (!_usePlayfieldNormals) {
				ClearCoverProjection(renderer, mesh, force);
				return;
			}
			if (!_coverMaterial.HasProperty("_CoverNormalMap") || !_coverMaterial.HasProperty("_PlayfieldNormalStrength")) {
				CoverWarning = "Use a Visual Pinball/Insert Cover material for playfield normals, or disable Use Playfield Normals to use this material unchanged.";
				ClearCoverProjection(renderer, mesh, force);
				return;
			}
			if (force && _coverPlayfield == null) {
				_autoCoverPlayfield = FindCoverPlayfield();
			}
			var playfield = CoverPlayfield;
			if (playfield == null) {
				CoverWarning = "No playfield surface found in this table. Assign a Playfield Override or disable Use Playfield Normals.";
				ClearCoverProjection(renderer, mesh, force);
				return;
			}
			var sourceMesh = playfield.TryGetComponent<MeshFilter>(out var filter) ? filter.sharedMesh : null;
			_coverProjectionValid = InsertCoverUv.TryCreate(sourceMesh, out _coverProjection, out var version) && playfield != renderer;
			force |= sourceMesh != _coverSourceMesh || version != _coverProjectionVersion;
			_coverSourceMesh = sourceMesh;
			_coverProjectionVersion = version;
			var source = playfield.sharedMaterial;
			if (!_coverProjectionValid || source == null || !source.HasProperty("_NormalMap") ||
				(source.HasProperty("_UVBase") && source.GetFloat("_UVBase") != 0f) ||
				(source.HasProperty("_NormalMapSpace") && source.GetFloat("_NormalMapSpace") != 0f)) {
				CoverWarning = "The cover needs a readable playfield mesh with planar XZ UV0 and a tangent-space normal map.";
				ClearCoverProjection(renderer, mesh, force);
				return;
			}
			var texture = source.GetTexture("_NormalMap");
			if (texture == null) {
				CoverWarning = "The playfield material has no normal map. The cover uses its own material settings.";
				ClearCoverProjection(renderer, mesh, force);
				return;
			}
			// HDRP Lit uses the base map's UV transform for its normal map as well.
			var slot = source.HasProperty("_BaseColorMap") ? "_BaseColorMap" : "_NormalMap";
			var scale = source.GetTextureScale(slot);
			var offset = source.GetTextureOffset(slot);
			var matrix = playfield.transform.worldToLocalMatrix * transform.localToWorldMatrix;
			if (force || !_coverMapped || matrix != _coverToPlayfield || scale != _coverTextureScale || offset != _coverTextureOffset) {
				_coverProjection.Apply(mesh, _coverFirstVertex, matrix, scale, offset);
				_coverToPlayfield = matrix;
				_coverTextureScale = scale;
				_coverTextureOffset = offset;
				_coverMapped = true;
			}
			_coverProperties ??= new MaterialPropertyBlock();
			renderer.GetPropertyBlock(_coverProperties, 1);
			_coverProperties.SetTexture("_CoverNormalMap", texture);
			// Source tiling is already applied to the projected UVs.
			_coverProperties.SetVector("_CoverNormalMap_ST", new Vector4(1f, 1f, 0f, 0f));
			_coverProperties.SetFloat("_PlayfieldNormalStrength", source.HasProperty("_NormalScale") ? source.GetFloat("_NormalScale") : 1f);
			renderer.SetPropertyBlock(_coverProperties, 1);
			_coverPropertiesApplied = true;
		}

		private void ClearCoverProjection(MeshRenderer renderer, Mesh mesh, bool force)
		{
			if (_coverPropertiesApplied || force) {
				renderer.SetPropertyBlock(null, 1);
				_coverPropertiesApplied = false;
			}
			if (_coverMapped) {
				InsertCoverUv.ApplyLocal(mesh, _coverFirstVertex);
				_coverMapped = false;
			}
		}

		private MeshRenderer FindCoverPlayfield()
		{
			var playfield = GetComponentInParent<PlayfieldComponent>();
			if (playfield == null) {
				var table = GetComponentInParent<TableComponent>();
				if (table == null) { return null; }
				var playfields = table.GetComponentsInChildren<PlayfieldComponent>(true);
				if (playfields.Length != 1) { return null; }
				playfield = playfields[0];
			}
			// Procedural playfields expose their surface through this component.
			var meshComponent = playfield.GetComponentInChildren<PlayfieldMeshComponent>(true);
			if (meshComponent != null && meshComponent.TryGetComponent<MeshRenderer>(out var procedural) && IsCoverSource(procedural)) { return procedural; }
			// Imported boards may have no renderer on PlayfieldMeshComponent.
			// Prefer the largest planar surface within this playfield, never another table.
			MeshRenderer best = null;
			var largest = 0f;
			foreach (var candidate in playfield.GetComponentsInChildren<MeshRenderer>(true)) {
				if (!IsCoverSource(candidate) || candidate.GetComponentInParent<InsertComponent>() != null) { continue; }
				var size = candidate.bounds.size;
				var area = size.x * size.z;
				if (area <= largest || !candidate.TryGetComponent<MeshFilter>(out var filter) || !InsertCoverUv.TryCreate(filter.sharedMesh, out _)) { continue; }
				best = candidate;
				largest = area;
			}
			return best;
		}

		private static bool IsCoverSource(MeshRenderer renderer)
		{
			var material = renderer.sharedMaterial;
			return renderer.enabled && renderer.gameObject.activeInHierarchy && material != null &&
				material.HasProperty("_NormalMap") && material.GetTexture("_NormalMap") != null;
		}

#if UNITY_EDITOR
		private bool _rebuildPending;
		private void OnValidate()
		{
			// OnValidate runs mid-serialization, where touching sibling components
			// is not allowed, so the rebuild waits for the next editor tick.
			if (_rebuildPending) { return; }
			_rebuildPending = true;
			UnityEditor.EditorApplication.delayCall += () => {
				if (this != null && _rebuildPending) {
					_rebuildPending = false;
					RebuildMesh();
				}
			};
		}
#endif
	}
}
