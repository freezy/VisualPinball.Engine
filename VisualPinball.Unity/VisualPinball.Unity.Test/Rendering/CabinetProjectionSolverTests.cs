// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
// https://github.com/freezy/VisualPinball.Engine
//
// SPDX-License-Identifier: GPL-3.0-or-later

using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace VisualPinball.Unity.Test
{
	/// <summary>
	/// The cabinet projection: the solved camera must put every point where the ray from the eye
	/// through it crosses the physical screen.
	/// </summary>
	public class CabinetProjectionSolverTests
	{
		private const float Left = 0f, Right = 952f, Top = 0f, Bottom = 2162f;
		private static float TableWidth => Physics.ScaleToWorld(Right - Left);
		private static float TableLength => Physics.ScaleToWorld(Bottom - Top);

		private static CabinetProjectionInput Input(int quarterTurns = 0, Matrix4x4? playfieldToWorld = null,
			CabinetFitPolicy fit = CabinetFitPolicy.Fit, double scaleX = 1, double scaleLength = 1,
			double inclination = 6, double3? eye = null, double heightFront = 0.08, double heightRear = 0.12)
		{
			var tableToWorld = (playfieldToWorld ?? Matrix4x4.identity) * CabinetProjectionSolver.TableReferenceToPlayfieldLocal(Left, Right, Bottom);
			var tableCenter = tableToWorld.MultiplyPoint(new Vector3(0, TableLength * 0.5f, 0.2f));
			return new CabinetProjectionInput {
				Cabinet = new CabinetGeometry {
					ScreenWidth = 0.523, ScreenLength = 0.93, Inclination = inclination,
					Eye = eye ?? new double3(0.02, -0.25, 0.65),
				},
				Table = new CabinetTablePlacement {
					TableWidth = TableWidth, TableLength = TableLength,
					HeightFront = heightFront, HeightRear = heightRear,
					Fit = fit, ScaleX = scaleX, ScaleLength = scaleLength,
				},
				QuarterTurns = quarterTurns,
				TableToWorld = tableToWorld,
				ContentBounds = new Bounds(tableCenter, Vector3.one * 2f),
			};
		}

		// NDC of a world point through the solved camera, the way Unity renders it
		private static Vector2 Ndc(in CabinetProjectionResult result, Vector3 world)
		{
			var view = Matrix4x4.TRS(result.Position, result.Rotation, new Vector3(1, 1, -1)).inverse;
			var clip = result.Projection * view * new Vector4(world.x, world.y, world.z, 1f);
			return new Vector2(clip.x / clip.w, clip.y / clip.w);
		}

		private static Vector3 TablePoint(in CabinetProjectionInput input, float x, float y, float z)
			=> input.TableToWorld.MultiplyPoint(new Vector3(x, y, z));

		[TestCase(0)]
		[TestCase(1)]
		[TestCase(2)]
		[TestCase(3)]
		public void ScreenCornersLandOnTheOutputCorners(int quarterTurns)
		{
			var result = CabinetProjectionSolver.Solve(Input(quarterTurns));
			Assert.That(result.IsValid, result.Error);

			var upperRight = result.LowerRight + result.UpperLeft - result.LowerLeft;
			AssertNdc(Ndc(result, result.LowerLeft), -1, -1);
			AssertNdc(Ndc(result, result.LowerRight), 1, -1);
			AssertNdc(Ndc(result, result.UpperLeft), -1, 1);
			AssertNdc(Ndc(result, upperRight), 1, 1);

			// the camera's axes are the picture's
			Assert.That(Vector3.Dot(result.Rotation * Vector3.right, (result.LowerRight - result.LowerLeft).normalized), Is.EqualTo(1f).Within(1e-4f));
			Assert.That(Vector3.Dot(result.Rotation * Vector3.up, (result.UpperLeft - result.LowerLeft).normalized), Is.EqualTo(1f).Within(1e-4f));
		}

		[TestCase(0, CabinetFitPolicy.Fit, 1.0, 1.0)]
		[TestCase(1, CabinetFitPolicy.FitStretched, 1.0, 1.0)]
		[TestCase(2, CabinetFitPolicy.PhysicalSize, 1.0, 1.0)]
		[TestCase(3, CabinetFitPolicy.Fit, 0.8, 1.15)]
		public void MatchesTheRayModelEverywhere(int quarterTurns, CabinetFitPolicy fit, double scaleX, double scaleLength)
		{
			var playfieldToWorld = Matrix4x4.TRS(new Vector3(0.3f, 0.9f, -0.4f), Quaternion.Euler(2f, 30f, 0f), Vector3.one * 1.3f);
			var input = Input(quarterTurns, playfieldToWorld, fit, scaleX, scaleLength);
			var result = CabinetProjectionSolver.Solve(input);
			Assert.That(result.IsValid, result.Error);

			var random = new global::Unity.Mathematics.Random(42u);
			var maxError = 0f;
			for (var i = 0; i < 2000; i++) {
				var point = TablePoint(input,
					random.NextFloat(-0.5f, 0.5f) * TableWidth,
					random.NextFloat(0f, 1f) * TableLength,
					random.NextFloat(-0.02f, 0.3f));
				Assert.That(CabinetProjectionSolver.IntersectPicture(result, point, out var picture));
				var ndc = Ndc(result, point);
				var fromCamera = new Vector2((ndc.x + 1f) * 0.5f, (ndc.y + 1f) * 0.5f);
				maxError = Mathf.Max(maxError, (fromCamera - picture).magnitude);
			}
			Assert.That(maxError, Is.LessThan(2e-4f), "float32 picture error");
		}

		[Test]
		public void PlayfieldReferenceFollowsTheVpxConversion()
		{
			var toLocal = CabinetProjectionSolver.TableReferenceToPlayfieldLocal(Left, Right, Bottom);
			// front left, front right and rear centre of the playfield, in VPX units
			AssertPoint(toLocal.MultiplyPoint(new Vector3(-TableWidth * 0.5f, 0, 0)), new Vector3(Left, Bottom, 0).TranslateToWorld());
			AssertPoint(toLocal.MultiplyPoint(new Vector3(TableWidth * 0.5f, 0, 0)), new Vector3(Right, Bottom, 0).TranslateToWorld());
			AssertPoint(toLocal.MultiplyPoint(new Vector3(0, TableLength, 0.1f)), new Vector3((Left + Right) * 0.5f, Top, Physics.ScaleToVpx(0.1f)).TranslateToWorld());
		}

		[Test]
		public void TableIsNotMirroredOnScreen()
		{
			var input = Input(0);
			var result = CabinetProjectionSolver.Solve(input);
			CabinetProjectionSolver.IntersectPicture(result, TablePoint(input, -0.2f, 0.1f, 0), out var frontLeft);
			CabinetProjectionSolver.IntersectPicture(result, TablePoint(input, 0.2f, TableLength - 0.1f, 0), out var rearRight);
			Assert.That(frontLeft.x, Is.LessThan(0.5f));
			Assert.That(frontLeft.y, Is.LessThan(0.5f));
			Assert.That(rearRight.x, Is.GreaterThan(0.5f));
			Assert.That(rearRight.y, Is.GreaterThan(0.5f));

			// turned clockwise, the rear is on the right and the table's left on top
			input = Input(1);
			result = CabinetProjectionSolver.Solve(input);
			CabinetProjectionSolver.IntersectPicture(result, TablePoint(input, -0.2f, 0.1f, 0), out frontLeft);
			Assert.That(frontLeft.x, Is.LessThan(0.5f));
			Assert.That(frontLeft.y, Is.GreaterThan(0.5f));
		}

		[Test]
		public void WindowFrontSitsOnTheScreensFrontEdge()
		{
			var input = Input(0, fit: CabinetFitPolicy.PhysicalSize, heightFront: 0.1, heightRear: 0.1);
			var result = CabinetProjectionSolver.Solve(input);
			CabinetProjectionSolver.IntersectPicture(result, TablePoint(input, 0, 0, 0.1f), out var front);
			Assert.That(front.x, Is.EqualTo(0.5f).Within(1e-4f));
			Assert.That(front.y, Is.EqualTo(0f).Within(1e-4f));

			// at physical size, the 1.17 m table is longer than the 0.93 m screen
			CabinetProjectionSolver.IntersectPicture(result, TablePoint(input, 0, TableLength, 0.1f), out var rear);
			Assert.That(rear.y, Is.EqualTo(TableLength / 0.93f).Within(1e-3f));
		}

		[TestCase(0, 0.0, 0.08, 0.08)]
		[TestCase(0, 0.0, 0.06, 0.14)]
		[TestCase(1, 0.05, 0.08, 0.12)]
		[TestCase(3, 0.0, 0.08, 0.12)]
		public void FillLengthPutsThePlayfieldEndsOnTheScreenEnds(int quarterTurns, double eyeX, double heightFront, double heightRear)
		{
			var input = Input(quarterTurns, fit: CabinetFitPolicy.FillLength, eye: new double3(eyeX, -0.25, 0.65), heightFront: heightFront, heightRear: heightRear);
			var result = CabinetProjectionSolver.Solve(input);
			Assert.That(result.IsValid, result.Error);

			// on the playfield (below the screen's plane): the front edge on the screen's front edge, the rear on its rear
			CabinetProjectionSolver.IntersectPicture(result, TablePoint(input, 0f, 0f, 0f), out var front);
			CabinetProjectionSolver.IntersectPicture(result, TablePoint(input, 0f, TableLength, 0f), out var rear);
			// the screen's length runs up the picture unturned, right turned clockwise, left turned counter-clockwise
			float Along(Vector2 picture) => quarterTurns switch { 1 => picture.x, 3 => 1f - picture.x, _ => picture.y };
			Assert.That(Along(front), Is.EqualTo(0f).Within(1e-4f));
			Assert.That(Along(rear), Is.EqualTo(1f).Within(1e-4f));
		}

		[Test]
		public void FillLengthRejectsAnEyeItCannotFillFrom()
		{
			// a low eye and a high window: no scale and shift put both playfield ends on the screen's ends
			var input = Input(0, fit: CabinetFitPolicy.FillLength, inclination: 0, eye: new double3(0, -0.25, 0.20), heightFront: 0.30, heightRear: 0.30);
			input.Cabinet.ScreenLength = 0.93;
			input.Table.TableLength = 1.167;
			Assert.That(CabinetProjectionSolver.Solve(input).IsValid, Is.False);
		}

		/// <summary>
		/// VPX scales about the table's mid-length but places the eye relative to the unscaled front. For a
		/// flat window this acts like an eye 24.35 cm in front and 69.44 cm above the screen instead of the
		/// calibrated 20 / 70 cm: the window plane still matches, the playfield is 4.1 to 4.8 mm off.
		/// </summary>
		[Test]
		public void FrontReferencedPlacementDiffersFromVpxByMillimetres()
		{
			// table reference straight into Unity's axes: X right, Z toward the rear, Y up
			var axes = Matrix4x4.zero;
			axes.m00 = axes.m21 = axes.m12 = axes.m33 = 1f;
			CabinetProjectionInput Flat(double3 eye) {
				var input = Input(0, fit: CabinetFitPolicy.Fit, inclination: 0, eye: eye, heightFront: 0.075, heightRear: 0.075);
				input.Cabinet.ScreenWidth = 0.6;
				input.Cabinet.ScreenLength = 1.08;
				input.Table.TableLength = 1.167;
				input.TableToWorld = axes;
				input.ContentBounds = new Bounds(new Vector3(0, 0, 0.6f), Vector3.one * 2f);
				return input;
			}
			var calibrated = CabinetProjectionSolver.Solve(Flat(new double3(0, -0.20, 0.70)));
			var vpxLike = CabinetProjectionSolver.Solve(Flat(new double3(0, -0.2435, 0.6944)));
			Assert.That(calibrated.IsValid && vpxLike.IsValid);

			float OnScreen(in CabinetProjectionResult result, Vector3 point) {
				CabinetProjectionSolver.IntersectPicture(result, axes.MultiplyPoint(point), out var picture);
				return picture.y * 1.08f;
			}
			// the window plane (75 mm above the playfield) maps the same from both eyes
			Assert.That(OnScreen(calibrated, new Vector3(0, 0.6f, 0.075f)) - OnScreen(vpxLike, new Vector3(0, 0.6f, 0.075f)), Is.EqualTo(0f).Within(1e-5f));
			// the playfield doesn't: 4.1 mm at the front to 4.8 mm at the rear
			Assert.That(OnScreen(calibrated, new Vector3(0, 0, 0)) - OnScreen(vpxLike, new Vector3(0, 0, 0)), Is.EqualTo(0.00409f).Within(0.0001f));
			Assert.That(OnScreen(calibrated, new Vector3(0, 1.167f, 0)) - OnScreen(vpxLike, new Vector3(0, 1.167f, 0)), Is.EqualTo(0.0048f).Within(0.0001f));
		}

		[Test]
		public void RejectsInvalidSetups()
		{
			var input = Input();
			input.Cabinet.Eye = new double3(0, 0.4, -0.5); // below the screen
			Assert.That(CabinetProjectionSolver.Solve(input).IsValid, Is.False);

			input = Input();
			input.Cabinet.ScreenWidth = 0;
			Assert.That(CabinetProjectionSolver.Solve(input).IsValid, Is.False);

			input = Input();
			input.Table.ScaleLength = double.NaN;
			Assert.That(CabinetProjectionSolver.Solve(input).IsValid, Is.False);

			// unevenly scaled table: the screen is no longer a rectangle in it
			input = Input(0, Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0, 20, 0), new Vector3(1f, 1.4f, 0.8f)) * Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(25, 40, 10), Vector3.one));
			Assert.That(CabinetProjectionSolver.Solve(input).IsValid, Is.False);
			Assert.That(CabinetProjectionSolver.IsSupportedTableTransform(Matrix4x4.Scale(new Vector3(1f, 1.2f, 1f)), out _), Is.False);
			Assert.That(CabinetProjectionSolver.IsSupportedTableTransform(Matrix4x4.TRS(Vector3.one, Quaternion.Euler(10, 20, 30), Vector3.one * 2f), out _), Is.True);
		}

		[Test]
		public void EnclosingMetadataCoversTheFrustum()
		{
			var result = CabinetProjectionSolver.Solve(Input(1));
			var yExtent = Mathf.Tan(result.EnclosingFieldOfView * 0.5f * Mathf.Deg2Rad) * result.Near;
			var xExtent = yExtent * result.EnclosingAspect;
			Assert.That(yExtent, Is.GreaterThanOrEqualTo(Mathf.Max(Mathf.Abs(result.Bottom), Mathf.Abs(result.Top)) - 1e-6f));
			Assert.That(xExtent, Is.GreaterThanOrEqualTo(Mathf.Max(Mathf.Abs(result.Left), Mathf.Abs(result.Right)) - 1e-6f));
			Assert.That(result.PictureAspect, Is.EqualTo(0.93f / 0.523f).Within(1e-4f));
		}

		private static void AssertNdc(Vector2 ndc, float x, float y)
		{
			Assert.That(ndc.x, Is.EqualTo(x).Within(1e-4f));
			Assert.That(ndc.y, Is.EqualTo(y).Within(1e-4f));
		}

		private static void AssertPoint(Vector3 actual, Vector3 expected)
		{
			Assert.That((actual - expected).magnitude, Is.LessThan(1e-5f), $"{actual} != {expected}");
		}
	}
}
