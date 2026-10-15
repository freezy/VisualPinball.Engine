// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
// https://github.com/freezy/VisualPinball.Engine
//
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using Unity.Mathematics;
using UnityEngine;

namespace VisualPinball.Unity
{
	/// <summary>How the virtual table is sized to the physical screen.</summary>
	public enum CabinetFitPolicy
	{
		/// <summary>The table at its real size; a short screen shows only part of it.</summary>
		PhysicalSize = 0,
		/// <summary>Scaled uniformly so the whole table fits the screen (letterboxed across or along).</summary>
		Fit = 1,
		/// <summary>Scaled across and along independently so the table fills the screen.</summary>
		FitStretched = 2,
		/// <summary>
		/// Scaled uniformly and moved along so that, seen from the eye, the playfield's front and rear
		/// edges meet the screen's front and rear edges.
		/// </summary>
		FillLength = 3,
	}

	/// <summary>
	/// The measured cabinet, in metres and degrees. The cabinet frame has its origin at the centre of the
	/// screen's front edge, +X to the right, +Y horizontally toward the backglass and +Z up.
	/// </summary>
	public struct CabinetGeometry
	{
		/// <summary>Illuminated width across the cabinet (the short edge for a portrait playfield).</summary>
		public double ScreenWidth;
		/// <summary>Illuminated length along the cabinet (the long edge).</summary>
		public double ScreenLength;
		/// <summary>How much the screen rises toward the back, in degrees.</summary>
		public double Inclination;
		/// <summary>The player's eye in the cabinet frame. In front of the cabinet, Y is negative.</summary>
		public double3 Eye;
	}

	/// <summary>
	/// Where the virtual table sits behind the screen. Lengths are in metres, in the table's reference frame
	/// (origin at the front centre of the playfield, X to the right, Y toward the rear, Z up).
	/// </summary>
	public struct CabinetTablePlacement
	{
		public double TableWidth;
		public double TableLength;
		/// <summary>Height of the window (the screen's plane) above the playfield at its front.</summary>
		public double HeightFront;
		/// <summary>Height of the window above the playfield at its rear.</summary>
		public double HeightRear;
		public CabinetFitPolicy Fit;
		/// <summary>Manual multiplier on the lateral scale (1 = none).</summary>
		public double ScaleX;
		/// <summary>Manual multiplier on the coupled length and height scale (1 = none).</summary>
		public double ScaleLength;
		/// <summary>Shift of the table on the screen along its width, in metres.</summary>
		public double OffsetX;
		/// <summary>Shift of the table on the screen along its length, in metres.</summary>
		public double OffsetY;
	}

	public struct CabinetProjectionInput
	{
		public CabinetGeometry Cabinet;
		public CabinetTablePlacement Table;
		/// <summary>Clockwise quarter turns of the picture on the physical screen (0 to 3).</summary>
		public int QuarterTurns;
		/// <summary>From the table reference frame (metres) to Unity world space.</summary>
		public Matrix4x4 TableToWorld;
		/// <summary>World-space bounds of everything that can be visible, used for the clip planes.</summary>
		public Bounds ContentBounds;
	}

	public struct CabinetProjectionResult
	{
		public bool IsValid;
		public string Error;

		public Vector3 Position;
		public Quaternion Rotation;
		public Matrix4x4 Projection;
		public float Near, Far;
		/// <summary>Frustum extents at the near plane.</summary>
		public float Left, Right, Bottom, Top;

		/// <summary>A symmetric field of view and aspect enclosing the frustum, for consumers that need one.</summary>
		public float EnclosingFieldOfView, EnclosingAspect;
		/// <summary>Width over height of the physical picture, in output orientation.</summary>
		public float PictureAspect;
		/// <summary>The scales actually used, after fitting and manual multipliers.</summary>
		public float ScaleX, ScaleLength;

		/// <summary>The screen corners in world space: picture lower left, lower right and upper left.</summary>
		public Vector3 LowerLeft, LowerRight, UpperLeft;
		public Vector3 Eye;

		public static CabinetProjectionResult Invalid(string error) => new() { IsValid = false, Error = error };
	}

	/// <summary>
	/// Computes the camera that renders the table as if the physical screen were a window into it, seen
	/// from a fixed eye: a camera at the eye and an off-centre frustum through the screen's corners.
	/// </summary>
	///
	/// <remarks>
	/// The virtual table is placed in the cabinet by a translation, a rotation about the lateral axis and
	/// the scales (sx, syz, syz). The screen and eye are mapped through the inverse placement into the
	/// unchanged table world, where the screen stays a rectangle, so the result is a rigid camera pose and
	/// an ordinary asymmetric frustum. All math is in double precision.
	/// </remarks>
	public static class CabinetProjectionSolver
	{
		private const double MinClip = 0.01;

		public static CabinetProjectionResult Solve(in CabinetProjectionInput input)
		{
			var cab = input.Cabinet;
			var table = input.Table;
			if (!Positive(cab.ScreenWidth) || !Positive(cab.ScreenLength)) {
				return CabinetProjectionResult.Invalid("The screen size must be positive.");
			}
			if (!Finite(cab.Inclination) || math.abs(cab.Inclination) > 60) {
				return CabinetProjectionResult.Invalid("The screen inclination must be between -60° and 60°.");
			}
			if (!Finite(cab.Eye.x) || !Finite(cab.Eye.y) || !Finite(cab.Eye.z)) {
				return CabinetProjectionResult.Invalid("The eye position is not a number.");
			}
			if (!Positive(table.TableWidth) || !Positive(table.TableLength)) {
				return CabinetProjectionResult.Invalid("The table has no size.");
			}
			if (!Finite(table.HeightFront) || !Finite(table.HeightRear)) {
				return CabinetProjectionResult.Invalid("The window heights are not numbers.");
			}
			if (!Positive(table.ScaleX) || !Positive(table.ScaleLength)) {
				return CabinetProjectionResult.Invalid("The table scales must be positive.");
			}
			if (!Finite(table.OffsetX) || !Finite(table.OffsetY)) {
				return CabinetProjectionResult.Invalid("The table offsets are not numbers.");
			}

			// the screen in the cabinet frame
			var alpha = math.radians(cab.Inclination);
			var ex = new double3(1, 0, 0);
			var ey = new double3(0, math.cos(alpha), math.sin(alpha));
			var screenCenter = ey * (cab.ScreenLength * 0.5);

			// placement of the table: A(Q) = offset + Rx(alpha - gamma) * diag(sx, syz, syz) * (Q - F)
			var rise = table.HeightRear - table.HeightFront;
			var windowLength = math.sqrt(table.TableLength * table.TableLength + rise * rise);
			var gamma = math.atan2(rise, table.TableLength);
			double sx, syz;
			switch (table.Fit) {
				case CabinetFitPolicy.Fit:
					sx = syz = math.min(cab.ScreenWidth / table.TableWidth, cab.ScreenLength / windowLength);
					break;
				case CabinetFitPolicy.FitStretched:
					sx = cab.ScreenWidth / table.TableWidth;
					syz = cab.ScreenLength / windowLength;
					break;
				default:
					sx = syz = 1;
					break;
			}
			sx *= table.ScaleX;
			syz *= table.ScaleLength;
			var front = new double3(0, 0, table.HeightFront);
			var offset = ex * table.OffsetX + ey * table.OffsetY;
			if (table.Fit == CabinetFitPolicy.FillLength) {
				var fill = FillLength(cab, table, alpha - gamma, front, ex, ey, out var along);
				if (double.IsNaN(fill)) {
					return CabinetProjectionResult.Invalid("From this eye position the table can't fill the screen's length. Choose another table size or check the eye.");
				}
				sx = fill * table.ScaleX;
				syz = fill * table.ScaleLength;
				offset += ey * along;
			}
			var toTable = RotateX(gamma - alpha); // inverse of Rx(alpha - gamma)
			var invScale = new double3(1 / sx, 1 / syz, 1 / syz);
			double3 CabinetToTable(double3 p) => front + invScale * math.mul(toTable, p - offset);

			// the picture's right and up on the screen, for the output's quarter turns
			double3 right, up;
			double halfRight, halfUp;
			switch (((input.QuarterTurns % 4) + 4) % 4) {
				case 1: right = ey; up = -ex; halfRight = cab.ScreenLength * 0.5; halfUp = cab.ScreenWidth * 0.5; break;
				case 2: right = -ex; up = -ey; halfRight = cab.ScreenWidth * 0.5; halfUp = cab.ScreenLength * 0.5; break;
				case 3: right = -ey; up = ex; halfRight = cab.ScreenLength * 0.5; halfUp = cab.ScreenWidth * 0.5; break;
				default: right = ex; up = ey; halfRight = cab.ScreenWidth * 0.5; halfUp = cab.ScreenLength * 0.5; break;
			}
			var cabA = screenCenter - right * halfRight - up * halfUp;
			var cabB = screenCenter + right * halfRight - up * halfUp;
			var cabC = screenCenter - right * halfRight + up * halfUp;

			// into Unity world, through the table frame
			var tableToWorld = ToDouble(input.TableToWorld);
			double3 ToWorld(double3 cabinet) => math.transform(tableToWorld, CabinetToTable(cabinet));
			var pa = ToWorld(cabA);
			var pb = ToWorld(cabB);
			var pc = ToWorld(cabC);
			var pe = ToWorld(cab.Eye);

			var edgeRight = pb - pa;
			var edgeUp = pc - pa;
			if (math.length(edgeRight) < 1e-9 || math.length(edgeUp) < 1e-9) {
				return CabinetProjectionResult.Invalid("The screen maps to a degenerate rectangle.");
			}
			var vr = math.normalize(edgeRight);
			var vu = math.normalize(edgeUp);
			if (math.abs(math.dot(vr, vu)) > 1e-4) {
				return CabinetProjectionResult.Invalid("The table is sheared or scaled unevenly, so the screen is no longer a rectangle in it.");
			}
			// Unity: right x up = forward. The normal toward the eye is -forward.
			var forward = math.normalize(math.cross(vr, vu));
			var va = pa - pe;
			var vb = pb - pe;
			var vc = pc - pe;
			var distance = math.dot(va, forward);
			if (!(distance > 1e-6)) {
				return CabinetProjectionResult.Invalid("The eye is behind the screen. Check the eye position and the screen's orientation.");
			}

			// clip planes enclosing the content (which may come out in front of the screen)
			var minDepth = double.MaxValue;
			var maxDepth = double.MinValue;
			var bounds = input.ContentBounds;
			for (var i = 0; i < 8; i++) {
				var corner = new double3(
					(i & 1) == 0 ? bounds.min.x : bounds.max.x,
					(i & 2) == 0 ? bounds.min.y : bounds.max.y,
					(i & 4) == 0 ? bounds.min.z : bounds.max.z);
				var depth = math.dot(corner - pe, forward);
				minDepth = math.min(minDepth, depth);
				maxDepth = math.max(maxDepth, depth);
			}
			maxDepth = math.max(maxDepth, distance);
			var near = math.max(MinClip, math.min(minDepth * 0.9, distance * 0.9));
			var far = maxDepth * 1.1 + 0.1;
			if (!(far > near) || !Finite(near) || !Finite(far)) {
				return CabinetProjectionResult.Invalid("The clip planes are invalid.");
			}

			var l = near * math.dot(vr, va) / distance;
			var r = near * math.dot(vr, vb) / distance;
			var b = near * math.dot(vu, va) / distance;
			var t = near * math.dot(vu, vc) / distance;

			var xExtent = math.max(math.abs(l), math.abs(r));
			var yExtent = math.max(math.abs(b), math.abs(t));
			return new CabinetProjectionResult {
				IsValid = true,
				Position = ToVector(pe),
				Rotation = Quaternion.LookRotation(ToVector(forward), ToVector(vu)),
				Projection = Matrix4x4.Frustum((float)l, (float)r, (float)b, (float)t, (float)near, (float)far),
				Near = (float)near, Far = (float)far,
				Left = (float)l, Right = (float)r, Bottom = (float)b, Top = (float)t,
				EnclosingFieldOfView = (float)math.degrees(2 * math.atan(yExtent / near)),
				EnclosingAspect = (float)(xExtent / yExtent),
				PictureAspect = (float)(halfRight / halfUp),
				ScaleX = (float)sx, ScaleLength = (float)syz,
				LowerLeft = ToVector(pa), LowerRight = ToVector(pb), UpperLeft = ToVector(pc),
				Eye = ToVector(pe),
			};
		}

		/// <summary>
		/// The uniform scale, and the shift along the screen, at which the playfield's front and rear edges,
		/// seen from the eye, meet the screen's front and rear edges. The playfield lies below the screen's
		/// plane, so it looks smaller than it is scaled and the eye's perspective moves it; both are found
		/// by bisection: the shift that puts the front on the front edge, for the scale that then puts the
		/// rear on the rear edge.
		/// </summary>
		private static double FillLength(CabinetGeometry cab, CabinetTablePlacement table, double tilt,
			double3 front, double3 ex, double3 ey, out double along)
		{
			var toCabinet = RotateX(tilt);
			var normal = math.cross(ex, ey); // up, away from the screen
			var eyeHeight = math.dot(cab.Eye, normal);
			along = 0;
			if (!(eyeHeight > 0)) {
				return 1; // the eye is behind the screen: Solve rejects it
			}
			// where a point on the playfield's centre line appears along the screen
			double Along(double scale, double shift, double y)
			{
				var p = ey * shift + math.mul(toCabinet, scale * (new double3(0, y, 0) - front));
				var t = eyeHeight / (eyeHeight - math.dot(p, normal));
				return math.dot(cab.Eye + t * (p - cab.Eye), ey);
			}
			double FrontShift(double scale)
			{
				double low = -10, high = 10;
				for (var i = 0; i < 60; i++) {
					var mid = 0.5 * (low + high);
					if (Along(scale, mid, 0) < 0) {
						low = mid;
					} else {
						high = mid;
					}
				}
				return 0.5 * (low + high);
			}
			double lowScale = 1e-3, highScale = 100;
			for (var i = 0; i < 60; i++) {
				var mid = 0.5 * (lowScale + highScale);
				var shift = FrontShift(mid);
				if (Along(mid, shift, table.TableLength) - Along(mid, shift, 0) < cab.ScreenLength) {
					lowScale = mid;
				} else {
					highScale = mid;
				}
			}
			var scale = 0.5 * (lowScale + highScale);
			along = FrontShift(scale);
			// the search stops at its limits when no scale and shift fit: then there is no solution
			const double tolerance = 1e-4;
			var front0 = Along(scale, along, 0);
			var rear = Along(scale, along, table.TableLength);
			if (math.abs(front0) > tolerance || math.abs(rear - cab.ScreenLength) > tolerance) {
				return double.NaN;
			}
			return scale;
		}

		/// <summary>
		/// The reference model: where the ray from the eye through a world point crosses the screen, as
		/// picture coordinates (0,0 lower left to 1,1 upper right). False when the ray misses the plane.
		/// </summary>
		public static bool IntersectPicture(in CabinetProjectionResult projection, Vector3 worldPoint, out Vector2 picture)
		{
			picture = default;
			var pa = (double3)(float3)projection.LowerLeft;
			var edgeRight = (double3)(float3)projection.LowerRight - pa;
			var edgeUp = (double3)(float3)projection.UpperLeft - pa;
			var normal = math.cross(edgeRight, edgeUp);
			var eye = (double3)(float3)projection.Eye;
			var dir = (double3)(float3)worldPoint - eye;
			var denominator = math.dot(dir, normal);
			if (math.abs(denominator) < 1e-12) {
				return false;
			}
			var hit = eye + dir * (math.dot(pa - eye, normal) / denominator) - pa;
			picture = new Vector2(
				(float)(math.dot(hit, edgeRight) / math.lengthsq(edgeRight)),
				(float)(math.dot(hit, edgeUp) / math.lengthsq(edgeUp)));
			return true;
		}

		/// <summary>
		/// From the table reference frame (metres; origin at the front centre of the playfield, X right,
		/// Y toward the rear, Z up) to the playfield's local Unity space: Q = (x - midX, Bottom - y, z) / scale
		/// in VPX units.
		/// </summary>
		public static Matrix4x4 TableReferenceToPlayfieldLocal(float left, float right, float bottom)
		{
			var m = Matrix4x4.zero;
			m.m00 = 1f;   // Q.x -> local x
			m.m21 = 1f;   // Q.y -> local z
			m.m12 = 1f;   // Q.z -> local y
			m.m03 = Physics.ScaleToWorld((left + right) * 0.5f);
			m.m23 = -Physics.ScaleToWorld(bottom);
			m.m33 = 1f;
			return m;
		}

		/// <summary>
		/// Checks that a table transform keeps the screen a rectangle: positive, uniform scale and no shear.
		/// </summary>
		public static bool IsSupportedTableTransform(Matrix4x4 localToWorld, out string error)
		{
			var x = (Vector3)localToWorld.GetColumn(0);
			var y = (Vector3)localToWorld.GetColumn(1);
			var z = (Vector3)localToWorld.GetColumn(2);
			float sx = x.magnitude, sy = y.magnitude, sz = z.magnitude;
			var max = Mathf.Max(sx, Mathf.Max(sy, sz));
			if (!(max > 0f) || Mathf.Abs(sx - sy) > 1e-3f * max || Mathf.Abs(sx - sz) > 1e-3f * max) {
				error = "The table is scaled unevenly, which the cabinet view doesn't support.";
				return false;
			}
			if (Mathf.Abs(Vector3.Dot(x, y)) > 1e-3f * max * max || Mathf.Abs(Vector3.Dot(x, z)) > 1e-3f * max * max || Mathf.Abs(Vector3.Dot(y, z)) > 1e-3f * max * max) {
				error = "The table transform is sheared, which the cabinet view doesn't support.";
				return false;
			}
			if (Vector3.Dot(Vector3.Cross(x, y), z) <= 0f) {
				error = "The table transform is mirrored, which the cabinet view doesn't support.";
				return false;
			}
			error = null;
			return true;
		}

		// rotation about X by angle (radians), right-handed
		private static double3x3 RotateX(double angle)
		{
			double c = math.cos(angle), s = math.sin(angle);
			return new double3x3(1, 0, 0, 0, c, -s, 0, s, c);
		}

		private static double4x4 ToDouble(Matrix4x4 m) => new(
			m.m00, m.m01, m.m02, m.m03,
			m.m10, m.m11, m.m12, m.m13,
			m.m20, m.m21, m.m22, m.m23,
			m.m30, m.m31, m.m32, m.m33);

		private static Vector3 ToVector(double3 v) => new((float)v.x, (float)v.y, (float)v.z);
		private static bool Finite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);
		private static bool Positive(double v) => Finite(v) && v > 0;
	}
}
