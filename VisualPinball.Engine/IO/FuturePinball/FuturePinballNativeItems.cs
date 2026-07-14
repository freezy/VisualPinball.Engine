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

using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Bumper;
using VisualPinball.Engine.VPT.Flipper;
using VisualPinball.Engine.VPT.Gate;
using VisualPinball.Engine.VPT.HitTarget;
using VisualPinball.Engine.VPT.Kicker;
using VisualPinball.Engine.VPT.Light;
using VisualPinball.Engine.VPT.MetalWireGuide;
using VisualPinball.Engine.VPT.Plunger;
using VisualPinball.Engine.VPT.Ramp;
using VisualPinball.Engine.VPT.Rubber;
using VisualPinball.Engine.VPT.Spinner;
using VisualPinball.Engine.VPT.Surface;
using VisualPinball.Engine.VPT.Trigger;

namespace VisualPinball.Engine.IO.FuturePinball
{
	public sealed class FuturePinballNativeItem
	{
		public IItem Item { get; internal set; }
		public IReadOnlyList<string> DefaultedParameters { get; internal set; } = Array.Empty<string>();
	}

	/// <summary>
	/// Converts Future Pinball elements whose behavior has a direct VPE counterpart.
	/// Source values are copied only when their meaning and units are known; all other
	/// values intentionally retain the VPE item's defaults.
	/// </summary>
	public static class FuturePinballNativeItemConverter
	{
		/// <summary>
		/// Synthetic playfield image name shared by imported insert lights and their table.
		/// VPE uses this equality to select and retain the insert-light mesh.
		/// </summary>
		public const string PlayfieldImage = "Future Pinball Playfield";

		private const uint NameTag = 0xA4F4D1D7;
		private const uint SurfaceTag = 0xA3EFBDD2;
		private const uint RotationTag = 0xA8EDC3D3;
		private const uint StartAngleTag = 0xA900BED2;
		private const uint SwingTag = 0xA2EABFE4;
		private const uint ReflectsTag = 0x9DFBCDD3;
		private const uint RenderModelTag = 0xA5F2C5D3;
		private const uint KickerTypeTag = 0x99E8BEDA;
		private const uint PassiveTag = 0xA0EED1D5;
		private const uint TriggerSkirtTag = 0x9EEED1DD;
		private const uint OneWayTag = 0x9100BBD6;
		private const uint OffsetTag = 0x96FBCCD6;
		private const uint HeightTag = 0xA2F8CDDD;
		private const uint WidthTag = 0x95FDC9CE;
		private const uint CollidableTag = 0x9DF5C3E2;
		private const uint RenderObjectTag = 0x97FDC4D3;
		private const uint TopHeightTag = 0x99F2BEDD;
		private const uint BottomHeightTag = 0x95F2D0DD;
		private const uint StartHeightTag = 0xA2F8CAD2;
		private const uint EndHeightTag = 0xA2F8CAE0;
		private const uint StartWidthTag = 0xA5F8BBD2;
		private const uint EndWidthTag = 0xA5F8BBE0;
		private const uint LeftSideHeightTag = 0xA2F8CAD9;
		private const uint RightSideHeightTag = 0xA2F8CAD3;
		private const uint StateTag = 0x9600BED2;
		private const uint BlinkIntervalTag = 0x95F3C9E3;
		private const uint BlinkPatternTag = 0x9600C2E3;
		private const uint LitColorTag = 0x9DF2CFD9;
		private const uint UnlitColorTag = 0x9DF2CFD0;
		private const uint GlowCenterTag = 0x9BFCCFDD;
		private const uint DiameterTag = 0x9D00C9E1;
		private const uint GlowRadiusTag = 0x96FDD1D3;
		private const uint GenerateHitEventTag = 0x95EBCDDD;

		public static bool TryConvert(FuturePinballSourceStream element, out FuturePinballNativeItem converted)
		{
			if (element == null) throw new ArgumentNullException(nameof(element));
			converted = null;
			if (!element.ElementType.HasValue) return false;

				switch (element.ElementType.Value) {
				case FuturePinballElementType.Surface:
				case FuturePinballElementType.GuideWall:
					return Result(Surface(element), new[] { "physics response" }, out converted);
				case FuturePinballElementType.Ramp:
				case FuturePinballElementType.WireRamp:
					return Result(Ramp(element), new[] { "physics response" }, out converted);
				case FuturePinballElementType.ShapeableRubber:
					return Result(ShapeableRubber(element), new[] { "thickness", "physics response", "slingshot and leaf-trigger point behavior" }, out converted);
				case FuturePinballElementType.WireGuide:
					return Result(WireGuide(element), new[] { "bend radius", "physics response" }, out converted);
				case FuturePinballElementType.Flipper:
					return Result(Flipper(element), new[] { "dimensions", "strength scale", "elasticity scale", "return and damping" }, out converted);
				case FuturePinballElementType.Bumper:
					return Result(Bumper(element), new[] { "radius", "strength scale when active", "lamp and skirt-switch scripting", "animation timing" }, out converted);
				case FuturePinballElementType.LeafTarget:
					return Result(Target(element, false), new[] { "dimensions", "physics response" }, out converted);
				case FuturePinballElementType.DropTarget:
					return Result(Target(element, true), new[] { "dimensions", "multi-target bank expansion and grouping", "animation timing", "physics response" }, out converted);
				case FuturePinballElementType.Plunger:
					return Result(Plunger(element, false), new[] { "rotation", "dimensions", "stroke", "strength scale", "pull and fire speed" }, out converted);
				case FuturePinballElementType.AutoPlunger:
					return Result(Plunger(element, true), new[] { "rotation", "dimensions", "stroke", "strength scale", "fire speed" }, out converted);
				case FuturePinballElementType.Kicker:
					return Result(Kicker(element), new[] { "radius", "strength scale", "scatter and hit accuracy" }, out converted);
				case FuturePinballElementType.EmKicker:
					return Result(Kicker(element), new[] { "radius", "strength scale", "scatter and hit accuracy" }, out converted);
				case FuturePinballElementType.Trigger:
					return Result(Trigger(element, false), new[] { "trigger footprint", "animation speed" }, out converted);
				case FuturePinballElementType.OptoTrigger:
					return Result(Trigger(element, true), new[] { "beam length", "animation speed" }, out converted);
				case FuturePinballElementType.Gate:
					return Result(Gate(element), new[] { "dimensions", "travel angles", "damping and physics response" }, out converted);
				case FuturePinballElementType.Spinner:
					return Result(Spinner(element), new[] { "dimensions", "damping scale", "physics response" }, out converted);
				case FuturePinballElementType.RoundLight:
				case FuturePinballElementType.ShapeableLight:
				case FuturePinballElementType.Bulb:
					return Result(Light(element), new[] { "lens and halo geometry", "intensity", "fade speeds" }, out converted);
				default:
					return false;
			}
		}

		private static bool Result(IItem item, IReadOnlyList<string> defaults, out FuturePinballNativeItem converted)
		{
			if (item == null) {
				converted = null;
				return false;
			}
			converted = new FuturePinballNativeItem { Item = item, DefaultedParameters = defaults };
			return true;
		}

		private static Surface Surface(FuturePinballSourceStream element)
		{
			var points = DragPoints(element);
			if (points.Length < 3) return null;
			var isGuideWall = element.ElementType == FuturePinballElementType.GuideWall;
			return new Surface(new SurfaceData(Name(element), points) {
				HeightTop = ToVpx(FuturePinballElementGeometry.Float(element, isGuideWall ? HeightTag : TopHeightTag)),
				HeightBottom = isGuideWall ? 0f : ToVpx(FuturePinballElementGeometry.Float(element, BottomHeightTag)),
				IsCollidable = FuturePinballElementGeometry.Integer(element, CollidableTag, 1) != 0,
				IsTopBottomVisible = FuturePinballElementGeometry.Integer(element, RenderObjectTag, 1) != 0,
				IsSideVisible = FuturePinballElementGeometry.Integer(element, RenderObjectTag, 1) != 0,
				IsReflectionEnabled = FuturePinballElementGeometry.Integer(element, ReflectsTag, 1) != 0,
				HitEvent = FuturePinballElementGeometry.Integer(element, GenerateHitEventTag) != 0
			});
		}

		private static Ramp Ramp(FuturePinballSourceStream element)
		{
			var points = DragPoints(element);
			if (points.Length < 2) return null;
			var isWire = element.ElementType == FuturePinballElementType.WireRamp;
			return new Ramp(new RampData(Name(element), points) {
				HeightBottom = ToVpx(FuturePinballElementGeometry.Integer(element, StartHeightTag)),
				HeightTop = ToVpx(FuturePinballElementGeometry.Integer(element, EndHeightTag)),
				WidthBottom = ToVpx(FuturePinballElementGeometry.Integer(element, StartWidthTag, 40)),
				WidthTop = ToVpx(FuturePinballElementGeometry.Integer(element, EndWidthTag, 40)),
				LeftWallHeight = ToVpx(FuturePinballElementGeometry.Integer(element, LeftSideHeightTag)),
				RightWallHeight = ToVpx(FuturePinballElementGeometry.Integer(element, RightSideHeightTag)),
				LeftWallHeightVisible = ToVpx(FuturePinballElementGeometry.Integer(element, LeftSideHeightTag)),
				RightWallHeightVisible = ToVpx(FuturePinballElementGeometry.Integer(element, RightSideHeightTag)),
				RampType = isWire ? VPT.RampType.RampType2Wire : VPT.RampType.RampTypeFlat,
				WireDiameter = ToVpx(6f),
				WireDistanceX = ToVpx(30f),
				IsCollidable = FuturePinballElementGeometry.Integer(element, CollidableTag, 1) != 0,
				IsVisible = FuturePinballElementGeometry.Integer(element, RenderObjectTag, 1) != 0,
				IsReflectionEnabled = FuturePinballElementGeometry.Integer(element, ReflectsTag, 1) != 0
			});
		}

		private static Rubber ShapeableRubber(FuturePinballSourceStream element)
		{
			var points = DragPoints(element);
			if (points.Length < 2) return null;
			var offset = ToVpx(FuturePinballElementGeometry.Integer(element, OffsetTag));
			return new Rubber(new RubberData(Name(element)) {
				DragPoints = points,
				Height = offset,
				HitHeight = offset,
				IsReflectionEnabled = FuturePinballElementGeometry.Integer(element, ReflectsTag, 1) != 0
			});
		}

		private static MetalWireGuide WireGuide(FuturePinballSourceStream element)
		{
			var points = DragPoints(element);
			if (points.Length < 2) return null;
			var height = ToVpx(FuturePinballElementGeometry.Integer(element, HeightTag, 25));
			return new MetalWireGuide(new MetalWireGuideData(Name(element)) {
				DragPoints = points,
				Height = height,
				HitHeight = height,
				Thickness = ToVpx(FuturePinballElementGeometry.Integer(element, WidthTag, 3)),
				IsReflectionEnabled = FuturePinballElementGeometry.Integer(element, ReflectsTag, 1) != 0
			});
		}

		private static Flipper Flipper(FuturePinballSourceStream element)
		{
			var position = Position(element);
			var data = new FlipperData(Name(element), position.X, position.Y) {
				Surface = SurfaceName(element),
				IsReflectionEnabled = FuturePinballElementGeometry.Integer(element, ReflectsTag, 1) != 0
			};
			if (FuturePinballElementGeometry.HasTag(element, StartAngleTag)) {
				data.StartAngle = FuturePinballElementGeometry.Integer(element, StartAngleTag);
				if (FuturePinballElementGeometry.HasTag(element, SwingTag)) {
					data.EndAngle = data.StartAngle + FuturePinballElementGeometry.Integer(element, SwingTag);
				}
			}
			return new Flipper(data);
		}

		private static Bumper Bumper(FuturePinballSourceStream element)
		{
			var position = Position(element);
			var data = new BumperData(Name(element), position.X, position.Y) {
				Surface = SurfaceName(element),
				IsReflectionEnabled = FuturePinballElementGeometry.Integer(element, ReflectsTag, 1) != 0
			};
			// An FP bumper without its trigger skirt cannot react to a ball hit, even when Passive is clear.
			if (FuturePinballElementGeometry.Integer(element, PassiveTag) != 0
				|| FuturePinballElementGeometry.Integer(element, TriggerSkirtTag, 1) == 0) data.Force = 0f;
			return new Bumper(data);
		}

		private static HitTarget Target(FuturePinballSourceStream element, bool drop)
		{
			var position = Position(element);
			return new HitTarget(new HitTargetData(Name(element), position.X, position.Y) {
				Position = new Vertex3D(position.X, position.Y, 0f),
				RotZ = FuturePinballElementGeometry.Integer(element, RotationTag),
				TargetType = drop ? VPT.TargetType.DropTargetSimple : VPT.TargetType.HitTargetRectangle,
				IsReflectionEnabled = FuturePinballElementGeometry.Integer(element, ReflectsTag, 1) != 0
			});
		}

		private static Plunger Plunger(FuturePinballSourceStream element, bool automatic)
		{
			var position = Position(element);
			return new Plunger(new PlungerData(Name(element), position.X, position.Y) {
				Surface = SurfaceName(element),
				AutoPlunger = automatic,
				IsMechPlunger = !automatic,
				IsReflectionEnabled = FuturePinballElementGeometry.Integer(element, ReflectsTag, 1) != 0
			});
		}

		private static Kicker Kicker(FuturePinballSourceStream element)
		{
			var position = Position(element);
			var hasSourceType = FuturePinballElementGeometry.HasTag(element, KickerTypeTag);
			var sourceType = FuturePinballElementGeometry.Integer(element, KickerTypeTag);
			var renderModel = FuturePinballElementGeometry.Integer(element, RenderModelTag, 1) != 0;
			var type = renderModel ? VPT.KickerType.KickerHole : VPT.KickerType.KickerInvisible;
			return new Kicker(new KickerData(Name(element), position.X, position.Y) {
				Surface = SurfaceName(element),
				Orientation = FuturePinballElementGeometry.Integer(element, RotationTag),
				KickerType = type,
				FallThrough = hasSourceType && sourceType == 1
			});
		}

		private static Trigger Trigger(FuturePinballSourceStream element, bool opto)
		{
			var position = Position(element);
			var data = new TriggerData(Name(element), position.X, position.Y) {
				Surface = SurfaceName(element),
				Rotation = FuturePinballElementGeometry.Integer(element, RotationTag),
				Shape = opto ? VPT.TriggerShape.TriggerNone : VPT.TriggerShape.TriggerWireA,
				IsVisible = !opto && FuturePinballElementGeometry.Integer(element, RenderModelTag, 1) != 0
			};
			if (opto && FuturePinballElementGeometry.HasTag(element, WidthTag)) {
				data.Radius = ToVpx(FuturePinballElementGeometry.Integer(element, WidthTag)) / 2f;
			}
			return new Trigger(data);
		}

		private static Gate Gate(FuturePinballSourceStream element)
		{
			var position = Position(element);
			return new Gate(new GateData(Name(element), position.X, position.Y) {
				Surface = SurfaceName(element),
				Rotation = FuturePinballElementGeometry.Integer(element, RotationTag),
				TwoWay = FuturePinballElementGeometry.Integer(element, OneWayTag, 1) == 0,
				IsReflectionEnabled = FuturePinballElementGeometry.Integer(element, ReflectsTag, 1) != 0
			});
		}

		private static Spinner Spinner(FuturePinballSourceStream element)
		{
			var position = Position(element);
			return new Spinner(new SpinnerData(Name(element), position.X, position.Y) {
				Surface = SurfaceName(element),
				Rotation = FuturePinballElementGeometry.Integer(element, RotationTag)
			});
		}

		private static Light Light(FuturePinballSourceStream element)
		{
			var type = element.ElementType.Value;
			var isRound = type == FuturePinballElementType.RoundLight;
			var isShapeable = type == FuturePinballElementType.ShapeableLight;
			var isBulb = type == FuturePinballElementType.Bulb;
			var position = isShapeable ? ShapeCenter(element) : Position(element);
			if (FuturePinballElementGeometry.HasTag(element, GlowCenterTag)) {
				position = Position(element, GlowCenterTag);
			}
			var data = new LightData(Name(element), position.X, position.Y) {
				Surface = SurfaceName(element),
				State = LightState(element),
				BlinkInterval = FuturePinballElementGeometry.Integer(element, BlinkIntervalTag, 125),
				BlinkPattern = FuturePinballElementGeometry.Text(element, BlinkPatternTag, "10"),
				Color = SourceColor(element, LitColorTag, 0x0000ffff),
				Color2 = SourceColor(element, UnlitColorTag, 0x00ffffff),
				OffImage = isBulb ? string.Empty : PlayfieldImage,
				IsRoundLight = isRound,
				IsBulbLight = isBulb,
				ShowBulbMesh = isBulb && FuturePinballElementGeometry.Integer(element, RenderModelTag, 1) != 0,
				DragPoints = isShapeable ? DragPoints(element) : null
			};
			var diameter = FuturePinballElementGeometry.Integer(element, DiameterTag);
			var glowRadius = FuturePinballElementGeometry.Integer(element, GlowRadiusTag);
			if (diameter > 0) data.MeshRadius = ToVpx(diameter) / 2f;
			if (isRound) data.DragPoints = RoundLightPoints(Position(element), data.MeshRadius);
			if (glowRadius > 0) data.Falloff = ToVpx(glowRadius);
			else if (diameter > 0) data.Falloff = ToVpx(diameter) / 2f;
			return new Light(data);
		}

		private static int LightState(FuturePinballSourceStream element)
		{
			// FP persists its documented BulbOff/BulbOn/BulbBlink states as 0/1/2. Unknown or
			// missing values deliberately retain VPE's safe Off default.
			switch (FuturePinballElementGeometry.Integer(element, StateTag, VPT.LightStatus.LightStateOff)) {
				case 1: return VPT.LightStatus.LightStateOn;
				case 2: return VPT.LightStatus.LightStateBlinking;
				default: return VPT.LightStatus.LightStateOff;
			}
		}

		private static Vertex2D ShapeCenter(FuturePinballSourceStream element)
		{
			var points = FuturePinballElementGeometry.Points(element);
			if (points.Count == 0) return Position(element);
			return new Vertex2D(
				ToVpx(points.Average(point => point.Position.X)),
				ToVpx(points.Average(point => point.Position.Y))
			);
		}

		private static DragPointData[] RoundLightPoints(Vertex2D center, float radius)
		{
			const int pointCount = 8;
			var points = new DragPointData[pointCount];
			for (var i = 0; i < pointCount; i++) {
				var angle = -System.Math.PI / 2d + i * 2d * System.Math.PI / pointCount;
				points[i] = new DragPointData(
					center.X + radius * (float)System.Math.Cos(angle),
					center.Y + radius * (float)System.Math.Sin(angle)
				) { IsSmooth = true };
			}
			return points;
		}

		private static DragPointData[] DragPoints(FuturePinballSourceStream element)
		{
			return FuturePinballElementGeometry.Points(element).Select(point => new DragPointData(
				FuturePinballCoordinateConverter.ToVpx(point.Position.X, point.Position.Y, 0f)
			) { IsSmooth = point.Smooth }).ToArray();
		}

		private static Vertex2D Position(FuturePinballSourceStream element, uint tag = FuturePinballElementGeometry.PositionTag)
		{
			var position = FuturePinballElementGeometry.Position(element, tag);
			return new Vertex2D(ToVpx(position.X), ToVpx(position.Y));
		}

		private static string Name(FuturePinballSourceStream element)
		{
			return FuturePinballElementGeometry.Text(element, NameTag, element.Name);
		}

		private static string SurfaceName(FuturePinballSourceStream element)
		{
			return FuturePinballElementGeometry.Text(element, SurfaceTag);
		}

		private static Color SourceColor(FuturePinballSourceStream element, uint tag, uint fallback)
		{
			return new Color(FuturePinballElementGeometry.Color(element, tag, fallback), ColorFormat.Bgr).WithAlpha(255);
		}

		private static float ToVpx(float value) => FuturePinballCoordinateConverter.ToVpx(value);
	}
}
