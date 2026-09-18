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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.Common;

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// Result of one deformation bake, before it is written to the component.
	/// </summary>
	public sealed class RubberDeformationBakeResult
	{
		public RubberDeformationBakeOutcome Outcome = RubberDeformationBakeOutcome.Invalid;
		public string Reason = RubberColliderComponent.NotBakedReason;
		public RubberCompiledDeformationMaterial Material;
		public RubberSpanDescriptor[] Spans = Array.Empty<RubberSpanDescriptor>();
		public RubberBackedResponseDescriptor[] BackedResponses
			= Array.Empty<RubberBackedResponseDescriptor>();
		public string Hash = string.Empty;
		public int BakeVersion;
	}

	/// <summary>
	/// Compiles the SI deformation material and the baked guided path of a Physical
	/// rubber into the descriptors the deformable free-span runtime consumes.
	/// </summary>
	/// <remarks>
	/// Everything here runs in the editor and is serialized on the collider
	/// component, so the runtime never sees an <c>AnimationCurve</c>, never converts
	/// units and never solves geometry. The pure computations — unit conversion,
	/// modal coefficients, inward sign, tessellation certification and hashing — are
	/// public so the test assembly can exercise them without a scene.
	/// </remarks>
	public static class RubberDeformationBaker
	{
		#region Constants

		/// <summary>Version of this compiler, part of the bake hash.</summary>
		public const int CompilerVersion = RubberDeformationBakeFormat.CompilerVersion;

		/// <summary>
		/// Version of the serialized bake layout. It lives in the runtime assembly so
		/// collider generation can decide staleness without the editor.
		/// </summary>
		public const int BakeVersion = RubberDeformationBakeFormat.BakeVersion;

		/// <summary>
		/// Why a rubber with rounded corners cannot use the compliant rebound.
		/// </summary>
		public const string RoundnessFallbackReason
			= "compliant rebound requires straight free spans in this version";

		/// <summary>Kilograms per VPE mass unit: one 80 g ball is mass one.</summary>
		public const float MassUnitKg = 0.08f;

		/// <summary>Smallest certified chain resolution.</summary>
		public const int MinimumSegmentCount = 8;

		/// <summary>Largest certified chain resolution.</summary>
		public const int MaximumSegmentCount = 64;

		/// <summary>Certified resolutions are multiples of this.</summary>
		public const int SegmentCountStep = 4;

		/// <summary>Chord error budget of the rest curve, in VPX units.</summary>
		public const float MaximumRestChordErrorVpx = 0.05f;

		/// <summary>Chord error budget of the fully deflected chain, in VPX units.</summary>
		public const float MaximumChainChordErrorVpx = 0.25f;

		/// <summary>Installed stretch below which a span counts as slack.</summary>
		public const float MinimumInstalledStretch = 1.001f;

		/// <summary>
		/// A segment whose minimum tangent speed falls below this fraction of the
		/// element mean is degenerate and cannot be certified.
		/// </summary>
		public const float DegenerateTangentFraction = 1e-3f;

		/// <summary>Free-span length below which a span counts as zero length, in VPX units.</summary>
		public const float MinimumSpanLengthVpx = 1e-3f;

		/// <summary>Samples used to build the arc-length parametrization of one element.</summary>
		public const int ArcLengthSampleCount = 4096;

		private const float TwoPi = 2f * math.PI;

		#endregion

		#region Units

		/// <summary>VPX units per meter, <c>S</c> in the plan's section 2.2.</summary>
		public static float VpxPerMeter => Physics.ScaleToVpx(1f);

		/// <summary>Seconds per VPE time unit, <c>t0</c> in the plan's section 2.2.</summary>
		public static float StepTimeS => PhysicsConstants.DefaultStepTimeS;

		public static float LengthToVpx(float meters) => meters * VpxPerMeter;

		public static float LengthToMeters(float vpx) => vpx / VpxPerMeter;

		/// <summary><c>M_vpe = M_kg / m0</c></summary>
		public static float MassToVpe(float kilograms) => kilograms / MassUnitKg;

		/// <summary><c>K_vpe = K_N_per_m * t0^2 / m0</c></summary>
		public static float StiffnessToVpe(float newtonsPerMeter)
			=> newtonsPerMeter * StepTimeS * StepTimeS / MassUnitKg;

		/// <summary><c>C_vpe = C_Ns_per_m * t0 / m0</c></summary>
		public static float DampingToVpe(float newtonSecondsPerMeter)
			=> newtonSecondsPerMeter * StepTimeS / MassUnitKg;

		/// <summary><c>F_vpe = F_N * S * t0^2 / m0</c></summary>
		public static float ForceToVpe(float newtons)
			=> newtons * VpxPerMeter * StepTimeS * StepTimeS / MassUnitKg;

		/// <summary>Inverse of <see cref="ForceToVpe"/>, for inspector readouts.</summary>
		public static float ForceToNewtons(float vpe)
			=> vpe * MassUnitKg / (VpxPerMeter * StepTimeS * StepTimeS);

		/// <summary>
		/// Stress converts as force over area: <c>sigma_vpe = sigma_Pa * t0^2 / (m0 * S)</c>,
		/// so that <c>sigma_vpe * A0_vpx</c> is the tension in VPE force units.
		/// </summary>
		public static float StressToVpe(float pascals)
			=> pascals * StepTimeS * StepTimeS / (MassUnitKg * VpxPerMeter);

		/// <summary>Inverse of <see cref="StressToVpe"/>, for inspector readouts.</summary>
		public static float StressToPascals(float vpe)
			=> vpe * MassUnitKg * VpxPerMeter / (StepTimeS * StepTimeS);

		/// <summary><c>mu_vpe = mu_kg_per_m / (m0 * S)</c></summary>
		public static float LinearDensityToVpe(float kilogramsPerMeter)
			=> kilogramsPerMeter / (MassUnitKg * VpxPerMeter);

		/// <summary>
		/// Free undamped frequency of a lane in hertz. The VPE angular frequency is
		/// per VPE time unit, so dividing by <see cref="StepTimeS"/> gives radians per
		/// second.
		/// </summary>
		public static float FrequencyHz(float modalStiffnessVpe, float modalMassVpe)
		{
			if (!(modalStiffnessVpe > 0f) || !(modalMassVpe > 0f)) {
				return 0f;
			}
			return math.sqrt(modalStiffnessVpe / modalMassVpe) / (TwoPi * StepTimeS);
		}

		/// <summary>Cross-section area of the cord in square VPX units.</summary>
		public static float CrossSectionAreaVpx(float thicknessVpx)
			=> math.PI * thicknessVpx * thicknessVpx * 0.25f;

		#endregion

		#region Material compilation

		/// <summary>
		/// Compiles an SI deformation material asset plus the rubber's cord thickness
		/// into the VPE-unit record the runtime reads.
		/// </summary>
		public static bool TryCompileMaterial(RubberPhysicsMaterialAsset asset, float thicknessVpx,
			out RubberCompiledDeformationMaterial material, out string error)
		{
			material = default;
			error = null;
			if (!asset) {
				error = "No deformation material is assigned.";
				return false;
			}
			if (!float.IsFinite(thicknessVpx) || thicknessVpx <= 0f) {
				error = "The rubber thickness must be positive.";
				return false;
			}
			if (!float.IsFinite(asset.DensityKgPerCubicMeter) || asset.DensityKgPerCubicMeter <= 0f) {
				error = "The deformation material density must be positive.";
				return false;
			}
			if (!float.IsFinite(asset.ModalDampingRatio) || asset.ModalDampingRatio <= 0f
				|| asset.ModalDampingRatio >= 1f) {
				error = "The deformation material damping ratio must be between zero and one, exclusive.";
				return false;
			}
			if (!float.IsFinite(asset.MaximumStretchRatio) || asset.MaximumStretchRatio <= 1f) {
				error = "The deformation material maximum stretch ratio must be greater than one.";
				return false;
			}
			var curve = asset.NominalStressMpaByStretchRatio;
			if (curve == null || curve.keys.Length == 0) {
				error = "The deformation material has no stress curve.";
				return false;
			}

			var areaM2 = math.PI * math.pow(LengthToMeters(thicknessVpx) * 0.5f, 2f);
			var count = RubberCompiledDeformationMaterial.LutSampleCount;
			var stress = new float[count];
			var tangent = new float[count];
			var maximumStretch = asset.MaximumStretchRatio;
			var step = (maximumStretch - 1f) / (count - 1f);
			for (var i = 0; i < count; i++) {
				var stretch = math.lerp(1f, maximumStretch, i / (count - 1f));
				stress[i] = StressToVpe(curve.Evaluate(stretch) * 1e6f);
				// Central difference on the authored curve. AnimationCurve exposes no
				// analytic derivative, and the table is read by linear interpolation
				// anyway, so a half-step difference matches the sampled resolution.
				var half = step * 0.5f;
				var lower = math.max(1f, stretch - half);
				var upper = math.min(maximumStretch, stretch + half);
				var span = upper - lower;
				tangent[i] = span > 0f
					? StressToVpe((curve.Evaluate(upper) - curve.Evaluate(lower)) * 1e6f) / span
					: 0f;
			}
			for (var i = 0; i < count; i++) {
				if (float.IsFinite(stress[i]) && float.IsFinite(tangent[i])) {
					continue;
				}
				error = "The deformation material produced non-finite stress samples.";
				return false;
			}

			var linearDensity = LinearDensityToVpe(asset.DensityKgPerCubicMeter * areaM2);
			if (!float.IsFinite(linearDensity) || linearDensity <= 0f) {
				error = "The deformation material density must be positive.";
				return false;
			}
			material = new RubberCompiledDeformationMaterial {
				CompilerVersion = CompilerVersion,
				LinearDensity = linearDensity,
				DampingRatio = asset.ModalDampingRatio,
				SurfaceFriction = math.max(0f, asset.BallSurfaceFriction),
				MaximumStretchRatio = maximumStretch,
				StressLut = stress,
				TangentStiffnessLut = tangent,
			};
			return true;
		}

		#endregion

		#region Geometry

		/// <summary>
		/// Signed area of the whole baked path, sampled as a polyline at each
		/// element's geometric subdivision count. Positive means counter-clockwise in
		/// the bake plane.
		/// </summary>
		public static float ComputeSignedArea(IReadOnlyList<RubberPathElement> path,
			float cordRadiusVpx)
		{
			if (path == null || path.Count == 0) {
				return 0f;
			}
			var tolerance = RubberPhysicalColliderGenerator.ChordTolerance(cordRadiusVpx);
			var area = 0.0;
			foreach (var element in path) {
				var count = SubdivisionCount(element, tolerance);
				var previous = RubberPathMath.PointAt(element, 0f);
				for (var i = 1; i <= count; i++) {
					var next = RubberPathMath.PointAt(element, i / (float)count);
					area += (double)previous.x * next.y - (double)next.x * previous.y;
					previous = next;
				}
			}
			return (float)(area * 0.5);
		}

		/// <summary>
		/// Orientation of the inward deflection direction of the whole rubber:
		/// <c>+1</c> when the loop interior is to the left of the rest tangent.
		/// </summary>
		public static sbyte ComputeInwardSign(IReadOnlyList<RubberPathElement> path,
			float cordRadiusVpx)
		{
			var area = ComputeSignedArea(path, cordRadiusVpx);
			// A degenerate, zero-area path has no interior. The bake fails on its
			// zero-length spans first; keeping +1 makes this branch deterministic.
			return area < 0f ? (sbyte)-1 : (sbyte)1;
		}

		/// <summary>
		/// The number of polyline samples the static Physical generator would use for
		/// this element.
		/// </summary>
		public static int SubdivisionCount(RubberPathElement element, float tolerance)
		{
			if (element.Type == RubberPathElementType.SupportedArc) {
				var maximumAngle = RubberPhysicalColliderGenerator.MaximumChordAngle(
					element.Radius, tolerance);
				return math.max(3, (int)math.ceil(math.abs(element.SweepAngleRad) / maximumAngle));
			}
			return math.max(1, RubberPathMath.FreeSpanSubdivisionCount(element, tolerance));
		}

		/// <summary>
		/// Curve parameters of <paramref name="segmentCount"/> + 1 points spaced
		/// uniformly in arc length along a free span.
		/// </summary>
		/// <remarks>
		/// The arc-length parametrization is built by sampling the cubic at
		/// <see cref="ArcLengthSampleCount"/> uniform parameter steps, accumulating the
		/// chord lengths and inverting the monotone table by binary search plus linear
		/// interpolation. At 4096 samples the chord sum of a 150 unit span is within a
		/// few micro-units of the true length, well under the 0.05 unit chord budget
		/// the certificate enforces.
		/// </remarks>
		public static float[] BuildArcLengthFractions(RubberPathElement element, int segmentCount)
		{
			var table = new ArcLengthTable(element);
			return table.BuildFractions(segmentCount);
		}

		/// <summary>
		/// Largest single-mode amplitude in VPX units at which the deformed span
		/// reaches <paramref name="maximumStretchRatio"/>.
		/// </summary>
		public static float StretchLimitedDeflection(float installedLength,
			float installedStretch, float maximumStretchRatio)
		{
			if (!(installedLength > 0f) || !(installedStretch > 0f)
				|| !(maximumStretchRatio > installedStretch)) {
				return 0f;
			}
			var unloaded = installedLength / installedStretch;
			var target = unloaded * maximumStretchRatio;
			if (target <= installedLength) {
				return 0f;
			}
			var high = installedLength;
			while (DeflectedLength(installedLength, high) < target
				&& high < installedLength * 1024f) {
				high *= 2f;
			}
			var low = 0f;
			for (var i = 0; i < 60; i++) {
				var middle = (low + high) * 0.5f;
				if (DeflectedLength(installedLength, middle) < target) {
					low = middle;
				} else {
					high = middle;
				}
			}
			return (low + high) * 0.5f;
		}

		/// <summary>
		/// Arc length of the single-mode deflected span
		/// <c>s -&gt; (s, amplitude * sin(pi * s / length))</c>, by composite Simpson.
		/// </summary>
		public static float DeflectedLength(float length, float amplitude)
		{
			if (!(length > 0f)) {
				return 0f;
			}
			const int intervals = 512;
			var k = global::System.Math.PI / length;
			var sum = 0.0;
			for (var i = 0; i <= intervals; i++) {
				var s = length * (i / (double)intervals);
				var slope = amplitude * k * global::System.Math.Cos(k * s);
				var value = global::System.Math.Sqrt(1.0 + slope * slope);
				var weight = i == 0 || i == intervals ? 1.0 : i % 2 == 1 ? 4.0 : 2.0;
				sum += weight * value;
			}
			return (float)(sum * (length / (double)intervals) / 3.0);
		}

		#endregion

		#region Tessellation certificate

		/// <summary>
		/// Finds the smallest certified chain resolution for a free span, per the
		/// plan's section 3.2.
		/// </summary>
		/// <remarks>
		/// <para>
		/// The displaced curve is affine in amplitude, <c>P(s; A) = R(s) + A * D(s)</c>
		/// with <c>D(s) = d(s) * sin(pi * s / L)</c>, so the chord interpolation error
		/// at the cap is bounded by <c>E_R + cap * E_D</c>. Each part uses the standard
		/// chord bound <c>E &lt;= (delta_s)^2 / 8 * max|P''(s)|</c> per segment.
		/// </para>
		/// <para>
		/// For the rest curve, <c>|R''(s)|</c> is the curvature. It is bounded by
		/// <c>max|B''(t)| / min|B'(t)|^2</c> over the segment. The cubic's second
		/// derivative is linear in <c>t</c>, so its maximum norm is the larger of the
		/// two endpoint norms. The tangent speed is bounded below through the
		/// hodograph: the quadratic Bezier <c>B'</c> is restricted to the segment's
		/// parameter range by its blossom, and <c>|B'| &gt;= dot(B', u)</c> for any unit
		/// <c>u</c>, which the convex hull property bounds below by the smallest control
		/// point projection. A segment where that bound is not positive falls back to a
		/// sampled minimum, and a segment whose minimum tangent speed falls under
		/// <see cref="DegenerateTangentFraction"/> of the element mean is degenerate.
		/// </para>
		/// <para>
		/// For the displacement shape, the Frenet relations in the bake plane give
		/// <c>d' = -kappa * T</c> and <c>d'' = -kappa' * T - kappa^2 * d</c>, so
		/// <c>|D''| &lt;= (|kappa'| + kappa^2) + 2 * kappa * (pi / L) + (pi / L)^2</c>.
		/// The curvature derivative has no cheap analytic bound on a cubic, so it is
		/// bounded numerically over the segment with a factor-two safety margin; this
		/// is the one non-analytic term of the certificate and is flagged in the plan's
		/// section 13 for review. A straight span has <c>kappa = 0</c>, so the bound
		/// collapses to <c>(pi / L)^2</c> and the certified count is the plan's
		/// <c>K &gt;= pi * sqrt(cap / 2)</c> estimate exactly.
		/// </para>
		/// </remarks>
		public static bool TryCertifySegmentCount(RubberPathElement element, float maxDeflection,
			out int segmentCount, out float restError, out float displacementError,
			out string error)
		{
			segmentCount = 0;
			restError = float.PositiveInfinity;
			displacementError = float.PositiveInfinity;
			error = null;
			if (element.Type != RubberPathElementType.FreeSpan) {
				error = "Only free spans are tessellated for deformation.";
				return false;
			}
			if (!float.IsFinite(maxDeflection) || maxDeflection <= 0f) {
				error = "The deflection cap must be positive.";
				return false;
			}
			var table = new ArcLengthTable(element);
			if (!(table.TotalLength > MinimumSpanLengthVpx)) {
				error = "The span has zero length.";
				return false;
			}
			if (table.MinimumTangentSpeed
				< DegenerateTangentFraction * table.MeanTangentSpeed) {
				error = "The span has a degenerate tangent and cannot be certified.";
				return false;
			}

			var seed = (int)math.ceil(math.PI * math.sqrt(maxDeflection * 0.5f));
			var candidate = math.clamp(RoundUpToStep(seed), MinimumSegmentCount,
				MaximumSegmentCount);
			for (; candidate <= MaximumSegmentCount; candidate += SegmentCountStep) {
				if (!table.TryBoundErrors(candidate, out var rest, out var displacement)) {
					error = "The span has a degenerate tangent and cannot be certified.";
					return false;
				}
				restError = rest;
				displacementError = displacement;
				if (rest <= MaximumRestChordErrorVpx
					&& rest + maxDeflection * displacement <= MaximumChainChordErrorVpx) {
					segmentCount = candidate;
					return true;
				}
			}
			error = $"The span could not be tessellated within the chord budget at {MaximumSegmentCount} segments.";
			return false;
		}

		/// <summary>Convenience overload that drops the measured error bounds.</summary>
		public static bool TryCertifySegmentCount(RubberPathElement element, float maxDeflection,
			out int segmentCount, out string error)
			=> TryCertifySegmentCount(element, maxDeflection, out segmentCount, out _, out _,
				out error);

		private static int RoundUpToStep(int value)
		{
			if (value <= MinimumSegmentCount) {
				return MinimumSegmentCount;
			}
			var remainder = value % SegmentCountStep;
			return remainder == 0 ? value : value + (SegmentCountStep - remainder);
		}

		#endregion

		#region Bake

		/// <summary>
		/// Computes a bake without writing anything. Pure with respect to the scene.
		/// </summary>
		public static RubberDeformationBakeResult Compute(RubberColliderComponent collider)
		{
			var result = new RubberDeformationBakeResult();
			if (!collider) {
				result.Reason = "No rubber collider component.";
				return result;
			}
			result.Hash = ComputeBakeHash(collider);
			result.BakeVersion = BakeVersion;

			var rubber = collider.MainComponent;
			if (!rubber || !collider.CanUsePhysical) {
				result.Reason = "The rubber has no usable serialized guided path.";
				return result;
			}
			var path = rubber.BakedPath;
			var thickness = (float)rubber.Thickness;
			if (!TryCompileMaterial(collider.RubberPhysicsMaterial, thickness,
				out var material, out var materialError)) {
				result.Reason = materialError;
				return result;
			}
			result.Material = material;
			result.BackedResponses = BuildBackedResponses(collider, path);

			var freeSpans = new List<int>();
			for (var i = 0; i < path.Count; i++) {
				if (path[i].Type == RubberPathElementType.FreeSpan) {
					freeSpans.Add(i);
				}
			}
			foreach (var index in freeSpans) {
				if (path[index].Length > MinimumSpanLengthVpx) {
					continue;
				}
				result.Reason = $"Free span {index} has zero length.";
				return result;
			}

			if (freeSpans.Count == 0) {
				return StaticFallback(result, "The baked path has no free spans.");
			}
			if (collider.IsKinematic) {
				return StaticFallback(result,
					"The rubber is kinematic; deformation does not support moving rubbers.");
			}
			if (rubber.Roundness > 0f) {
				return StaticFallback(result,
					$"The rubber has rounded corners; {RoundnessFallbackReason}.");
			}
			var fittedLength = path.Sum(element => element.Length);
			if (!(rubber.RestLength > 0f)) {
				return StaticFallback(result,
					"The rest length is not configured. Use Set Rest Length From Fitted Length on the rubber.");
			}
			var installedStretch = fittedLength / rubber.RestLength;
			if (!float.IsFinite(installedStretch) || installedStretch < MinimumInstalledStretch) {
				return StaticFallback(result,
					$"The installed stretch of {installedStretch.ToString("0.####", CultureInfo.InvariantCulture)} is slack; deformation needs at least {MinimumInstalledStretch.ToString("0.###", CultureInfo.InvariantCulture)}.");
			}
			if (installedStretch > material.MaximumStretchRatio) {
				result.Reason = $"The installed stretch of {installedStretch.ToString("0.####", CultureInfo.InvariantCulture)} exceeds the material maximum of {material.MaximumStretchRatio.ToString("0.###", CultureInfo.InvariantCulture)}.";
				return result;
			}
			if (!IsMaterialDomainValid(in material, installedStretch, out var domainError)) {
				result.Reason = domainError;
				return result;
			}
			var stress = material.SampleStress(installedStretch);

			var inwardSign = ComputeInwardSign(path, thickness * 0.5f);
			var area = CrossSectionAreaVpx(thickness);
			var tension = stress * area;
			var tangentStiffness = material.SampleTangentStiffness(installedStretch) * area;
			var deflectionClearance = collider.DeflectionClearance;
			var spans = new List<RubberSpanDescriptor>(freeSpans.Count);
			foreach (var index in freeSpans) {
				var element = path[index];
				var installedLength = element.Length;
				var unloadedLength = installedLength / installedStretch;
				var modalMass = material.LinearDensity * unloadedLength * 0.5f;
				var stretchLimited = StretchLimitedDeflection(installedLength, installedStretch,
					material.MaximumStretchRatio);
				var maxDeflection = math.min(deflectionClearance, stretchLimited);
				if (!(maxDeflection > 0f) || !float.IsFinite(maxDeflection)) {
					result.Reason = $"Free span {index} has no usable deflection cap.";
					return result;
				}

				var stiffness = float4.zero;
				var damping = float4.zero;
				for (var n = 1; n <= 4; n++) {
					var k = tension * (n * math.PI) * (n * math.PI) / (2f * installedLength);
					stiffness[n - 1] = k;
					damping[n - 1] = 2f * material.DampingRatio * math.sqrt(k * modalMass);
				}
				var mass = new float4(modalMass);
				if (!math.all(math.isfinite(stiffness)) || !math.all(math.isfinite(damping))
					|| !float.IsFinite(modalMass) || modalMass <= 0f || stiffness.x <= 0f) {
					result.Reason = $"Free span {index} produced non-finite coefficients.";
					return result;
				}

				if (!TryCertifySegmentCount(element, maxDeflection, out var segmentCount,
					out var certificateError)) {
					return StaticFallback(result, $"Free span {index}: {certificateError}");
				}

				spans.Add(new RubberSpanDescriptor {
					PathElementIndex = index,
					StartBindingIndex = element.StartBindingIndex,
					EndBindingIndex = element.EndBindingIndex,
					InstalledLength = installedLength,
					UnloadedLength = unloadedLength,
					InstalledTension = tension,
					TangentStiffness = tangentStiffness,
					ModalMass = mass,
					ModalStiffness = stiffness,
					ModalDamping = damping,
					MaxDeflection = maxDeflection,
					SurfaceFriction = material.SurfaceFriction,
					InwardSign = inwardSign,
					SegmentCount = segmentCount,
					ArcLengthFractions = BuildArcLengthFractions(element, segmentCount),
					ReboundEligible = RubberPathMath.IsStraightFreeSpan(element),
				});
			}

			result.Spans = spans.ToArray();
			result.Outcome = RubberDeformationBakeOutcome.Deformable;
			result.Reason = $"{spans.Count} free span{(spans.Count == 1 ? string.Empty : "s")} compiled at an installed stretch of {installedStretch.ToString("0.###", CultureInfo.InvariantCulture)}.";
			return result;
		}

		/// <summary>
		/// The compliant rebound's material domain: the stress table must be
		/// non-negative everywhere, and the tension must be strictly positive from the
		/// installed stretch up to the material's maximum stretch, because that is the
		/// range the massless-string surrogate reads while the cord is loaded.
		/// </summary>
		/// <remarks>
		/// A zero at stretch one is correct and is what the default material has, so the
		/// table is only required to be non-negative below the installed stretch.
		/// </remarks>
		public static bool IsMaterialDomainValid(in RubberCompiledDeformationMaterial material,
			float installedStretch, out string error)
		{
			error = null;
			var lut = material.StressLut;
			if (lut == null || lut.Length != RubberCompiledDeformationMaterial.LutSampleCount) {
				error = "The deformation material has no compiled stress table.";
				return false;
			}
			var stress = material.SampleStress(installedStretch);
			if (!float.IsFinite(stress) || stress <= 0f) {
				error = "The nominal stress at the installed stretch is not positive.";
				return false;
			}
			for (var i = 0; i < lut.Length; i++) {
				if (material.StretchRatioAt(i) < installedStretch) {
					continue;
				}
				if (float.IsFinite(lut[i]) && lut[i] > 0f) {
					continue;
				}
				error = $"The nominal stress is not positive at a stretch ratio of {material.StretchRatioAt(i).ToString("0.####", CultureInfo.InvariantCulture)}; the compliant rebound needs a strictly positive tension from the installed stretch to the material maximum.";
				return false;
			}
			for (var i = 0; i < lut.Length; i++) {
				if (float.IsFinite(lut[i]) && lut[i] >= 0f) {
					continue;
				}
				error = $"The nominal stress is negative at a stretch ratio of {material.StretchRatioAt(i).ToString("0.####", CultureInfo.InvariantCulture)}.";
				return false;
			}
			return true;
		}

		/// <summary>
		/// Recomputes the bake hash and compares it with the one stored on the
		/// component. This is the source of truth for bake staleness.
		/// </summary>
		public static bool IsBakeCurrent(RubberColliderComponent collider)
		{
			if (!collider) {
				return false;
			}
			var stored = collider.DeformationBakeHash;
			return stored.Length > 0
				&& collider.DeformationBakeVersion == BakeVersion
				&& stored == ComputeBakeHash(collider);
		}

		/// <summary>Bakes and writes the result, recording one undo step.</summary>
		public static RubberDeformationBakeResult Bake(RubberColliderComponent collider,
			string undoName = "Bake Rubber Deformation")
		{
			if (!collider) {
				return new RubberDeformationBakeResult();
			}
			Undo.RecordObject(collider, undoName);
			return Apply(collider);
		}

		/// <summary>
		/// Re-bakes the rubber's collider after its guided path changed, but only when
		/// deformation is switched on. Called from the rubber rebake path, which has
		/// already registered its own undo step.
		/// </summary>
		public static void RebakeIfEnabled(RubberComponent rubber)
		{
			if (!rubber || !rubber.TryGetComponent<RubberColliderComponent>(out var collider)) {
				return;
			}
			if (!collider.DeformationEnabled || collider.Mode != RubberColliderMode.Physical) {
				return;
			}
			Apply(collider);
		}

		private static RubberDeformationBakeResult Apply(RubberColliderComponent collider)
		{
			var result = Compute(collider);
			collider.ApplyDeformationBake(result.Material, result.Spans, result.BackedResponses,
				result.Outcome, result.Reason, result.BakeVersion, result.Hash);
			EditorUtility.SetDirty(collider);
			PrefabUtility.RecordPrefabInstancePropertyModifications(collider);
			return result;
		}

		private static RubberDeformationBakeResult StaticFallback(
			RubberDeformationBakeResult result, string reason)
		{
			result.Outcome = RubberDeformationBakeOutcome.StaticFallback;
			result.Reason = reason;
			result.Spans = Array.Empty<RubberSpanDescriptor>();
			return result;
		}

		/// <summary>
		/// One backed response per guide binding referenced by the baked path,
		/// resolved exactly as <c>ColliderComponent.GetPhysicsMaterialData</c> resolves
		/// it for the static Physical stream today.
		/// </summary>
		public static RubberBackedResponseDescriptor[] BuildBackedResponses(
			RubberColliderComponent collider, IReadOnlyList<RubberPathElement> path)
		{
			if (!collider || path == null) {
				return Array.Empty<RubberBackedResponseDescriptor>();
			}
			var arcByBinding = new Dictionary<int, int>();
			var bindings = new SortedSet<int>();
			for (var i = 0; i < path.Count; i++) {
				var element = path[i];
				bindings.Add(element.StartBindingIndex);
				bindings.Add(element.EndBindingIndex);
				if (element.Type == RubberPathElementType.SupportedArc
					&& !arcByBinding.ContainsKey(element.StartBindingIndex)) {
					arcByBinding.Add(element.StartBindingIndex, i);
				}
			}

			var settings = collider.GuideDistanceElasticitySettings;
			var elasticityScale = settings.UsesResponseProfile ? settings.GuideElasticityScale : 1f;
			var material = collider.GetPhysicsMaterialData();
			var asset = collider.PhysicsOverwrite ? null : collider.PhysicsMaterial;
			var elasticityLut = Array.Empty<float>();
			var frictionLut = Array.Empty<float>();
			if (asset && asset.UseElasticityOverVelocity) {
				material.UseElasticityOverVelocity = true;
				elasticityLut = ToArray(asset.GetElasticityOverVelocityLUT());
			}
			if (asset && asset.UseFrictionOverVelocity) {
				material.UseFrictionOverVelocity = true;
				frictionLut = ToArray(asset.GetFrictionOverVelocityLUT());
			}

			return bindings.Select(binding => new RubberBackedResponseDescriptor {
				BindingIndex = binding,
				PathElementIndex = arcByBinding.TryGetValue(binding, out var element) ? element : -1,
				Material = material,
				ElasticityOverVelocityLut = elasticityLut,
				FrictionOverVelocityLut = frictionLut,
				ElasticityScale = elasticityScale,
			}).ToArray();
		}

		private static float[] ToArray(global::Unity.Collections.FixedList512Bytes<float> list)
		{
			var values = new float[list.Length];
			for (var i = 0; i < list.Length; i++) {
				values[i] = list[i];
			}
			return values;
		}

		#endregion

		#region Hashing

		/// <summary>
		/// Canonical lowercase hex SHA-256 over every input that decides the bake
		/// outcome, per the plan's section 4.2.
		/// </summary>
		public static string ComputeBakeHash(RubberColliderComponent collider)
		{
			if (!collider) {
				return string.Empty;
			}
			using var stream = new MemoryStream();
			using (var writer = new BinaryWriter(stream, Encoding.UTF8, true)) {
				writer.Write(CompilerVersion);
				writer.Write(BakeVersion);
				writer.Write(collider.IsKinematic);
				writer.Write(collider.DeflectionClearance);

				var settings = collider.GuideDistanceElasticitySettings;
				writer.Write(settings.Enabled);
				writer.Write(settings.GuideElasticityScale);
				writer.Write(settings.GuideInfluenceLength);
				writer.Write(settings.MaximumResponseSegmentLength);

				WriteContactMaterial(writer, collider);
				WriteDeformationMaterial(writer, collider.RubberPhysicsMaterial);

				var rubber = collider.MainComponent;
				if (!rubber) {
					writer.Write(0);
				} else {
					writer.Write(1);
					writer.Write(rubber.Thickness);
					writer.Write(rubber.RestLength);
					writer.Write((int)rubber.PathSource);
					var frame = rubber.BakeFrameToLocal;
					for (var row = 0; row < 4; row++) {
						for (var column = 0; column < 4; column++) {
							writer.Write(frame[row, column]);
						}
					}
					WritePath(writer, rubber.BakedPath);
					WriteGuideSlots(writer, rubber);
				}
			}
			using var sha = SHA256.Create();
			return ToHex(sha.ComputeHash(stream.ToArray()));
		}

		private static void WritePath(BinaryWriter writer, IReadOnlyList<RubberPathElement> path)
		{
			writer.Write(path.Count);
			foreach (var element in path) {
				writer.Write((int)element.Type);
				writer.Write(element.Start.x);
				writer.Write(element.Start.y);
				writer.Write(element.End.x);
				writer.Write(element.End.y);
				writer.Write(element.Control1.x);
				writer.Write(element.Control1.y);
				writer.Write(element.Control2.x);
				writer.Write(element.Control2.y);
				writer.Write(element.Center.x);
				writer.Write(element.Center.y);
				writer.Write(element.Radius);
				writer.Write(element.StartAngleRad);
				writer.Write(element.SweepAngleRad);
				writer.Write(element.StartBindingIndex);
				writer.Write(element.EndBindingIndex);
				writer.Write(element.StartDistance);
				writer.Write(element.Length);
			}
		}

		private static void WriteGuideSlots(BinaryWriter writer, RubberComponent rubber)
		{
			var bindings = rubber.GuideBindings;
			writer.Write(bindings.Count);
			foreach (var binding in bindings) {
				writer.Write(binding.SlotId.A);
				writer.Write(binding.SlotId.B);
				if (!binding.Guide || !binding.Guide.TryGetSlot(binding.SlotId, out var slot)) {
					writer.Write(0);
					continue;
				}
				writer.Write(1);
				writer.Write(slot.LocalHeight);
				writer.Write(slot.RubberSupportFriction);
				writer.Write((int)slot.Profile.Type);
				writer.Write(slot.Profile.LocalCenter.x);
				writer.Write(slot.Profile.LocalCenter.y);
				writer.Write(slot.Profile.Radius);
				var hull = slot.Profile.ConvexHull;
				writer.Write(hull?.Length ?? 0);
				if (hull == null) {
					continue;
				}
				foreach (var point in hull) {
					writer.Write(point.x);
					writer.Write(point.y);
				}
			}
		}

		private static void WriteContactMaterial(BinaryWriter writer,
			RubberColliderComponent collider)
		{
			writer.Write(collider.PhysicsOverwrite);
			writer.Write(collider.PhysicsElasticity);
			writer.Write(collider.PhysicsElasticityFalloff);
			writer.Write(collider.PhysicsFriction);
			writer.Write(collider.PhysicsScatter);
			var asset = collider.PhysicsMaterial;
			if (!asset) {
				writer.Write(0);
				return;
			}
			writer.Write(1);
			writer.Write(asset.Elasticity);
			writer.Write(asset.ElasticityFalloff);
			writer.Write(asset.Friction);
			writer.Write(asset.ScatterAngle);
			WriteCurve(writer, asset.ElasticityOverVelocity);
			WriteCurve(writer, asset.FrictionOverVelocity);
		}

		private static void WriteDeformationMaterial(BinaryWriter writer,
			RubberPhysicsMaterialAsset asset)
		{
			if (!asset) {
				writer.Write(0);
				return;
			}
			writer.Write(1);
			writer.Write(asset.DensityKgPerCubicMeter);
			writer.Write(asset.ModalDampingRatio);
			writer.Write(asset.BallSurfaceFriction);
			writer.Write(asset.MaximumStretchRatio);
			WriteCurve(writer, asset.NominalStressMpaByStretchRatio);
		}

		private static void WriteCurve(BinaryWriter writer, AnimationCurve curve)
		{
			if (curve == null) {
				writer.Write(0);
				return;
			}
			writer.Write(1);
			writer.Write((int)curve.preWrapMode);
			writer.Write((int)curve.postWrapMode);
			var keys = curve.keys;
			writer.Write(keys.Length);
			foreach (var key in keys) {
				writer.Write(key.time);
				writer.Write(key.value);
				writer.Write(key.inTangent);
				writer.Write(key.outTangent);
				writer.Write(key.inWeight);
				writer.Write(key.outWeight);
				writer.Write((int)key.weightedMode);
			}
		}

		private static string ToHex(IReadOnlyList<byte> digest)
		{
			var builder = new StringBuilder(digest.Count * 2);
			foreach (var value in digest) {
				builder.Append(value.ToString("x2", CultureInfo.InvariantCulture));
			}
			return builder.ToString();
		}

		#endregion

		#region Arc length table

		/// <summary>
		/// Arc-length parametrization of one free span, plus the per-segment
		/// derivative bounds the tessellation certificate needs.
		/// </summary>
		private sealed class ArcLengthTable
		{
			private readonly RubberPathElement _element;
			private readonly float[] _cumulative;
			private readonly float[] _speed;
			private readonly float2 _hodograph0;
			private readonly float2 _hodograph1;
			private readonly float2 _hodograph2;

			internal float TotalLength { get; }
			internal float MinimumTangentSpeed { get; }
			internal float MeanTangentSpeed { get; }

			internal ArcLengthTable(RubberPathElement element)
			{
				_element = element;
				_hodograph0 = 3f * (element.Control1 - element.Start);
				_hodograph1 = 3f * (element.Control2 - element.Control1);
				_hodograph2 = 3f * (element.End - element.Control2);

				var count = ArcLengthSampleCount;
				_cumulative = new float[count + 1];
				_speed = new float[count + 1];
				var previous = RubberPathMath.PointAt(element, 0f);
				var length = 0.0;
				var speedSum = 0.0;
				var minimum = float.PositiveInfinity;
				for (var i = 0; i <= count; i++) {
					var t = i / (float)count;
					_speed[i] = math.length(Hodograph(t));
					minimum = math.min(minimum, _speed[i]);
					speedSum += _speed[i];
					if (i > 0) {
						var point = RubberPathMath.PointAt(element, t);
						length += math.distance(previous, point);
						previous = point;
					}
					_cumulative[i] = (float)length;
				}
				TotalLength = (float)length;
				MinimumTangentSpeed = minimum;
				MeanTangentSpeed = (float)(speedSum / (count + 1));
			}

			internal float[] BuildFractions(int segmentCount)
			{
				segmentCount = math.max(1, segmentCount);
				var fractions = new float[segmentCount + 1];
				fractions[0] = 0f;
				fractions[segmentCount] = 1f;
				for (var k = 1; k < segmentCount; k++) {
					fractions[k] = ParameterAtLength(TotalLength * k / segmentCount);
				}
				return fractions;
			}

			internal bool TryBoundErrors(int segmentCount, out float restError,
				out float displacementError)
			{
				restError = 0f;
				displacementError = 0f;
				var deltaS = TotalLength / segmentCount;
				var sineTerm = math.PI / TotalLength;
				var boundary = 0f;
				for (var k = 0; k < segmentCount; k++) {
					var next = k == segmentCount - 1 ? 1f : ParameterAtLength(
						TotalLength * (k + 1) / (float)segmentCount);
					if (!TryBoundSegment(boundary, next, out var curvature, out var curvatureRate)) {
						return false;
					}
					boundary = next;
					var rest = deltaS * deltaS / 8f * curvature;
					var displacement = deltaS * deltaS / 8f
						* (curvatureRate + curvature * curvature
							+ 2f * curvature * sineTerm + sineTerm * sineTerm);
					restError = math.max(restError, rest);
					displacementError = math.max(displacementError, displacement);
				}
				return float.IsFinite(restError) && float.IsFinite(displacementError);
			}

			/// <summary>
			/// Bounds the curvature and the curvature rate of one segment in arc-length
			/// coordinates.
			/// </summary>
			private bool TryBoundSegment(float startT, float endT, out float curvature,
				out float curvatureRate)
			{
				curvature = 0f;
				curvatureRate = 0f;
				if (!(endT > startT)) {
					return false;
				}
				var speedBound = TangentSpeedLowerBound(startT, endT);
				if (!(speedBound > 0f)) {
					return false;
				}
				// The cubic's second derivative is linear in t, so the larger endpoint
				// norm bounds the whole segment.
				var secondDerivative = math.max(math.length(SecondDerivative(startT)),
					math.length(SecondDerivative(endT)));
				curvature = secondDerivative / (speedBound * speedBound);

				// Numerical bound on |dkappa/ds| with a factor-two safety margin.
				const int samples = 16;
				var previousCurvature = CurvatureAt(startT);
				var maximumRate = 0f;
				for (var i = 1; i <= samples; i++) {
					var t = math.lerp(startT, endT, i / (float)samples);
					var value = CurvatureAt(t);
					var deltaT = (endT - startT) / samples;
					var deltaArc = deltaT * speedBound;
					if (deltaArc > 0f) {
						maximumRate = math.max(maximumRate,
							math.abs(value - previousCurvature) / deltaArc);
					}
					previousCurvature = value;
				}
				curvatureRate = 2f * maximumRate;
				return float.IsFinite(curvature) && float.IsFinite(curvatureRate);
			}

			/// <summary>
			/// Rigorous lower bound of <c>|B'(t)|</c> over a parameter range, from the
			/// convex hull of the restricted hodograph projected on its mean direction.
			/// Falls back to a sampled minimum when the projection is not positive.
			/// </summary>
			private float TangentSpeedLowerBound(float startT, float endT)
			{
				var w0 = Hodograph(startT);
				var w2 = Hodograph(endT);
				var w1 = Blossom(startT, endT);
				var direction = math.normalizesafe(w0 + w1 + w2, float2.zero);
				if (!math.all(direction == float2.zero)) {
					var bound = math.min(math.dot(w0, direction),
						math.min(math.dot(w1, direction), math.dot(w2, direction)));
					if (bound > 0f) {
						return bound;
					}
				}
				return SampledMinimumSpeed(startT, endT) * 0.9f;
			}

			private float SampledMinimumSpeed(float startT, float endT)
			{
				var minimum = float.PositiveInfinity;
				var first = (int)math.floor(startT * ArcLengthSampleCount);
				var last = (int)math.ceil(endT * ArcLengthSampleCount);
				for (var i = math.max(0, first); i <= math.min(ArcLengthSampleCount, last); i++) {
					minimum = math.min(minimum, _speed[i]);
				}
				return minimum;
			}

			private float CurvatureAt(float t)
			{
				var first = Hodograph(t);
				var speed = math.length(first);
				if (!(speed > 1e-9f)) {
					return 0f;
				}
				var second = SecondDerivative(t);
				var cross = math.abs(first.x * second.y - first.y * second.x);
				return cross / (speed * speed * speed);
			}

			private float2 Hodograph(float t)
			{
				var u = 1f - t;
				return u * u * _hodograph0 + 2f * u * t * _hodograph1 + t * t * _hodograph2;
			}

			private float2 Blossom(float a, float b)
			{
				return (1f - a) * (1f - b) * _hodograph0
					+ ((1f - a) * b + a * (1f - b)) * _hodograph1
					+ a * b * _hodograph2;
			}

			private float2 SecondDerivative(float t)
			{
				return 2f * ((1f - t) * (_hodograph1 - _hodograph0) + t * (_hodograph2 - _hodograph1));
			}

			private float ParameterAtLength(float target)
			{
				if (!(TotalLength > 0f)) {
					return 0f;
				}
				target = math.clamp(target, 0f, TotalLength);
				var low = 0;
				var high = _cumulative.Length - 1;
				while (low < high) {
					var middle = (low + high) >> 1;
					if (_cumulative[middle] < target) {
						low = middle + 1;
					} else {
						high = middle;
					}
				}
				if (low == 0) {
					return 0f;
				}
				var before = _cumulative[low - 1];
				var after = _cumulative[low];
				var span = after - before;
				var fraction = span > 0f ? (target - before) / span : 0f;
				return math.saturate((low - 1 + fraction) / (_cumulative.Length - 1));
			}
		}

		#endregion
	}

	/// <summary>
	/// Repaints inspectors when a material a deforming rubber depends on is
	/// reimported, so the bake freshness badge, which is derived from the hash,
	/// updates without a selection change.
	/// </summary>
	internal sealed class RubberDeformationAssetPostprocessor : AssetPostprocessor
	{
		private static void OnPostprocessAllAssets(string[] importedAssets,
			string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
		{
			if (!TouchesDeformationMaterial(importedAssets)
				&& !TouchesDeformationMaterial(movedAssets)
				&& !TouchesDeformationMaterial(deletedAssets)) {
				return;
			}
			// The hash comparison at read time is the source of truth for staleness, so
			// nothing has to be rewritten here. Dependent components in open scenes only
			// need their inspectors and gizmos to redraw.
			var hasDependent = UnityEngine.Object.FindObjectsByType<RubberColliderComponent>(
					FindObjectsInactive.Include, FindObjectsSortMode.None)
				.Any(collider => collider && collider.DeformationEnabled);
			if (!hasDependent) {
				return;
			}
			SceneView.RepaintAll();
			foreach (var editor in Resources.FindObjectsOfTypeAll<RubberColliderInspector>()) {
				editor.Repaint();
			}
		}

		private static bool TouchesDeformationMaterial(IEnumerable<string> paths)
		{
			foreach (var path in paths) {
				if (string.IsNullOrEmpty(path) || !path.EndsWith(".asset",
					StringComparison.OrdinalIgnoreCase)) {
					continue;
				}
				var type = AssetDatabase.GetMainAssetTypeAtPath(path);
				if (type == typeof(RubberPhysicsMaterialAsset) || type == typeof(PhysicsMaterialAsset)) {
					return true;
				}
			}
			return false;
		}
	}
}
