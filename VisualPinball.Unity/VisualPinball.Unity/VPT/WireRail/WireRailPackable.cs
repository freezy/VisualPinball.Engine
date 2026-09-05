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
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Package payload of a <see cref="WireRailComponent"/>: every authored value except the
	/// route, which travels with the spline child as <see cref="WireRailSplinePackable"/>.
	///
	/// Evolving the format: the JSON packer ignores unknown fields and leaves missing ones at
	/// their default, so adding a field never breaks an old table by itself. It does read as
	/// zero, though, which is rarely the authored default. Bump <see cref="CurrentVersion"/>
	/// and gate the new field on the version passed into the restore methods, falling back to
	/// the fixture's Default constant. Never change a fixture <c>Kind</c> string; add an alias
	/// in <see cref="WireRailFixturePackable.ToFixture"/> if a kind has to be renamed. Golden
	/// payloads of every past version live in WireRailPackagingTests.
	/// </summary>
	public struct WireRailPackable
	{
		public const int CurrentVersion = 1;

		public int Version;
		public int RailCount;
		public float WireDiameter;
		public float WireCapBevelSize;
		public int RadialSegments;
		public int RenderSamplesPerSegment;
		public float ReferenceBallDiameter;
		public int ColliderSamplesPerSegment;
		public bool ShowColliderPreview;
		public bool OverwritePhysics;
		public float Elasticity;
		public float ElasticityFalloff;
		public float Friction;
		public float Scatter;
		public WireRailLayoutPackable[] Layouts;
		public int[] LayoutDisplayOrder;
		public WireRailFixturePackable[] Fixtures;

		public static byte[] Pack(WireRailComponent comp)
		{
			return PackageApi.Packer.Pack(new WireRailPackable {
				Version = CurrentVersion,
				RailCount = comp.RailCount,
				WireDiameter = comp.WireDiameter,
				WireCapBevelSize = comp.WireCapBevelSize,
				RadialSegments = comp.RadialSegments,
				RenderSamplesPerSegment = comp.RenderSamplesPerSegment,
				ReferenceBallDiameter = comp.ReferenceBallDiameter,
				ColliderSamplesPerSegment = comp.ColliderSamplesPerSegment,
				ShowColliderPreview = comp.ShowColliderPreview,
				OverwritePhysics = comp.PhysicsOverwrite,
				Elasticity = comp.PhysicsElasticity,
				ElasticityFalloff = comp.PhysicsElasticityFalloff,
				Friction = comp.PhysicsFriction,
				Scatter = comp.PhysicsScatter,
				Layouts = comp.Segments.Select(WireRailLayoutPackable.From).ToArray(),
				LayoutDisplayOrder = comp.LayoutDisplayOrder.ToArray(),
				Fixtures = comp.Fixtures.Select(WireRailFixturePackable.From).ToArray(),
			});
		}

		public static void Unpack(byte[] bytes, WireRailComponent comp)
		{
			var data = PackageApi.Packer.Unpack<WireRailPackable>(bytes);
			if (data.Version > CurrentVersion) {
				Debug.LogWarning($"Wire rail \"{comp.name}\" was packaged by a newer VPE " +
					$"(format {data.Version}, this build reads {CurrentVersion}); loading what is understood.");
			}
			var layouts = (data.Layouts ?? Array.Empty<WireRailLayoutPackable>())
				.Select(layout => layout.ToSegment(data.Version)).ToList();
			var fixtures = (data.Fixtures ?? Array.Empty<WireRailFixturePackable>())
				.Select(fixture => fixture.ToFixture(data.Version))
				.Where(fixture => fixture != null).ToList();
			comp.RestoreFromPackage(data, layouts,
				(data.LayoutDisplayOrder ?? Array.Empty<int>()).ToList(), fixtures);
		}
	}

	/// <summary>
	/// The physics material assets of a wire rail, resolved through the package's asset table.
	/// </summary>
	public struct WireRailReferencesPackable
	{
		public int PhysicsMaterialRef;
		public int TerminalPhysicsMaterialRef;

		public static byte[] Pack(WireRailComponent comp, PackagedFiles files)
		{
			return PackageApi.Packer.Pack(new WireRailReferencesPackable {
				PhysicsMaterialRef = files.AddAsset(comp.PhysicsMaterialReference),
				TerminalPhysicsMaterialRef = files.AddAsset(comp.TerminalPhysicsMaterialReference),
			});
		}

		public static void Unpack(byte[] bytes, WireRailComponent comp, PackagedFiles files)
		{
			var data = PackageApi.Packer.Unpack<WireRailReferencesPackable>(bytes);
			comp.PhysicsMaterialReference = files.GetAsset<PhysicsMaterialAsset>(data.PhysicsMaterialRef);
			comp.TerminalPhysicsMaterialReference =
				files.GetAsset<PhysicsMaterialAsset>(data.TerminalPhysicsMaterialRef);
		}
	}

	public struct WireRailLayoutPackable
	{
		public float Distance;
		public int ThirdRailSide;
		public PackableFloat2[] RailOffsets;
		public bool[] ActiveRails;
		public float[] WireDiameters;
		public WireRailTransitionPackable[] Transitions;

		public static WireRailLayoutPackable From(WireRailSegment segment)
		{
			var railCount = segment.RailCount;
			return new WireRailLayoutPackable {
				Distance = segment.Distance,
				ThirdRailSide = (int)segment.ThirdRailSide,
				RailOffsets = Enumerable.Range(0, railCount)
					.Select(i => { var o = segment.GetRailOffset(i); return new PackableFloat2(o.x, o.y); })
					.ToArray(),
				ActiveRails = Enumerable.Range(0, railCount).Select(segment.IsRailActive).ToArray(),
				WireDiameters = Enumerable.Range(0, railCount).Select(segment.GetWireDiameter).ToArray(),
				Transitions = Enumerable.Range(0, segment.ConnectionToNext.WireCount)
					.Select(i => WireRailTransitionPackable.From(segment.ConnectionToNext, i))
					.ToArray(),
			};
		}

		/// <param name="version">Payload version, for gating fields added after version 1.</param>
		public WireRailSegment ToSegment(int version)
		{
			var offsets = (RailOffsets ?? Array.Empty<PackableFloat2>())
				.Select(o => new Vector2(o.X, o.Y)).ToList();
			var active = (ActiveRails ?? Array.Empty<bool>()).ToList();
			var diameters = (WireDiameters ?? Array.Empty<float>()).ToList();
			var wires = (Transitions ?? Array.Empty<WireRailTransitionPackable>())
				.Select(t => t.ToTransition()).ToList();
			var connection = new WireRailConnection();
			connection.Restore(wires);
			var segment = new WireRailSegment();
			segment.Restore(Distance, (WireRailThirdRailSide)ThirdRailSide, offsets, active,
				diameters, connection);
			return segment;
		}
	}

	public struct WireRailTransitionPackable
	{
		public bool Overridden;
		public bool Continuous;
		public AnimationCurve Curve;

		public static WireRailTransitionPackable From(WireRailConnection connection, int wireIndex)
			=> new() {
				Overridden = connection.IsWireOverridden(wireIndex),
				Continuous = connection.IsWireContinuous(wireIndex),
				Curve = connection.GetWireCurve(wireIndex),
			};

		public WireRailTransition ToTransition()
		{
			var transition = new WireRailTransition();
			transition.Restore(Overridden, Continuous,
				Curve ?? AnimationCurve.Linear(0f, 0f, 1f, 1f));
			return transition;
		}
	}

	/// <summary>
	/// One flat record for every fixture kind. <see cref="Kind"/> says which fields matter;
	/// the rest stay at their defaults, which keeps the payload readable and forward-tolerant.
	/// </summary>
	public struct WireRailFixturePackable
	{
		public const string RingKind = "Ring";
		public const string CradleKind = "Cradle";
		public const string RungKind = "Rung";
		public const string StandKind = "Stand";
		public const string HairpinKind = "Hairpin";
		public const string ElbowKind = "Elbow";
		public const string RailTrimKind = "RailTrim";

		public string Kind;
		// common
		public float Distance;
		public float SolderThreshold;
		public float SolderSize;
		public bool Enabled;
		public float Diameter;
		// ring
		public bool HasCutout;
		public float CutoutStartAngle;
		public float CutoutEndAngle;
		public bool HasStraightSection;
		public float StraightStartAngle;
		public float StraightEndAngle;
		public float Scale;
		public int RingDensity;
		// shared by several kinds
		public float LateralOffset;
		public float VerticalOffset;
		public float Angle;
		public float Rotation;
		public float LengthAdjustment;
		public int Endpoint;
		public int FirstRailIndex;
		public int SecondRailIndex;
		public float[] RailOffsets;
		// cradle
		public float BottomLength;
		public float LeftLength;
		public float RightLength;
		public float CornerRadius;
		// hairpin
		public float LoopDiameter;
		public float LeadLength;
		public float TangentLength;
		public float RailOffset;
		// elbow
		public float Offset;
		public float DropLength;
		public float ZAngle;
		// stand
		public int LegSide;
		public PackableFloat3 StartDirection;
		public float StartLength;
		public PackableFloat3 FootPosition;
		public PackableFloat3 FootRotation;
		public bool FootClockwise;
		public float FootWidth;
		public float FootLength;
		public float FootConnectionLength;

		public static WireRailFixturePackable From(WireRailFixture fixture)
		{
			var data = new WireRailFixturePackable {
				Distance = fixture.Distance,
				SolderThreshold = fixture.SolderThreshold,
				SolderSize = fixture.SolderSize,
				Enabled = fixture.Enabled,
			};
			switch (fixture) {
				case WireRailRingFixture ring:
					data.Kind = RingKind;
					data.Diameter = ring.Diameter;
					data.HasCutout = ring.HasCutout;
					data.CutoutStartAngle = ring.CutoutStartAngle;
					data.CutoutEndAngle = ring.CutoutEndAngle;
					data.HasStraightSection = ring.HasStraightSection;
					data.StraightStartAngle = ring.StraightStartAngle;
					data.StraightEndAngle = ring.StraightEndAngle;
					data.LateralOffset = ring.LateralOffset;
					data.VerticalOffset = ring.VerticalOffset;
					data.Scale = ring.Scale;
					data.RingDensity = ring.RingDensity;
					break;
				case WireRailCradleFixture cradle:
					data.Kind = CradleKind;
					data.Diameter = cradle.Diameter;
					data.RingDensity = cradle.RingDensity;
					data.LateralOffset = cradle.LateralOffset;
					data.VerticalOffset = cradle.VerticalOffset;
					data.BottomLength = cradle.BottomLength;
					data.LeftLength = cradle.LeftLength;
					data.RightLength = cradle.RightLength;
					data.Angle = cradle.Angle;
					data.Rotation = cradle.Rotation;
					data.CornerRadius = cradle.CornerRadius;
					break;
				case WireRailRungFixture rung:
					data.Kind = RungKind;
					data.Diameter = rung.Diameter;
					data.FirstRailIndex = rung.StartRailIndex;
					data.SecondRailIndex = rung.EndRailIndex;
					data.Angle = rung.Angle;
					data.LateralOffset = rung.LateralOffset;
					data.VerticalOffset = rung.VerticalOffset;
					data.LengthAdjustment = rung.LengthAdjustment;
					break;
				case WireRailStandFixture stand:
					data.Kind = StandKind;
					data.Diameter = stand.Diameter;
					data.LegSide = (int)stand.LegSide;
					data.LateralOffset = stand.LateralOffset;
					data.VerticalOffset = stand.VerticalOffset;
					data.LengthAdjustment = stand.LengthAdjustment;
					data.StartDirection = ToPackable(stand.StartDirection);
					data.StartLength = stand.StartLength;
					data.FootPosition = ToPackable(stand.FootPosition);
					data.FootRotation = ToPackable(stand.FootRotation);
					data.FootClockwise = stand.FootClockwise;
					data.FootWidth = stand.FootWidth;
					data.FootLength = stand.FootLength;
					data.FootConnectionLength = stand.FootConnectionLength;
					break;
				case WireRailHairpinFixture hairpin:
					data.Kind = HairpinKind;
					data.Diameter = hairpin.Diameter;
					data.Endpoint = (int)hairpin.Endpoint;
					data.FirstRailIndex = hairpin.FirstRailIndex;
					data.SecondRailIndex = hairpin.SecondRailIndex;
					data.LoopDiameter = hairpin.LoopDiameter;
					data.LeadLength = hairpin.LeadLength;
					data.TangentLength = hairpin.TangentLength;
					data.RingDensity = hairpin.RingDensity;
					data.RailOffset = hairpin.RailOffset;
					data.Rotation = hairpin.Rotation;
					break;
				case WireRailElbowFixture elbow:
					data.Kind = ElbowKind;
					data.Diameter = elbow.Diameter;
					data.Endpoint = (int)elbow.Endpoint;
					data.FirstRailIndex = elbow.FirstRailIndex;
					data.SecondRailIndex = elbow.SecondRailIndex;
					data.Offset = elbow.Offset;
					data.DropLength = elbow.DropLength;
					data.ZAngle = elbow.ZAngle;
					data.RailOffsets = elbow.RailOffsets.ToArray();
					break;
				case WireRailTrimFixture trim:
					data.Kind = RailTrimKind;
					data.Endpoint = (int)trim.Endpoint;
					data.RailOffsets = trim.RailOffsets.ToArray();
					break;
			}
			return data;
		}

		/// <param name="version">Payload version, for gating fields added after version 1.</param>
		public WireRailFixture ToFixture(int version)
		{
			var railOffsets = (RailOffsets ?? Array.Empty<float>()).ToList();
			WireRailFixture fixture;
			switch (Kind) {
				case RingKind: {
					var ring = new WireRailRingFixture();
					ring.Restore(Diameter, HasCutout, CutoutStartAngle, CutoutEndAngle,
						HasStraightSection, StraightStartAngle, StraightEndAngle,
						LateralOffset, VerticalOffset, Scale, RingDensity);
					fixture = ring;
					break;
				}
				case CradleKind: {
					var cradle = new WireRailCradleFixture();
					cradle.Restore(Diameter, RingDensity, LateralOffset, VerticalOffset,
						BottomLength, LeftLength, RightLength, Angle, Rotation, CornerRadius);
					fixture = cradle;
					break;
				}
				case RungKind: {
					var rung = new WireRailRungFixture();
					rung.Restore(Diameter, FirstRailIndex, SecondRailIndex, Angle,
						LateralOffset, VerticalOffset, LengthAdjustment);
					fixture = rung;
					break;
				}
				case StandKind: {
					var stand = new WireRailStandFixture();
					stand.Restore(Diameter, (WireRailStandSide)LegSide, LateralOffset,
						VerticalOffset, LengthAdjustment, ToVector(StartDirection), StartLength,
						ToVector(FootPosition), ToVector(FootRotation), FootClockwise, FootWidth,
						FootLength, FootConnectionLength);
					fixture = stand;
					break;
				}
				case HairpinKind: {
					var hairpin = new WireRailHairpinFixture();
					hairpin.Restore(Diameter, (WireRailEndpoint)Endpoint, FirstRailIndex,
						SecondRailIndex, LoopDiameter, LeadLength, TangentLength, RingDensity,
						RailOffset, Rotation);
					fixture = hairpin;
					break;
				}
				case ElbowKind: {
					var elbow = new WireRailElbowFixture();
					elbow.Restore(Diameter, (WireRailEndpoint)Endpoint, FirstRailIndex,
						SecondRailIndex, Offset, DropLength, ZAngle, railOffsets);
					fixture = elbow;
					break;
				}
				case RailTrimKind: {
					var trim = new WireRailTrimFixture();
					trim.Restore((WireRailEndpoint)Endpoint, railOffsets);
					fixture = trim;
					break;
				}
				default:
					// A kind this build does not know, most likely from a newer VPE. The rest of
					// the rail still loads.
					Debug.LogWarning($"Unknown wire rail fixture kind \"{Kind}\" in package, skipping.");
					return null;
			}
			fixture.RestoreCommon(Distance, SolderThreshold, SolderSize, Enabled);
			return fixture;
		}

		private static PackableFloat3 ToPackable(Vector3 v) => new(v.x, v.y, v.z);
		private static Vector3 ToVector(PackableFloat3 v) => new(v.X, v.Y, v.Z);
	}

	/// <summary>
	/// The authored route of a wire rail: the knots of the first spline in the child's
	/// container plus its closed flag. Positions are in the child's local (VPX) space.
	/// </summary>
	public struct WireRailSplinePackable
	{
		private const int CurrentVersion = 1;

		public int Version;
		public bool Closed;
		public WireRailKnotPackable[] Knots;

		public static byte[] Pack(WireRailSplineComponent comp)
		{
			var container = comp.GetComponent<SplineContainer>();
			var spline = container ? container.Spline : null;
			var knots = new List<WireRailKnotPackable>();
			if (spline != null) {
				for (var i = 0; i < spline.Count; i++) {
					var knot = spline[i];
					knots.Add(new WireRailKnotPackable {
						Position = new PackableFloat3(knot.Position.x, knot.Position.y, knot.Position.z),
						TangentIn = new PackableFloat3(knot.TangentIn.x, knot.TangentIn.y, knot.TangentIn.z),
						TangentOut = new PackableFloat3(knot.TangentOut.x, knot.TangentOut.y, knot.TangentOut.z),
						Rotation = new PackableFloat4(knot.Rotation.value.x, knot.Rotation.value.y,
							knot.Rotation.value.z, knot.Rotation.value.w),
						TangentMode = (int)spline.GetTangentMode(i),
					});
				}
			}
			return PackageApi.Packer.Pack(new WireRailSplinePackable {
				Version = CurrentVersion,
				Closed = spline?.Closed ?? false,
				Knots = knots.ToArray(),
			});
		}

		public static void Unpack(byte[] bytes, WireRailSplineComponent comp)
		{
			var data = PackageApi.Packer.Unpack<WireRailSplinePackable>(bytes);
			var spline = new Spline();
			foreach (var knot in data.Knots ?? Array.Empty<WireRailKnotPackable>()) {
				var rotation = new quaternion(knot.Rotation.X, knot.Rotation.Y, knot.Rotation.Z,
					knot.Rotation.W);
				if (math.lengthsq(rotation.value) < 1e-8f) {
					rotation = quaternion.identity;
				}
				spline.Add(new BezierKnot(
						new float3(knot.Position.X, knot.Position.Y, knot.Position.Z),
						new float3(knot.TangentIn.X, knot.TangentIn.Y, knot.TangentIn.Z),
						new float3(knot.TangentOut.X, knot.TangentOut.Y, knot.TangentOut.Z),
						rotation),
					(TangentMode)knot.TangentMode);
			}
			spline.Closed = data.Closed;
			comp.RestoreSpline(spline);
		}
	}

	public struct WireRailKnotPackable
	{
		public PackableFloat3 Position;
		public PackableFloat3 TangentIn;
		public PackableFloat3 TangentOut;
		public PackableFloat4 Rotation;
		public int TangentMode;
	}

	public struct PackableFloat4
	{
		public float X;
		public float Y;
		public float Z;
		public float W;

		public PackableFloat4(float x, float y, float z, float w)
		{
			X = x;
			Y = y;
			Z = z;
			W = w;
		}
	}
}
