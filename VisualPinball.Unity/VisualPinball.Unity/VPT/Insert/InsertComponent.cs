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
	[AddComponentMenu("Pinball/Insert")]
	[PackAs("Insert")]
	[ExecuteAlways]
	[RequireComponent(typeof(MeshFilter))]
	[RequireComponent(typeof(MeshRenderer))]
	public class InsertComponent : MonoBehaviour, IPackable, ISerializationCallbackReceiver
	{
		private static int _packageImportDepth;

		public static IDisposable DeferGenerationForPackageImport()
		{
			_packageImportDepth++;
			return new PackageImportScope();
		}

		private sealed class PackageImportScope : IDisposable
		{
			private bool _disposed;

			public void Dispose()
			{
				if (_disposed) { return; }
				_disposed = true;
				_packageImportDepth--;
			}
		}

		private const string MeshNamePrefix = "Insert (generated) ";
		private string MeshName => MeshNamePrefix + GetEntityId();

		#region Data

		[SerializeField]
		[Tooltip("The outline the insert is built from.")]
		private InsertShape _shape = InsertShape.Triangle;

		[SerializeField]
		[Tooltip("A readable mesh containing one flat, closed outline or a filled planar surface. Its local dimensions are in meters; the outline is centered and aligned to the insert plane.")]
		private Mesh _customMesh;

		[SerializeField]
		[Tooltip("Rounding applied to every corner of the custom outline. Zero preserves the source shape.")]
		private InsertCornerSettings _customCorner = new(0f, 8);

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
		[Tooltip("Mirrors the flash left to right, so the bolt runs the other way.")]
		private bool _flashMirror;

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
		[Tooltip("Diameter of the circle, in VPX units.")]
		private float _diameter = InsertShapes.VpxUnitsPerInch;

		[SerializeField]
		[Range(6, 128)]
		[Tooltip("How many straight sides the circle is drawn with, which is all the sampling a circle gets.")]
		private int _circleVertices = 32;

		[SerializeField]
		[Min(0.01f)]
		[Tooltip("Diameter of the circle the banana's band is bent round, in VPX units. The middle of the band follows it.")]
		private float _bananaDiameter = 510.3f;

		[SerializeField]
		[Min(0.01f)]
		[Tooltip("How thick the banana's band is, across its circle, in VPX units. Half of it reaches out either side of the circle.")]
		private float _bananaThickness = 38.3f;

		[SerializeField]
		[Range(1f, 180f)]
		[Tooltip("How far round its circle the banana reaches, in degrees. The circle's centre is above the band, so its ends turn up.")]
		private float _bananaAngle = 27.4f;

		[SerializeField]
		[Range(2, 128)]
		[Tooltip("How many straight sides each of the banana's two arcs is drawn with.")]
		private int _bananaSegments = 11;

		[SerializeField]
		[Range(2, 32)]
		[Tooltip("How many straight sides each of the banana's rounded ends is drawn with.")]
		private int _bananaEndSegments = 8;

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
		[Tooltip("Whether the raised floor carries parallel ribs. Off keeps it flat.")]
		private bool _ribbed;

		[SerializeField]
		[Range(-180f, 180f)]
		[Tooltip("The direction the ribs run, in degrees from the insert's X axis: zero runs them along X, ninety along Y.")]
		private float _ribAngle = 90f;

		[SerializeField]
		[Min(0.2f)]
		[Tooltip("Ridge to ridge, in VPX units. A ridge runs through the insert's origin.")]
		private float _ribWidth = 4f;

		[SerializeField]
		[Min(0f)]
		[Tooltip("How far the ribs' ridges rise above and grooves drop below the floor, in VPX units.")]
		private float _ribOffset = 1.5f;

		[SerializeField]
		[Range(0f, 1f)]
		[Tooltip("How far the ribs' ridges and grooves are rounded over: zero leaves them sharp creases, one makes them roll over like a moulded wave.")]
		private float _ribRoundness = 0.5f;

		[SerializeField]
		[Tooltip("Whether the raised floor carries concentric rings. Off keeps it flat.")]
		private bool _ringed;

		[SerializeField]
		[Tooltip("Where the rings' centre sits relative to the insert's origin, in VPX units.")]
		private Vector2 _ringCenter = Vector2.zero;

		[SerializeField]
		[Min(0.2f)]
		[Tooltip("Ridge to ridge, in VPX units. The centre is a ridge, the first groove a half width out.")]
		private float _ringWidth = 4f;

		[SerializeField]
		[Min(0f)]
		[Tooltip("How far the rings' ridges rise above and grooves drop below the floor, in VPX units.")]
		private float _ringOffset = 1.5f;

		[SerializeField]
		[Range(0f, 1f)]
		[Tooltip("How far the rings' ridges and grooves are rounded over: zero leaves them sharp creases, one makes them roll over like a moulded wave.")]
		private float _ringRoundness = 0.5f;

		[SerializeField]
		[Min(0f)]
		[Tooltip("How far out from the centre the rings start, in VPX units, leaving a flat disc in the middle at the ridges' height. Rounded to whole widths, so the rings lie where they would without the disc.")]
		private float _ringStart;

		[SerializeField]
		[Tooltip("Whether the raised floor is cut like a gem: a flat table with rows of triangular facets zigzagging out to the edge. Off keeps it flat.")]
		private bool _gem;

		[SerializeField]
		[Tooltip("Where the gem's centre sits relative to the insert's origin, in VPX units.")]
		private Vector2 _gemCenter = Vector2.zero;

		[SerializeField]
		[Range(4, 16)]
		[Tooltip("How many sides the table has, and kites round it. A brilliant has eight.")]
		private int _gemSides = 8;

		[SerializeField]
		[Range(0.05f, 0.9f)]
		[Tooltip("How far out the table's corners lie, as a share of the way from the centre to the edge.")]
		private float _gemTable = 0.5f;

		[SerializeField]
		[Min(0f)]
		[Tooltip("How far out each further row of facets starts beyond the last, from the table's sides, in VPX units. Zero runs one row of facets straight from the table to the edge; a length adds a ring of facets every that far out, as a brilliant's crown has.")]
		private float _gemRowLength;

		[SerializeField]
		[Range(0f, 1f)]
		[Tooltip("How evenly the facets meet the edge. At zero the lines from two neighbouring corners meet the edge at one point; at one every line meets it as far from the next as from the last, and the facet off each side runs out to the edge as a trapezoid.")]
		private float _gemSpread = 1f;

		[SerializeField]
		[Min(0f)]
		[Tooltip("How far the table rises above and the girdle drops below the floor, in VPX units.")]
		private float _gemOffset = 3f;

		[SerializeField]
		[Tooltip("Whether the flat surface carries a grid of square pyramids for the normal bake.")]
		private bool _stippled;

		[SerializeField]
		[Tooltip("How far each stipple's apex rises above the flat surface, in VPX units. Negative sinks the stipples into the surface instead.")]
		private float _stippleHeight = 1f;

		[SerializeField]
		[Min(0.01f)]
		[Tooltip("The length of each side of a stipple's square base, in VPX units.")]
		private float _stippleWidth = 4f;

		[SerializeField]
		[Min(0f)]
		[Tooltip("The flat space between adjacent stipple bases, in VPX units.")]
		private float _stippleMargin = 0.5f;

		[SerializeField]
		[Tooltip("Only stipples that fit on the surface whole are built. Off builds every stipple the surface reaches and cuts it off at the outline.")]
		private bool _stippleWhole = true;

		[SerializeField]
		[Tooltip("How far the grid of stipples is moved from centred on the surface, in VPX units.")]
		private Vector2 _stippleOffset = Vector2.zero;

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

		[SerializeField, HideInInspector]
		private bool _usesPackagedMesh;

		#endregion

		#region Generated state

		[NonSerialized]
		private Mesh _generatedMesh;

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

		/// <summary>How the engraved line's own complaints start, told apart from the outline's.</summary>
		public const string EngravedLineWarning = "Engraved line: ";

		/// <summary>
		/// Why the last rebuild failed, or null. The previous mesh is kept when a
		/// rebuild fails, so dragging a slider through an impossible value does not
		/// blank the object out.
		/// </summary>
		public string Error { get; private set; }

		/// <summary>The mesh supplying a custom outline, separate from the generated insert mesh.</summary>
		public Mesh CustomMesh
		{
			get => _customMesh;
			set { _customMesh = value; RebuildMesh(); }
		}

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

		/// <summary>The exact material saved before the current bake, including when another is worn now.</summary>
		public Material PreBakeMaterial => _unbakedMaterial;

		/// <summary>Completes an editor import after its generator and bake sidecar has been loaded.</summary>
		public void RestoreAuthoring(InsertBakeData bake, Material preBakeMaterial)
		{
			_bake = bake;
			_unbakedMaterial = preBakeMaterial;
			var slots = GetComponent<MeshRenderer>().sharedMaterials;
			_coverMaterial = slots.Length > 1 ? slots[1] : null;
			_coverSlotRead = true;
			_usesPackagedMesh = false;
			RebuildMesh();
		}

		#endregion

		public void RebuildMesh()
		{
			if (_packageImportDepth > 0) { return; }
			if (_usesPackagedMesh) {
				UsePackagedMesh();
				return;
			}
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
				var plainStipple = body.StippleHeight != 0f && _bake != null
					&& _bake.PlainStipple && _bake.Fits(layout.Hash);
				if (plainStipple) {
					var plainBody = body;
					plainBody.StippleHeight = 0f;
					InsertMeshGenerator.Generate(outline, plainBody, mesh);
					IsBakeFresh = _bake.TryApply(mesh, layout.Hash);
					if (!IsBakeFresh) {
						InsertMeshGenerator.Generate(outline, body, mesh, layout);
					}
				} else {
					IsBakeFresh = body.StippleHeight == 0f && _bake != null && _bake.TryApply(mesh, layout.Hash);
				}
				// Bake-only relief never appears in the finished mesh. Stipples bake
				// against the whole plain body; an engraved line replaces its middle.
				IsMiddleFlat = IsBakeFresh && (plainStipple || InsertMeshGenerator.ReplaceMiddle(mesh, layout));
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
				UpdateBakePreview(renderer);
#if UNITY_EDITOR
				_gizmoEdges = IsShowingGeometry ? layout.PreviewEdges : null;
#endif

			} catch (InsertOutlineException exception) {
				Error = exception.Message;
			}
		}

		/// <summary>
		/// Whether the middle is the plain flat island, with engraved relief or
		/// stipples carried by a fitting bake's maps.
		/// </summary>
		public bool IsMiddleFlat { get; private set; }

		/// <summary>
		/// Whether the insert shows its geometry in place of the bake's maps:
		/// while the bake is stale, the maps no longer lie on the geometry, so
		/// the renderer is off and the generated mesh is drawn as a gizmo in
		/// the scene instead, yellow and see-through with its feature edges over it,
		/// the way a collider preview is. Only in the editor, outside play mode.
		/// </summary>
		public bool IsShowingGeometry { get; private set; }

		/// <summary>
		/// The renderer is off while the bake is stale and on otherwise, set from
		/// the bake's state on every rebuild and remembered nowhere: an undo
		/// puts the geometry back and the rebuild that follows puts the renderer
		/// back with it, where a flag on the component would be undone without
		/// the renderer, and leave the insert gone. The insert owns its
		/// renderer's on and off, as it owns its mesh. In play mode and in a
		/// build the renderer is on whatever the bake's state.
		/// </summary>
		private void UpdateBakePreview(MeshRenderer renderer)
		{
			IsShowingGeometry = _bake != null && !IsBakeFresh && !Application.isPlaying;
			if (renderer.enabled == IsShowingGeometry) {
				renderer.enabled = !IsShowingGeometry;
			}
		}

#if UNITY_EDITOR
		/// <summary>The fill of the geometry gizmo, yellow and see-through.</summary>
		private static readonly Color GizmoFill = new(1f, 0.85f, 0.1f, 0.3f);

		/// <summary>The quad and contour wires of the geometry gizmo, drawn solid over the fill.</summary>
		private static readonly Color GizmoWire = new(1f, 0.85f, 0.1f, 1f);

		[NonSerialized] private Vector3[] _gizmoEdges;

		/// <summary>
		/// The generated mesh in the insert's place while its bake is stale: the
		/// body as a see-through yellow fill under its quad and contour wires, the way
		/// a collider preview draws, so the geometry to be baked is what is
		/// seen, and not maps that no longer lie on it.
		/// </summary>
		private void OnDrawGizmos()
		{
			if (!IsShowingGeometry || !isActiveAndEnabled) {
				return;
			}
			var mesh = GetComponent<MeshFilter>().sharedMesh;
			if (mesh == null || mesh.vertexCount == 0) {
				return;
			}
			Gizmos.matrix = transform.localToWorldMatrix;
			Gizmos.color = GizmoFill;
			Gizmos.DrawMesh(mesh, 0, Vector3.zero, Quaternion.identity, Vector3.one);
			Gizmos.color = GizmoWire;
			if (_gizmoEdges != null) {
				Gizmos.DrawLineList(_gizmoEdges);
			}
			Gizmos.matrix = Matrix4x4.identity;
		}
#endif

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
		public InsertBody Generate(Mesh target, InsertMeshLayout layout, bool withoutStipples = false)
		{
			var warnings = new List<string>();
			var outline = BuildOutline(warnings);
			var body = BuildBody(outline, warnings);
			if (withoutStipples) { body.StippleHeight = 0f; }
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
				InsertShape.Banana => InsertShapes.BananaCorners(_bananaDiameter, _bananaThickness, _bananaAngle, _bananaSegments, _bananaEndSegments, out arcCenter),
				InsertShape.Diamond => InsertShapes.DiamondCorners(_width, _height, _diamondSide, _diamondTip, _diamondShear, warnings),
				InsertShape.Flash => InsertShapes.FlashCorners(_flashWidth, _flashHeight, _flashTip, _flashNotch, _flashStep, _flashTipCorner, _flashSpikeCorner, _flashNotchCorner, warnings, _flashMirror),
				InsertShape.Bullet => InsertShapes.BulletCorners(_width, _height, _bulletTip, _bulletShoulder, _bulletShoulderCorner, _bulletBaseCorner, warnings),
				InsertShape.Arrow => InsertShapes.ArrowCorners(_width, _height, _arrowTip, _arrowNotchDepth, _arrowNotchWidth, _arrowFootAngle, _arrowNotchCorner, _arrowInnerCorner, _arrowFootCorner, warnings),
				InsertShape.Custom => InsertCustomShape.Corners(_customMesh, _customCorner),
				_ => throw new InsertOutlineException($"Shape {_shape} has no outline builder yet."),
			};
			ArcCenter = new Vector2(arcCenter.x, arcCenter.y);
			if (_shape == InsertShape.Custom && _customMesh != null && !_customMesh.isReadable) {
				// The editor read it, a build could not: a build rebuilds its
				// inserts as they come in.
				warnings.Add("The source mesh's Read/Write is off: it works here, but a build cannot read it. Enable Read/Write in its import settings.");
			}
			return InsertOutlineBuilder.Build(corners);
		}

		/// <summary>
		/// The engraved line: the outline eroded by the channel's width
		/// and the line's inset, which closes the flat ends up into points and
		/// widens the notches, built into an outline of its own, its corners
		/// sharp where the erosion sharpened them.
		/// </summary>
		private InsertGroove BuildGroove(InsertOutline outline, float channelWidth, List<string> warnings)
		{
			if (_shape is not InsertShape.Flash and not InsertShape.Custom || !_flashGroove || _stippled) {
				return null;
			}
			var half = math.max(_flashGrooveWidth, 0.1f) * 0.5f;
			var inset = channelWidth + math.max(_flashGrooveInset, half + 0.01f);
			// Sharp all the way: a tip the erosion closes up is a point, and the
			// band's edges meet there in mitres, as the engraver's line does.
			var corners = InsertShapes.Erode(outline, inset, 0f, warnings);
			if (corners == null) {
				warnings.Add("The engraved line does not fit inside the insert: bring its inset down.");
				return null;
			}
			InsertOutline line;
			try {
				line = InsertOutlineBuilder.Build(corners, center: false);
			} catch (InsertOutlineException e) {
				warnings.Add($"The engraved line cannot be drawn: {e.Message}");
				return null;
			}
			foreach (var warning in line.Warnings) {
				warnings.Add(EngravedLineWarning + warning);
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
			if (groove != null && (_starburst || _ribbed || _ringed || _gem)) {
				warnings.Add("The engraved line takes the pattern's place: the starburst, ribs, rings or gem are not built while the line is on.");
			}
			var starburstBursts = _starburst && groove == null && !_stippled ? _starburstBursts : 0;
			var ribWidth = _ribbed && groove == null && !_stippled && starburstBursts < InsertStarburst.MinBursts ? math.max(_ribWidth, InsertRibs.MinWidth) : 0f;
			var ringWidth = _ringed && groove == null && !_stippled && starburstBursts < InsertStarburst.MinBursts && ribWidth <= 0f ? math.max(_ringWidth, InsertRings.MinWidth) : 0f;

			// Stipples rise no higher than the rim, and sink no deeper than the
			// surface they are cut into stands above what lies below it.
			var clearance = math.max(outline.WeldTolerance * 4f, 0.01f);
			var stippleHeight = 0f;
			if (_stippled) {
				stippleHeight = _stippleHeight < 0f
					? -math.min(math.max(0.01f, -_stippleHeight), math.max(0f, (channel ? channelDepth : depth) - clearance))
					: math.min(math.max(0.01f, _stippleHeight), math.max(0f, depth - channelDepth - clearance));
			}

			var body = new InsertBody {
				Depth = depth,
				ChannelWidth = channelWidth,
				ChannelDepth = channelDepth,
				Groove = groove,
				StarburstBursts = starburstBursts,
				StarburstCenter = new float2(_starburstCenter.x, _starburstCenter.y),
				StarburstOffset = _starburstOffset,
				RibWidth = ribWidth,
				RibAngle = _ribAngle,
				RibOffset = _ribOffset,
				RibRoundness = _ribRoundness,
				RingWidth = ringWidth,
				RingCenter = new float2(_ringCenter.x, _ringCenter.y),
				RingOffset = _ringOffset,
				RingRoundness = _ringRoundness,
				RingStart = _ringStart,
				GemSides = _gem && groove == null && !_stippled && starburstBursts < InsertStarburst.MinBursts && ribWidth <= 0f && ringWidth <= 0f ? math.clamp(_gemSides, InsertGem.MinSides, InsertGem.MaxSides) : 0,
				GemCenter = new float2(_gemCenter.x, _gemCenter.y),
				GemTable = _gemTable,
				GemRowLength = _gemRowLength,
				GemSpread = _gemSpread,
				GemOffset = _gemOffset,
				StippleHeight = stippleHeight,
				StippleWidth = math.max(0.01f, _stippleWidth),
				StippleMargin = math.max(0f, _stippleMargin),
				StippleCut = !_stippleWhole,
				StippleOffset = new float2(_stippleOffset.x, _stippleOffset.y),
				Holes = channel ? _holes.ToArray() : null,
				HoleSegments = _holeSegments,
				BottomBevel = bottomBevel,
				BottomBevelSegments = _bottomBevelSegments,
			};
			if (_stippled && math.abs(body.StippleHeight) < math.abs(_stippleHeight) - 1e-4f) {
				warnings.Add(body.StippleHeight < 0f
					? $"Stipple depth reduced to {-body.StippleHeight:0.##} to stay within the surface."
					: $"Stipple height reduced to {body.StippleHeight:0.##} to stay below the rim.");
			}

			if (body.HasRelief) {
				// The zigzag straddles the floor, so it can reach no further down than
				// what lies below it, nor further up than the rim. It is the size that
				// is held: a negative offset only swaps which creases are ridges, and
				// reaches just as far.
				var limit = body.StarburstLimit();
				var asked = body.StarburstBursts >= InsertStarburst.MinBursts ? _starburstOffset : body.RibWidth > 0f ? _ribOffset : body.RingWidth > 0f ? _ringOffset : _gemOffset;
				var held = math.clamp(asked, -limit, limit);
				if (body.StarburstBursts >= InsertStarburst.MinBursts) {
					body.StarburstOffset = held;
				} else if (body.RibWidth > 0f) {
					body.RibOffset = held;
				} else if (body.RingWidth > 0f) {
					body.RingOffset = held;
				} else {
					body.GemOffset = held;
				}
				if (math.abs(held) < math.abs(asked) - 1e-4f) {
					var pattern = body.StarburstBursts >= InsertStarburst.MinBursts ? "Starburst" : body.RibWidth > 0f ? "Rib" : body.RingWidth > 0f ? "Ring" : "Gem";
					warnings.Add(channel
						? $"{pattern} offset reduced to {held:0.##} to stay between the channel floor and the rim."
						: $"{pattern} offset reduced to {held:0.##} to stay below the rim.");
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
			if (OwnsMesh(mesh)) {
				// One saved into the scene before generated meshes stopped being saved.
				mesh.hideFlags |= HideFlags.DontSave;
				if (_generatedMesh != mesh) { ReleaseGeneratedMesh(); }
				_generatedMesh = mesh;
				return mesh;
			}

			ReleaseGeneratedMesh();
			// Never saved: the insert rebuilds it from its own fields whenever it is
			// enabled, so a copy in the scene is only dead weight, and one left behind
			// by an earlier session, whose name no longer matches, was never cleaned up.
			mesh = new Mesh { name = MeshName, hideFlags = HideFlags.DontSave };
			_generatedMesh = mesh;
			filter.sharedMesh = mesh;
			return mesh;
		}

		/// <summary>
		/// Keeps a generated mesh that an older version saved into the scene from
		/// being saved again. An inactive insert never rebuilds, so it still holds
		/// one; it is built again the next time the insert is enabled or exported.
		/// </summary>
		internal void UnsaveLegacyMesh() => UnsaveLegacyMesh(GetComponent<MeshFilter>()?.sharedMesh);

		/// <summary>
		/// The same for a mesh held elsewhere, like a prefab instance's override
		/// that still points at the old mesh after the insert rebuilt its own.
		/// </summary>
		internal static void UnsaveLegacyMesh(Mesh mesh)
		{
			if (mesh != null && !IsAsset(mesh) && mesh.name.StartsWith(MeshNamePrefix, StringComparison.Ordinal)) {
				mesh.hideFlags |= HideFlags.DontSave;
			}
		}

		private bool OwnsMesh(Mesh mesh) => mesh != null && mesh.name == MeshName && !IsAsset(mesh);

		private void ReleaseGeneratedMesh()
		{
			var mesh = _generatedMesh;
			_generatedMesh = null;
			if (!OwnsMesh(mesh)) { return; }
			if (Application.isPlaying) { Destroy(mesh); } else { DestroyImmediate(mesh); }
		}

		private static bool IsAsset(Mesh mesh)
		{
#if UNITY_EDITOR
			return UnityEditor.EditorUtility.IsPersistent(mesh);
#else
			return false;
#endif
		}

		/// <summary>
		/// The version of the generator's fields. A banana once took the circle's
		/// diameter; data saved before it had its own has no version, and its banana
		/// keeps the diameter it was made with.
		/// </summary>
		[SerializeField, HideInInspector]
		private int _version;

		private const int Version = 1;

		void ISerializationCallbackReceiver.OnBeforeSerialize() => _version = Version;

		void ISerializationCallbackReceiver.OnAfterDeserialize()
		{
			if (_version < 1) {
				_bananaDiameter = _diameter;
			}
			_version = Version;
		}

		private void Reset()
		{
			if (_packageImportDepth == 0) { RebuildMesh(); }
		}
		private void OnEnable()
		{
			if (_packageImportDepth > 0) { return; }
#if UNITY_EDITOR
			ClaimId();
#endif
			if (_usesPackagedMesh) {
				UsePackagedMesh();
				return;
			}
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
		private void LateUpdate()
		{
			if (!_usesPackagedMesh) { UpdateCover(false); }
		}

		private void UsePackagedMesh()
		{
			var renderer = GetComponent<MeshRenderer>();
			var slots = renderer.sharedMaterials;
			_coverMaterial = slots.Length > 1 ? slots[1] : null;
			_coverSlotRead = true;
			UpdateCover(true);
		}

		public byte[] Pack() => InsertPackable.Pack(this);

		public byte[] PackReferences(Transform root, PackagedRefs refs, PackagedFiles files)
			=> InsertPackable.PackReferences(this, refs);

		public void Unpack(byte[] bytes) => InsertPackable.Unpack(bytes, this);

		public void UnpackReferences(byte[] bytes, Transform root, PackagedRefs refs, PackagedFiles files)
			=> InsertPackable.UnpackReferences(bytes, this, refs);

		internal void UseImportedMesh(bool value) => _usesPackagedMesh = value;

		internal void RestoreCoverState(bool usePlayfieldNormals, MeshRenderer playfield)
		{
			_usePlayfieldNormals = usePlayfieldNormals;
			_coverPlayfield = playfield;
		}

		public void PrepareForPackageExport()
		{
			// An inactive insert has not been enabled since the scene loaded, so it has
			// no mesh yet: it is built now, for the export to find.
			if (!_usesPackagedMesh && !OwnsMesh(GetComponent<MeshFilter>().sharedMesh)) {
				RebuildMesh();
			}
			UpdateCover(true);
		}

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

#endif

		private void OnDestroy()
		{
#if UNITY_EDITOR
			if (!string.IsNullOrEmpty(_id) && Ids.TryGetValue(_id, out var holder) && holder == this) {
				Ids.Remove(_id);
			}
#endif
			// An inactive insert may not have reclaimed its mesh after a domain reload.
			if (_generatedMesh == null && !_usesPackagedMesh) {
				var filter = GetComponent<MeshFilter>();
				if (filter != null && OwnsMesh(filter.sharedMesh)) { _generatedMesh = filter.sharedMesh; }
			}
			ReleaseGeneratedMesh();
		}

		private void UpdateCover(bool force)
		{
			CoverWarning = null;
			if (_coverMaterial == null) { return; }
			var renderer = GetComponent<MeshRenderer>();
			var mesh = GetComponent<MeshFilter>().sharedMesh;
			if (mesh == null || mesh.subMeshCount < 2) { return; }
			if (!_usePlayfieldNormals) {
				ClearCoverProjection(renderer, mesh, force);
				ApplyCoverThickness(renderer);
				return;
			}
			if (!_coverMaterial.HasProperty("_CoverNormalMap") || !_coverMaterial.HasProperty("_PlayfieldNormalStrength")) {
				CoverWarning = "Use a Pinball/Insert Cover material for playfield normals, or disable Use Playfield Normals to use this material unchanged.";
				ClearCoverProjection(renderer, mesh, force);
				ApplyCoverThickness(renderer);
				return;
			}
			if (force && _coverPlayfield == null) {
				_autoCoverPlayfield = FindCoverPlayfield();
			}
			var playfield = CoverPlayfield;
			if (playfield == null) {
				CoverWarning = "No playfield surface found in this table. Assign a Playfield Override or disable Use Playfield Normals.";
				ClearCoverProjection(renderer, mesh, force);
				ApplyCoverThickness(renderer);
				return;
			}
			if (!_usesPackagedMesh) {
				var sourceMesh = playfield.TryGetComponent<MeshFilter>(out var filter) ? filter.sharedMesh : null;
#if UNITY_EDITOR
				if (MakeReadable(sourceMesh)) {
					// The cover maps again once the model is reimported.
					ClearCoverProjection(renderer, mesh, force);
					ApplyCoverThickness(renderer);
					return;
				}
#endif
				_coverProjectionValid = InsertCoverUv.TryCreate(sourceMesh, out _coverProjection, out var version) && playfield != renderer;
				force |= sourceMesh != _coverSourceMesh || version != _coverProjectionVersion;
				_coverSourceMesh = sourceMesh;
				_coverProjectionVersion = version;
			} else {
				_coverProjectionValid = playfield != renderer;
			}
			var source = playfield.sharedMaterial;
			if (!_coverProjectionValid || source == null || !source.HasProperty("_NormalMap") ||
				(source.HasProperty("_UVBase") && source.GetFloat("_UVBase") != 0f) ||
				(source.HasProperty("_NormalMapSpace") && source.GetFloat("_NormalMapSpace") != 0f)) {
				CoverWarning = "The cover needs a readable playfield mesh with planar XZ UV0 and a tangent-space normal map.";
				ClearCoverProjection(renderer, mesh, force);
				ApplyCoverThickness(renderer);
				return;
			}
			var texture = source.GetTexture("_NormalMap");
			if (texture == null) {
				CoverWarning = "The playfield material has no normal map. The cover uses its own material settings.";
				ClearCoverProjection(renderer, mesh, force);
				ApplyCoverThickness(renderer);
				return;
			}
			// HDRP Lit uses the base map's UV transform for its normal map as well.
			var slot = source.HasProperty("_BaseColorMap") ? "_BaseColorMap" : "_NormalMap";
			var scale = source.GetTextureScale(slot);
			var offset = source.GetTextureOffset(slot);
			var matrix = playfield.transform.worldToLocalMatrix * transform.localToWorldMatrix;
			if (!_usesPackagedMesh && (force || !_coverMapped || matrix != _coverToPlayfield || scale != _coverTextureScale || offset != _coverTextureOffset)) {
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
			_coverProperties.SetFloat(CoverThicknessId, CoverThickness);
			renderer.SetPropertyBlock(_coverProperties, 1);
			_coverPropertiesApplied = true;
			_coverThicknessApplied = true;
		}

		private static readonly int CoverThicknessId = Shader.PropertyToID("_CoverThickness");
		private bool _coverThicknessApplied;

		/// <summary>
		/// How deep the plastic is under the cover, in millimetres: from the
		/// rim down to the raised middle's surface, which is what shows through.
		/// </summary>
		public float CoverThickness => Outline != null
			? math.max(0.1f, -InsertMeshGenerator.ToLocal(float2.zero, -Body.Depth + Body.ChannelDepth).y * 1000f)
			: _packagedCoverThickness > 0f ? _packagedCoverThickness : 4f;

		// A packaged insert keeps its GLB mesh and has no outline to measure: the thickness comes with it.
		private float _packagedCoverThickness;

		internal void RestorePackagedCoverThickness(float millimetres)
		{
			_packagedCoverThickness = millimetres;
			_coverThicknessApplied = false;
		}

		/// <summary>
		/// Writes the slab's thickness into the cover's property block, once
		/// after the block was cleared or the geometry changed: the cover
		/// refracts what shows through it as through a slab that deep.
		/// </summary>
		private void ApplyCoverThickness(MeshRenderer renderer)
		{
			if (_coverThicknessApplied || _coverMaterial == null || !_coverMaterial.HasProperty(CoverThicknessId)) {
				return;
			}
			_coverProperties ??= new MaterialPropertyBlock();
			renderer.GetPropertyBlock(_coverProperties, 1);
			_coverProperties.SetFloat(CoverThicknessId, CoverThickness);
			renderer.SetPropertyBlock(_coverProperties, 1);
			_coverThicknessApplied = true;
		}

		private void ClearCoverProjection(MeshRenderer renderer, Mesh mesh, bool force)
		{
			if (_coverPropertiesApplied || force) {
				renderer.SetPropertyBlock(null, 1);
				_coverPropertiesApplied = false;
				_coverThicknessApplied = false;
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
			Mesh unreadable = null;
			var unreadableArea = 0f;
			foreach (var candidate in playfield.GetComponentsInChildren<MeshRenderer>(true)) {
				if (!IsCoverSource(candidate) || candidate.GetComponentInParent<InsertComponent>() != null) { continue; }
				var size = candidate.bounds.size;
				var area = size.x * size.z;
				if (area <= largest) { continue; }
				if (!_usesPackagedMesh) {
					var candidateMesh = candidate.TryGetComponent<MeshFilter>(out var filter) ? filter.sharedMesh : null;
					if (candidateMesh != null && !candidateMesh.isReadable) {
						// Its UVs can't be checked until it's readable.
						if (area > unreadableArea) {
							unreadable = candidateMesh;
							unreadableArea = area;
						}
						continue;
					}
					if (!InsertCoverUv.TryCreate(candidateMesh, out _)) { continue; }
				}
				best = candidate;
				largest = area;
			}
#if UNITY_EDITOR
			// The board is usually the largest surface: read it, and search again once it's reimported.
			if (unreadableArea > largest) {
				MakeReadable(unreadable);
			}
#endif
			return best;
		}

		private static bool IsCoverSource(MeshRenderer renderer)
		{
			var material = renderer.sharedMaterial;
			return renderer.enabled && renderer.gameObject.activeInHierarchy && material != null &&
				material.HasProperty("_NormalMap") && material.GetTexture("_NormalMap") != null;
		}

#if UNITY_EDITOR
		// Model paths waiting to become readable, with the inserts to rebuild once they are.
		private static readonly Dictionary<string, HashSet<InsertComponent>> ReadablePending = new();

		/// <summary>
		/// Turns on Read/Write for the imported model a cover needs to read, then
		/// rebuilds every insert that asked. Returns whether the mesh is on its way
		/// to being readable.
		/// </summary>
		private bool MakeReadable(Mesh mesh)
		{
			if (mesh == null || mesh.isReadable || UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode) { return false; }
			var path = UnityEditor.AssetDatabase.GetAssetPath(mesh);
			if (string.IsNullOrEmpty(path) || UnityEditor.AssetImporter.GetAtPath(path) is not UnityEditor.ModelImporter importer || importer.isReadable) {
				return false;
			}
			if (ReadablePending.TryGetValue(path, out var waiting)) {
				waiting.Add(this);
				return true;
			}
			ReadablePending[path] = new HashSet<InsertComponent> { this };
			// Not mid-update or mid-serialization: reimport on the next editor tick.
			UnityEditor.EditorApplication.delayCall += () => {
				ReadablePending.Remove(path, out var inserts);
				// Never reimport while the game runs; the inserts ask again back in edit mode.
				if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode) { return; }
				if (importer != null && !importer.isReadable) {
					importer.isReadable = true;
					importer.SaveAndReimport();
				}
				foreach (var insert in inserts) {
					if (insert != null) { insert.RebuildMesh(); }
				}
			};
			return true;
		}

		private bool _rebuildPending;
		private void OnValidate()
		{
			if (_usesPackagedMesh) { return; }
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
