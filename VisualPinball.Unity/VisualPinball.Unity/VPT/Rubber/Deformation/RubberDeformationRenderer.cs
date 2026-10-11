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
	/// Pushes the free spans of a guide-driven rubber out of line, so a slingshot's
	/// kick shows on the cord.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Every free span of the rubber's guided path gets one signed V profile: pinned at
	/// both supports, linear up to its apex and back. A slingshot drives it, either
	/// following its arm's position or, without an animated arm, played over
	/// <see cref="AnimationDurationMs"/> on <see cref="KickCurve"/>.
	/// </para>
	/// <para>
	/// The component never writes the shared mesh. On enable it takes an instance of
	/// the rubber's mesh, binds every vertex within one cord diameter of a span's
	/// rest chord to that chord, and restores the shared mesh on disable.
	/// </para>
	/// <para>
	/// <b>Clock.</b> The timed kick runs on the physics engine's simulation clock
	/// (<c>PhysicsEngine.TimeMsec</c>). Without a physics engine, the synthetic
	/// fixtures of the test suite, the clock falls back to accumulated frame time, and
	/// tests may inject their own through <see cref="ClockMsOverride"/>.
	/// </para>
	/// </remarks>
	[PackAs("RubberDeformationRenderer")]
	[AddComponentMenu("Pinball/Game Item/Rubber Deformation Renderer")]
	[DisallowMultipleComponent]
	public sealed class RubberDeformationRenderer : MonoBehaviour, IPackable
	{
		#region Constants

		public const float DefaultUpdateThreshold = 0.05f;

		/// <summary>Default length of the kick animation, in milliseconds.</summary>
		public const float DefaultAnimationDurationMs = 60f;

		/// <summary>Smallest and largest apex the profile accepts.</summary>
		internal const float MinimumApex = 0.02f;
		internal const float MaximumApex = 0.98f;

		#endregion

		#region Authoring

		[SerializeField]
		[Min(0f)]
		[Tooltip("Smallest deflection change in VPX units that rebuilds the render mesh. Zero updates every frame.")]
		private float _updateThreshold = DefaultUpdateThreshold;

		[SerializeField]
		[Tooltip("Recalculate mesh normals after displacing the vertices. Turning this off is cheaper but flattens the shading of a strongly deflected cord.")]
		private bool _recalculateNormals = true;

		[SerializeField]
		[Tooltip("Shape of the slingshot kick animation over its duration. Starts at zero, peaks at one and returns to zero.")]
		private AnimationCurve _kickCurve = CreateDefaultKickCurve();

		[SerializeField]
		[Min(0f)]
		[Tooltip("Total duration of the slingshot kick animation, in milliseconds.")]
		private float _animationDurationMs = DefaultAnimationDurationMs;

		public float UpdateThreshold {
			get => float.IsFinite(_updateThreshold) && _updateThreshold >= 0f
				? _updateThreshold
				: DefaultUpdateThreshold;
			set => _updateThreshold = float.IsFinite(value) && value >= 0f
				? value
				: DefaultUpdateThreshold;
		}

		public bool RecalculateNormals {
			get => _recalculateNormals;
			set => _recalculateNormals = value;
		}

		/// <summary>Shape of the kick animation, never null.</summary>
		public AnimationCurve KickCurve {
			get => _kickCurve ??= CreateDefaultKickCurve();
			set => _kickCurve = value ?? CreateDefaultKickCurve();
		}

		/// <summary>Length of the kick animation in milliseconds, sanitized.</summary>
		public float AnimationDurationMs {
			get => float.IsFinite(_animationDurationMs) && _animationDurationMs >= 0f
				? _animationDurationMs
				: DefaultAnimationDurationMs;
			set => _animationDurationMs = float.IsFinite(value) && value >= 0f
				? value
				: DefaultAnimationDurationMs;
		}

		/// <summary>
		/// An ease in-out that starts at zero, peaks at one halfway through and returns
		/// to zero, the shape the legacy slingshot animates on.
		/// </summary>
		public static AnimationCurve CreateDefaultKickCurve() => new AnimationCurve(
			new Keyframe(0f, 0f, 0f, 0f),
			new Keyframe(0.5f, 1f, 0f, 0f),
			new Keyframe(1f, 0f, 0f, 0f)
		);

		/// <summary>The deformation runtime ships with this component.</summary>
		public static bool RuntimeAvailable => true;

		#endregion

		#region Runtime state

		/// <summary>One free span, its rest chord and its profile.</summary>
		private struct SpanVisual
		{
			public int PathElementIndex;

			/// <summary>Rest chord of the span, in mesh space.</summary>
			public float3 RestStart;
			public float3 ChordDirection;
			public float ChordLength;

			/// <summary>Unit inward normal of the rest chord, in mesh space.</summary>
			public float3 Inward;

			/// <summary>Current profile: apex fraction and signed depth, inward positive.</summary>
			public float Apex;
			public float Depth;

			public float AppliedApex;
			public float AppliedDepth;

			public bool KickActive;
			public float KickApex;

			/// <summary>Signed kick amplitude, always outward, so never positive.</summary>
			public float KickAmplitude;
			public float KickStartMs;

			/// <summary>An outward push that follows an arm's position; zero when none.</summary>
			public float DrivenApex;
			public float DrivenDepth;
		}

		/// <summary>One render vertex tied to the rest chord of a span.</summary>
		internal struct VertexBinding
		{
			public int VertexIndex;
			public int SpanIndex;

			/// <summary>Arc-length fraction of the closest point on the rest chord.</summary>
			public float Alpha;

			/// <summary>Inward direction this vertex is displaced along, in mesh space.</summary>
			public float3 Inward;
		}

		/// <summary>Mesh vertex count and path hash the bindings were built for.</summary>
		private struct BindingFingerprint : IEquatable<BindingFingerprint>
		{
			public int VertexCount;
			public int PathHash;

			public bool Equals(BindingFingerprint other)
				=> VertexCount == other.VertexCount && PathHash == other.PathHash;
		}

		private static readonly SpanVisual[] NoSpans = Array.Empty<SpanVisual>();
		private static readonly VertexBinding[] NoBindings = Array.Empty<VertexBinding>();

		[NonSerialized] private RubberComponent _rubber;
		[NonSerialized] private MeshFilter _meshFilter;
		[NonSerialized] private PhysicsEngine _physicsEngine;

		[NonSerialized] private Mesh _sharedMesh;
		[NonSerialized] private Mesh _instancedMesh;
		[NonSerialized] private Vector3[] _restVertices;
		[NonSerialized] private List<Vector3> _vertexBuffer;

		[NonSerialized] private SpanVisual[] _spans = NoSpans;
		[NonSerialized] private VertexBinding[] _bindings = NoBindings;
		[NonSerialized] private BindingFingerprint _fingerprint;
		[NonSerialized] private bool _bindingsValid;

		[NonSerialized] private float _fallbackClockMs;
		[NonSerialized] private int _kickCount;
		[NonSerialized] private int _meshUpdateCount;
		[NonSerialized] private float _heldDeflection;

		/// <summary>
		/// A deflection in VPX units held on every bound span, inward positive, for
		/// diagnosing a cord that does not move. Zero releases it.
		/// </summary>
		internal float HeldDeflection {
			get => _heldDeflection;
			set => _heldDeflection = float.IsFinite(value) ? value : 0f;
		}

		/// <summary>Test seam for the simulation clock, in milliseconds.</summary>
		[NonSerialized] internal Func<float> ClockMsOverride;

		/// <summary>Simulation time the renderer animates on, in milliseconds.</summary>
		internal float NowMs {
			get {
				if (ClockMsOverride != null) {
					return ClockMsOverride();
				}
				return _physicsEngine ? _physicsEngine.TimeMsec : _fallbackClockMs;
			}
		}

		/// <summary>Number of free spans the bindings were built for.</summary>
		internal int SpanCount => _spans.Length;

		/// <summary>Number of render vertices bound to a span.</summary>
		internal int BindingCount => _bindings.Length;

		/// <summary>How many slingshot kicks started an outward push.</summary>
		internal int KickCount => _kickCount;

		/// <summary>How many times displaced vertices were written to the mesh instance.</summary>
		internal int MeshUpdateCount => _meshUpdateCount;

		/// <summary>The mesh instance the displacement is written to, null when inactive.</summary>
		internal Mesh InstancedMesh => _instancedMesh;

		/// <summary>The undisplaced vertices of the acquired mesh, null when none is acquired.</summary>
		internal IReadOnlyList<Vector3> RestVertices => _restVertices;

		internal IReadOnlyList<VertexBinding> Bindings => _bindings;

		#endregion

		#region Lifecycle

		private void OnEnable()
		{
			if (!Application.isPlaying) {
				return;
			}
			Acquire();
			AcquireMesh();
			RebuildBindings();
		}

		private void OnDisable()
		{
			_previewActive = false;
			ReleaseMesh();
			_spans = NoSpans;
			_bindings = NoBindings;
			_bindingsValid = false;
		}

		private void Update()
		{
			if (!Application.isPlaying) {
				return;
			}
			if (Time.deltaTime > 0f) {
				_fallbackClockMs += Time.deltaTime * 1000f;
			}
			RenderFrame();
		}

		/// <summary>Caches the components the renderer reads. Idempotent.</summary>
		internal void Acquire()
		{
			_rubber = GetComponent<RubberComponent>();
			_meshFilter = GetComponent<MeshFilter>();
			_physicsEngine = GetComponentInParent<PhysicsEngine>();
		}

		#endregion

		#region Mesh instance

		/// <summary>
		/// Replaces the shared mesh with a private instance. The shared mesh is kept and
		/// restored on disable, so nothing the editor or another rubber sees is touched.
		/// </summary>
		internal void AcquireMesh()
		{
			if (!_meshFilter) {
				_meshFilter = GetComponent<MeshFilter>();
			}
			if (!_meshFilter) {
				return;
			}
			var source = _meshFilter.sharedMesh;
			if (!source || source == _instancedMesh) {
				return;
			}
			ReleaseMesh();
			_sharedMesh = source;
			_instancedMesh = Instantiate(source);
			_instancedMesh.name = $"{source.name} (Deformed)";
			_instancedMesh.MarkDynamic();
			_meshFilter.sharedMesh = _instancedMesh;
			_restVertices = source.vertices;
			_vertexBuffer = new List<Vector3>(_restVertices.Length);
			for (var i = 0; i < _restVertices.Length; i++) {
				_vertexBuffer.Add(_restVertices[i]);
			}
			_bindingsValid = false;
		}

		internal void ReleaseMesh()
		{
			if (_instancedMesh) {
				if (_meshFilter && _meshFilter.sharedMesh == _instancedMesh) {
					_meshFilter.sharedMesh = _sharedMesh;
				}
				if (Application.isPlaying) {
					Destroy(_instancedMesh);
				} else {
					DestroyImmediate(_instancedMesh);
				}
			}
			_instancedMesh = null;
			_sharedMesh = null;
			_restVertices = null;
			_vertexBuffer = null;
		}

		#endregion

		#region Binding

		/// <summary>
		/// Binds every render vertex within one cord diameter of a free
		/// span's rest chord to that chord. Deterministic: vertices go to the nearest
		/// chord, ties to the span with the lower path element index.
		/// </summary>
		internal void RebuildBindings()
		{
			_spans = NoSpans;
			_bindings = NoBindings;
			_fingerprint = ComputeFingerprint();
			_bindingsValid = true;
			if (!_rubber || _rubber.PathSource != RubberPathSource.Guides) {
				return;
			}

			var path = _rubber.BakedPath;
			var frame = _rubber.BakeFrameToLocal;
			var insideSign = RubberPathMath.InsideSign(path);
			var spans = new List<SpanVisual>(path.Count);
			var polylines = new List<(float3[] Points, float[] Cumulative)>(path.Count);
			for (var pathElementIndex = 0; pathElementIndex < path.Count; pathElementIndex++) {
				var element = path[pathElementIndex];
				if (element.Type != RubberPathElementType.FreeSpan) {
					continue;
				}
				if (!TryBuildSpan(frame, element, insideSign, out var span)) {
					continue;
				}
				span.PathElementIndex = pathElementIndex;
				spans.Add(span);
				polylines.Add(BuildRestPolyline(frame, element));
			}
			_spans = spans.Count > 0 ? spans.ToArray() : NoSpans;

			if (_restVertices == null || _restVertices.Length == 0 || _spans.Length == 0) {
				return;
			}

			// One cord diameter, in mesh units. The tube's own radius is half of it, so
			// this covers the cord and the sliver of a neighbouring ring around it.
			var maximumDistance = Physics.ScaleToWorld(_rubber.Thickness);
			var bindings = new List<VertexBinding>(_restVertices.Length);
			for (var vertex = 0; vertex < _restVertices.Length; vertex++) {
				var position = (float3)_restVertices[vertex];
				var bestSpan = -1;
				var bestDistance = float.PositiveInfinity;
				var bestAlpha = 0f;
				for (var spanIndex = 0; spanIndex < _spans.Length; spanIndex++) {
					var polyline = polylines[spanIndex];
					var distance = NearestOnPolyline(polyline.Points, polyline.Cumulative,
						position, out var alpha);
					if (distance > maximumDistance || distance >= bestDistance) {
						continue;
					}
					bestSpan = spanIndex;
					bestDistance = distance;
					bestAlpha = alpha;
				}
				if (bestSpan < 0) {
					continue;
				}
				bindings.Add(new VertexBinding {
					VertexIndex = vertex,
					SpanIndex = bestSpan,
					Alpha = bestAlpha,
					Inward = _spans[bestSpan].Inward,
				});
			}
			_bindings = bindings.Count > 0 ? bindings.ToArray() : NoBindings;
		}

		/// <summary>
		/// The rest chord and inward normal of one free span, in mesh space. Mesh space
		/// is the rubber's own local Unity space: the mesh generator emits the tube in
		/// rubber-local VPX coordinates and <c>TransformToWorld</c> maps it with
		/// <c>VpxToWorld</c>, which is exactly what the bake frame's output needs.
		/// </summary>
		private static bool TryBuildSpan(Matrix4x4 frame, RubberPathElement element,
			int insideSign, out SpanVisual span)
		{
			span = default;
			var start = ToMeshSpace(frame, element.Start);
			var end = ToMeshSpace(frame, element.End);
			var delta = end - start;
			var length = math.length(delta);
			if (!(length > 1e-6f)) {
				return false;
			}
			var direction = delta / length;

			var bakeChord = element.End - element.Start;
			var bakeLength = math.length(bakeChord);
			if (!(bakeLength > 1e-6f)) {
				return false;
			}
			var tangent = bakeChord / bakeLength;
			var inward2 = insideSign >= 0
				? new float2(-tangent.y, tangent.x)
				: new float2(tangent.y, -tangent.x);
			var midpoint = (element.Start + element.End) * 0.5f;
			var inward = ToMeshSpace(frame, midpoint + inward2) - ToMeshSpace(frame, midpoint);
			inward -= direction * math.dot(inward, direction);
			var inwardLength = math.length(inward);
			if (!(inwardLength > 1e-6f)) {
				return false;
			}

			span = new SpanVisual {
				RestStart = start,
				ChordDirection = direction,
				ChordLength = length,
				Inward = inward / inwardLength,
			};
			return true;
		}

		/// <summary>
		/// The span's rest centreline in mesh space: the chord of a straight span, a
		/// sampled polyline of a rounded one, so the bulge of a rounded span binds too.
		/// </summary>
		private static (float3[] Points, float[] Cumulative) BuildRestPolyline(Matrix4x4 frame,
			RubberPathElement element)
		{
			var count = RubberPathMath.IsStraightFreeSpan(element)
				? 1
				: math.max(8, RubberPathMath.FreeSpanSubdivisionCount(element,
					RubberAutofit.RenderToleranceVpx));
			var points = new float3[count + 1];
			var cumulative = new float[count + 1];
			for (var i = 0; i <= count; i++) {
				points[i] = ToMeshSpace(frame, RubberPathMath.PointAt(element, i / (float)count));
				cumulative[i] = i == 0 ? 0f : cumulative[i - 1] + math.distance(points[i - 1], points[i]);
			}
			return (points, cumulative);
		}

		/// <summary>
		/// Distance from a point to a polyline, and the arc-length fraction of the
		/// nearest point on it.
		/// </summary>
		private static float NearestOnPolyline(float3[] points, float[] cumulative,
			float3 position, out float alpha)
		{
			var best = float.PositiveInfinity;
			alpha = 0f;
			var total = cumulative[cumulative.Length - 1];
			for (var i = 1; i < points.Length; i++) {
				var start = points[i - 1];
				var delta = points[i] - start;
				var lengthSq = math.lengthsq(delta);
				var t = lengthSq > 0f ? math.saturate(math.dot(position - start, delta) / lengthSq) : 0f;
				var distance = math.distance(position, start + delta * t);
				if (distance < best) {
					best = distance;
					alpha = total > 0f ? (cumulative[i - 1] + t * math.sqrt(lengthSq)) / total : 0f;
				}
			}
			return best;
		}

		/// <summary>A point of the bake plane in mesh space.</summary>
		private static float3 ToMeshSpace(Matrix4x4 frame, float2 point)
		{
			var localVpx = frame.MultiplyPoint3x4(new Vector3(point.x, point.y, 0f));
			return localVpx.TranslateToWorld();
		}

		private BindingFingerprint ComputeFingerprint()
		{
			var hash = 17;
			if (_rubber) {
				hash = hash * 31 + (int)_rubber.BakeVersion;
				hash = hash * 31 + _rubber.BakedPath.Count;
				hash = hash * 31 + _rubber.Thickness;
				hash = hash * 31 + (int)_rubber.PathSource;
				hash = hash * 31 + _rubber.BakeInputHash.GetHashCode();
			}
			return new BindingFingerprint {
				VertexCount = _restVertices?.Length ?? 0,
				PathHash = hash,
			};
		}

		/// <summary>
		/// One line per bound span for the inspector: how many render vertices the span
		/// drives and, so a mesh that sits off the chord can be told from one that is
		/// simply missing, how far the nearest render vertex is from the chord.
		/// </summary>
		internal IEnumerable<string> DescribeBindings()
		{
			if (_restVertices == null) {
				yield return "No render mesh acquired.";
				yield break;
			}
			if (_spans.Length == 0) {
				yield return "No free span to bind.";
				yield break;
			}
			var limit = _rubber ? _rubber.Thickness : 0f;
			for (var spanIndex = 0; spanIndex < _spans.Length; spanIndex++) {
				var span = _spans[spanIndex];
				var bound = 0;
				for (var i = 0; i < _bindings.Length; i++) {
					if (_bindings[i].SpanIndex == spanIndex) {
						bound++;
					}
				}
				var nearest = float.PositiveInfinity;
				for (var vertex = 0; vertex < _restVertices.Length; vertex++) {
					var position = (float3)_restVertices[vertex];
					var along = math.clamp(math.dot(position - span.RestStart, span.ChordDirection),
						0f, span.ChordLength);
					nearest = math.min(nearest,
						math.distance(position, span.RestStart + span.ChordDirection * along));
				}
				var nearestVpx = float.IsFinite(nearest) ? Physics.ScaleToVpx(nearest) : float.NaN;
				yield return $"Span {span.PathElementIndex}: depth {span.AppliedDepth:F2}, "
					+ $"{bound} vertices bound, nearest {nearestVpx:F1} VPX from the chord (limit {limit:F0})";
			}
		}

		#endregion

		#region Profile

		/// <summary>
		/// The V profile: pinned at both ends, linear up to the
		/// apex and linear back down, inward positive.
		/// </summary>
		internal static float ProfileAt(float alpha, float apex, float depth)
		{
			if (!float.IsFinite(alpha) || !float.IsFinite(apex) || !float.IsFinite(depth)) {
				return 0f;
			}
			alpha = math.saturate(alpha);
			apex = math.clamp(apex, MinimumApex, MaximumApex);
			return alpha <= apex
				? depth * alpha / apex
				: depth * (1f - alpha) / (1f - apex);
		}

		/// <summary>The current profile of a span, by its baked path element index.</summary>
		internal bool TryGetProfile(int pathElementIndex, out float apex, out float depth)
		{
			apex = 0f;
			depth = 0f;
			if (!TryGetSpanIndex(pathElementIndex, out var index)) {
				return false;
			}
			apex = _spans[index].Apex;
			depth = _spans[index].Depth;
			return true;
		}

		internal bool TryGetSpanIndex(int pathElementIndex, out int index)
		{
			for (var i = 0; i < _spans.Length; i++) {
				if (_spans[i].PathElementIndex == pathElementIndex) {
					index = i;
					return true;
				}
			}
			index = -1;
			return false;
		}

		#endregion

		#region Events

		/// <summary>
		/// Starts the outward kick animation of one span.
		/// <paramref name="outwardDepth"/> is a magnitude in VPX units; the profile is
		/// signed outward.
		/// </summary>
		internal void PlayKick(int pathElementIndex, float apexPathCoordinate01,
			float outwardDepth)
		{
			if (!TryGetSpanIndex(pathElementIndex, out var index)) {
				return;
			}
			if (!float.IsFinite(outwardDepth) || outwardDepth <= 0f
				|| AnimationDurationMs <= 0f) {
				return;
			}
			ref var span = ref _spans[index];
			span.KickActive = true;
			span.KickApex = math.clamp(apexPathCoordinate01, MinimumApex, MaximumApex);
			span.KickAmplitude = -outwardDepth;
			span.KickStartMs = NowMs;
			_kickCount++;
		}

		/// <summary>
		/// Holds the span at an outward push that follows an arm's position: applied as
		/// long as the depth is non-zero, overriding the timed kick.
		/// </summary>
		internal void DriveKick(int pathElementIndex, float apexPathCoordinate01,
			float outwardDepth)
		{
			if (!TryGetSpanIndex(pathElementIndex, out var index)) {
				return;
			}
			ref var span = ref _spans[index];
			var depth = float.IsFinite(outwardDepth) && outwardDepth > 0f ? -outwardDepth : 0f;
			if (span.DrivenDepth == 0f && depth != 0f) {
				_kickCount++;
			}
			span.DrivenApex = math.clamp(apexPathCoordinate01, MinimumApex, MaximumApex);
			span.DrivenDepth = depth;
			span.KickActive = false;
		}

		#endregion

		#region Preview

		[NonSerialized] private bool _previewActive;
		[NonSerialized] private int _previewPathElementIndex;
		[NonSerialized] private float _previewApex;
		[NonSerialized] private float _previewDepth;

		/// <summary>Whether an edit-mode preview currently poses the cord.</summary>
		internal bool IsPreviewing => _previewActive;

		/// <summary>
		/// Poses one span at a fixed profile outside play mode, so an author can align a
		/// slingshot's arm against the deflected cord without starting a game. Acquires
		/// the mesh instance the way play mode does; <see cref="EndPreview"/> hands the
		/// shared mesh back. Ignored while the game runs, where the simulation drives
		/// the cord.
		/// </summary>
		internal void Preview(int pathElementIndex, float apex01, float depth)
		{
			if (Application.isPlaying) {
				return;
			}
			Acquire();
			AcquireMesh();
			if (!_bindingsValid || !ComputeFingerprint().Equals(_fingerprint)) {
				RebuildBindings();
			}
			_previewActive = true;
			_previewPathElementIndex = pathElementIndex;
			_previewApex = apex01;
			_previewDepth = depth;
			RenderFrame();
		}

		/// <summary>Ends the edit-mode preview and hands the mesh filter its shared mesh back.</summary>
		internal void EndPreview()
		{
			if (!_previewActive) {
				return;
			}
			_previewActive = false;
			if (Application.isPlaying) {
				return;
			}
			ReleaseMesh();
			_spans = NoSpans;
			_bindings = NoBindings;
			_bindingsValid = false;
		}

		#endregion

		#region Frame

		/// <summary>
		/// One frame of animation: evaluate every span's profile and, when anything moved
		/// by more than <see cref="UpdateThreshold"/>, write the displaced vertices once.
		/// </summary>
		internal void RenderFrame()
		{
			if (_meshFilter && _meshFilter.sharedMesh
				&& _meshFilter.sharedMesh != _instancedMesh) {
				// the mesh arrived late or was replaced under us, for instance by a package
				// load that adds this component before the rubber has its mesh
				AcquireMesh();
				RebuildBindings();
			}
			if (!_bindingsValid || !ComputeFingerprint().Equals(_fingerprint)) {
				RebuildBindings();
			}
			if (_spans.Length == 0) {
				return;
			}

			var now = NowMs;
			var threshold = UpdateThreshold;
			var changed = false;
			for (var i = 0; i < _spans.Length; i++) {
				ref var span = ref _spans[i];
				Evaluate(ref span, now);
				if (span.Depth == 0f && span.AppliedDepth == 0f) {
					continue;
				}
				if ((span.Depth == 0f) != (span.AppliedDepth == 0f)
					|| math.abs(span.Depth - span.AppliedDepth) > threshold
					|| math.abs(span.Apex - span.AppliedApex) > 1e-3f) {
					changed = true;
				}
			}
			if (!changed) {
				return;
			}
			ApplyVertices();
		}

		/// <summary>
		/// The signed profile of one span: the preview, a held diagnostic deflection, the
		/// arm's push or the timed kick, in that order.
		/// </summary>
		private void Evaluate(ref SpanVisual span, float now)
		{
			if (_previewActive) {
				var previewed = span.PathElementIndex == _previewPathElementIndex;
				span.Apex = previewed ? _previewApex : 0f;
				span.Depth = previewed ? _previewDepth : 0f;
				return;
			}
			if (_heldDeflection != 0f) {
				span.Apex = 0.5f;
				span.Depth = _heldDeflection;
				return;
			}
			if (span.DrivenDepth != 0f) {
				span.Apex = span.DrivenApex;
				span.Depth = span.DrivenDepth;
				return;
			}
			if (span.KickActive) {
				var duration = AnimationDurationMs;
				var progress = duration > 0f ? (now - span.KickStartMs) / duration : 1f;
				if (progress >= 1f || progress < 0f) {
					span.KickActive = false;
				} else {
					span.Apex = span.KickApex;
					span.Depth = span.KickAmplitude * KickCurve.Evaluate(math.saturate(progress));
					return;
				}
			}

			span.Apex = 0f;
			span.Depth = 0f;
		}

		/// <summary>
		/// Writes the displaced vertices. Only bound vertices are touched, and the whole
		/// pass runs on the arrays allocated when the bindings were built.
		/// </summary>
		private void ApplyVertices()
		{
			if (_instancedMesh == null || _restVertices == null || _vertexBuffer == null) {
				return;
			}
			var scale = Physics.ScaleToWorld(1f);
			for (var i = 0; i < _bindings.Length; i++) {
				var binding = _bindings[i];
				var span = _spans[binding.SpanIndex];
				var rest = _restVertices[binding.VertexIndex];
				if (span.Depth == 0f) {
					_vertexBuffer[binding.VertexIndex] = rest;
					continue;
				}
				var offset = binding.Inward
					* (ProfileAt(binding.Alpha, span.Apex, span.Depth) * scale);
				_vertexBuffer[binding.VertexIndex] = new Vector3(
					rest.x + offset.x, rest.y + offset.y, rest.z + offset.z);
			}
			for (var i = 0; i < _spans.Length; i++) {
				_spans[i].AppliedApex = _spans[i].Apex;
				_spans[i].AppliedDepth = _spans[i].Depth;
			}
			_instancedMesh.SetVertices(_vertexBuffer);
			if (_recalculateNormals) {
				_instancedMesh.RecalculateNormals();
			}
			_instancedMesh.RecalculateBounds();
			_meshUpdateCount++;
		}

		#endregion

		#region Packaging

		public byte[] Pack() => RubberDeformationRendererPackable.Pack(this);

		public byte[] PackReferences(Transform root, PackagedRefs refs, PackagedFiles files)
			=> null;

		public void Unpack(byte[] bytes) => RubberDeformationRendererPackable.Unpack(bytes, this);

		public void UnpackReferences(byte[] data, Transform root, PackagedRefs refs,
			PackagedFiles files)
		{
		}

		#endregion
	}

	public struct RubberDeformationRendererPackable
	{
		public float UpdateThreshold;
		public bool RecalculateNormals;
		public AnimationCurve KickCurve;
		public float AnimationDurationMs;

		public static byte[] Pack(RubberDeformationRenderer component)
		{
			return PackageApi.Packer.Pack(new RubberDeformationRendererPackable {
				UpdateThreshold = component.UpdateThreshold,
				RecalculateNormals = component.RecalculateNormals,
				KickCurve = component.KickCurve,
				AnimationDurationMs = component.AnimationDurationMs,
			});
		}

		public static void Unpack(byte[] bytes, RubberDeformationRenderer component)
		{
			if (bytes == null || bytes.Length == 0) {
				return;
			}
			var data = PackageApi.Packer.Unpack<RubberDeformationRendererPackable>(bytes);
			component.UpdateThreshold = data.UpdateThreshold;
			component.RecalculateNormals = data.RecalculateNormals;
			if (data.KickCurve == null || data.KickCurve.length == 0) {
				component.KickCurve = RubberDeformationRenderer.CreateDefaultKickCurve();
				component.AnimationDurationMs =
					RubberDeformationRenderer.DefaultAnimationDurationMs;
				return;
			}
			component.KickCurve = data.KickCurve;
			component.AnimationDurationMs = data.AnimationDurationMs;
		}
	}
}
