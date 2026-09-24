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
		[Min(0f)]
		[Tooltip("How far the rounding where the outer wall meets the floor reaches, up the wall and in across the floor, in VPX units. Zero leaves a sharp edge.")]
		private float _bottomBevel = 1f;

		[SerializeField]
		[Range(1, 16)]
		[Tooltip("How many steps the bottom bevel is drawn with. One is a flat chamfer.")]
		private int _bottomBevelSegments = 4;

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

		public void SetCoverMaterial(Material material)
		{
			_coverMaterial = material;
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
				_warnings = warnings.ToArray();
				Error = null;

				var mesh = GetOrCreateMesh();
				var layout = new InsertMeshLayout();
				InsertMeshGenerator.Generate(outline, body, mesh, layout);
				IsBakeFresh = _bake != null && _bake.TryApply(mesh, layout.Hash);
				WearBake();
				// Add the cover only after applying the body's bake. It must never
				// change the bake hash, atlas, or per-corner tangent mapping.
				var renderer = GetComponent<MeshRenderer>();
				if (_coverMaterial != null) {
					_coverFirstVertex = mesh.vertexCount;
					InsertMeshGenerator.AddCover(outline, mesh);
					_coverMapped = false;
					renderer.sharedMaterials = new[] { renderer.sharedMaterial, _coverMaterial };
					UpdateCover(true);
				} else if (renderer.sharedMaterials.Length > 1) {
					renderer.sharedMaterials = new[] { renderer.sharedMaterial };
				}

			} catch (InsertOutlineException exception) {
				Error = exception.Message;
			}
		}

		public InsertBakeSettings BakeSettings => _bakeSettings;

		/// <summary>
		/// Replaces the bake, or drops it with null, and rebuilds. The material the
		/// insert wore before is kept, to be put back when the bake is cleared.
		/// </summary>
		public void SetBake(InsertBakeData bake)
		{
			var unbaked = UnbakedMaterial;
			var renderer = GetComponent<MeshRenderer>();
			if (_bake != null && renderer.sharedMaterial == _bake.Material) {
				renderer.sharedMaterial = unbaked;
			}
			_bake = bake;
			_unbakedMaterial = bake != null ? unbaked : null;
			RebuildMesh();
		}

		/// <summary>
		/// Puts a fresh bake's material on in place of the original material.
		/// Geometry edits keep the material as a preview, and a material the
		/// user chose themselves is left alone.
		/// </summary>
		private void WearBake()
		{
			if (_bake == null || _bake.Material == null) {
				return;
			}
			var renderer = GetComponent<MeshRenderer>();
			if (IsBakeFresh && renderer.sharedMaterial == _unbakedMaterial) {
				renderer.sharedMaterial = _bake.Material;
			}
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

			// The bevel can climb no higher than the wall, and must leave some of the
			// channel's floor between it and the raised middle, or the two collide.
			var bottomBevel = math.min(_bottomBevel, _depth);
			if (channelWidth > InsertOutlineBuilder.Epsilon) {
				bottomBevel = math.min(bottomBevel, channelWidth - math.max(outline.WeldTolerance * 16f, channelWidth * 0.05f));
			}
			bottomBevel = outline.ClampInset(math.max(0f, bottomBevel));
			if (bottomBevel < _bottomBevel - 1e-4f) {
				warnings.Add($"Bottom bevel reduced to {bottomBevel:0.##} to fit the wall and the channel.");
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
				BottomBevel = bottomBevel,
				BottomBevelSegments = _bottomBevelSegments,
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
