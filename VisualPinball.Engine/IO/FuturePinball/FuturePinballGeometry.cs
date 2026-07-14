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
using VisualPinball.Engine.VPT.Ramp;
using VisualPinball.Engine.VPT.Surface;

namespace VisualPinball.Engine.IO.FuturePinball
{
	public readonly struct FuturePinballWorldPoint
	{
		public float X { get; }
		public float Y { get; }
		public float Z { get; }

		public FuturePinballWorldPoint(float x, float y, float z)
		{
			X = x;
			Y = y;
			Z = z;
		}
	}

	public sealed class FuturePinballShapePoint
	{
		public FuturePinballVector2 Position { get; internal set; }
		public bool Smooth { get; internal set; }
		public bool IsRampEndPoint { get; internal set; }
		public bool LeftGuide { get; internal set; }
		public bool LeftUpperGuide { get; internal set; }
		public bool RightGuide { get; internal set; }
		public bool RightUpperGuide { get; internal set; }
		public bool TopWire { get; internal set; }
		public int RingType { get; internal set; }
	}

	public sealed class FuturePinballGeneratedElement
	{
		public int SourceIndex { get; internal set; }
		public string Name { get; internal set; }
		public FuturePinballElementType Type { get; internal set; }
		public bool IsCollidable { get; internal set; }
		public string Texture { get; internal set; }
		public uint Color { get; internal set; }
		public FuturePinballMaterialDescription Material { get; internal set; }
		public IReadOnlyList<Mesh> Meshes { get; internal set; } = Array.Empty<Mesh>();
	}

	public static class FuturePinballCoordinateConverter
	{
		// VPE uses 1852.71 VPX units per metre. Future Pinball dimensions are millimetres.
		public const float VpxUnitsPerMillimeter = 1.85271f;
		public const float WorldUnitsPerMillimeter = 0.001f;

		public static float ToVpx(float millimeters) => millimeters * VpxUnitsPerMillimeter;

		public static Vertex3D ToVpx(float x, float y, float z)
		{
			return new Vertex3D(ToVpx(x), ToVpx(y), ToVpx(z));
		}

		public static FuturePinballWorldPoint ToWorld(float x, float y, float z)
		{
			return new FuturePinballWorldPoint(
				x * WorldUnitsPerMillimeter,
				z * WorldUnitsPerMillimeter,
				-y * WorldUnitsPerMillimeter
			);
		}

		public static FuturePinballWorldPoint ModelToWorld(float x, float y, float z)
		{
			return new FuturePinballWorldPoint(
				x * WorldUnitsPerMillimeter,
				y * WorldUnitsPerMillimeter,
				-z * WorldUnitsPerMillimeter
			);
		}

		public static Mesh ModelMeshToWorld(Mesh source, bool flipTextureV = true)
		{
			if (source == null) throw new ArgumentNullException(nameof(source));
			var result = source.Clone();
			for (var i = 0; i < (result.Vertices?.Length ?? 0); i++) {
				var vertex = result.Vertices[i];
				var position = ModelToWorld(vertex.X, vertex.Y, vertex.Z);
				var normal = ModelToWorld(vertex.Nx, vertex.Ny, vertex.Nz);
				vertex.X = position.X;
				vertex.Y = position.Y;
				vertex.Z = position.Z;
				vertex.Nx = normal.X / WorldUnitsPerMillimeter;
				vertex.Ny = normal.Y / WorldUnitsPerMillimeter;
				vertex.Nz = normal.Z / WorldUnitsPerMillimeter;
				if (flipTextureV) vertex.Tv = 1f - vertex.Tv;
				result.Vertices[i] = vertex;
			}
			for (var i = 0; i + 2 < (result.Indices?.Length ?? 0); i += 3) {
				var index = result.Indices[i + 1];
				result.Indices[i + 1] = result.Indices[i + 2];
				result.Indices[i + 2] = index;
			}
			return result;
		}
	}

	public static class FuturePinballElementGeometry
	{
		public const uint PointTag = 0x95F3C2D2;
		public const uint PositionTag = 0x9BFCCFCF;
		public const uint SmoothTag = 0xA1EDC5D2;
		public const uint RampEndPointTag = 0x9AF1CAE0;
		public const uint LeftGuideTag = 0xA4F5C9D8;
		public const uint LeftUpperGuideTag = 0xA4F5C2D0;
		public const uint RightGuideTag = 0xA0EFC9D8;
		public const uint RightUpperGuideTag = 0xA0EFC2D0;
		public const uint TopWireTag = 0x95ECC3D1;
		public const uint RingTypeTag = 0xA2F3C9D3;

		private const uint EndTag = 0xA7FDC4E0;
		private const uint LegacyTagOffset = 0x15BDECDB;

		public static IReadOnlyList<FuturePinballShapePoint> Points(FuturePinballSourceStream element)
		{
			if (element == null) throw new ArgumentNullException(nameof(element));
			var points = new List<FuturePinballShapePoint>();
			for (var i = 0; i < element.Records.Count; i++) {
				if (!Matches(element.Records[i], PointTag)) continue;
				var point = new FuturePinballShapePoint();
				for (i++; i < element.Records.Count && !Matches(element.Records[i], EndTag); i++) {
					var record = element.Records[i];
					if (Matches(record, PositionTag)) point.Position = Vector2(record);
					else if (Matches(record, SmoothTag)) point.Smooth = Integer(record) != 0;
					else if (Matches(record, RampEndPointTag)) point.IsRampEndPoint = Integer(record) != 0;
					else if (Matches(record, LeftGuideTag)) point.LeftGuide = Integer(record) != 0;
					else if (Matches(record, LeftUpperGuideTag)) point.LeftUpperGuide = Integer(record) != 0;
					else if (Matches(record, RightGuideTag)) point.RightGuide = Integer(record) != 0;
					else if (Matches(record, RightUpperGuideTag)) point.RightUpperGuide = Integer(record) != 0;
					else if (Matches(record, TopWireTag)) point.TopWire = Integer(record) != 0;
					else if (Matches(record, RingTypeTag)) point.RingType = Integer(record);
				}
				points.Add(point);
			}
			return points;
		}

		public static FuturePinballVector2 Position(FuturePinballSourceStream element, uint tag = PositionTag)
		{
			var record = Find(element, tag);
			return record == null ? new FuturePinballVector2() : Vector2(record);
		}

		/// <summary>
		/// Returns a representative table position for resolving a shape against its named support surface.
		/// Shape-based FP elements generally have no top-level position, so their first control point is the
		/// only stable point guaranteed to lie on the element itself.
		/// </summary>
		public static FuturePinballVector2 SurfaceProbePosition(FuturePinballSourceStream element)
		{
			if (element == null) throw new ArgumentNullException(nameof(element));
			var points = Points(element);
			return points.Count > 0 ? points[0].Position : Position(element);
		}

		public static bool ContainsPoint(FuturePinballSourceStream element, FuturePinballVector2 point, float edgeTolerance = 0.001f)
		{
			var polygon = Points(element).Select(item => item.Position).ToArray();
			if (polygon.Length < 3) return false;
			var inside = false;
			for (var i = 0; i < polygon.Length; i++) {
				var j = i == 0 ? polygon.Length - 1 : i - 1;
				if (PointOnSegment(point, polygon[j], polygon[i], edgeTolerance)) return true;
				if ((polygon[i].Y > point.Y) != (polygon[j].Y > point.Y)
					&& point.X < (polygon[j].X - polygon[i].X) * (point.Y - polygon[i].Y)
					/ (polygon[j].Y - polygon[i].Y) + polygon[i].X) inside = !inside;
			}
			return inside;
		}

		public static string Text(FuturePinballSourceStream stream, uint tag, string fallback = "")
		{
			var record = Find(stream, tag);
			return record?.Value as string ?? fallback;
		}

		public static int Integer(FuturePinballSourceStream stream, uint tag, int fallback = 0)
		{
			var record = Find(stream, tag);
			return record == null ? fallback : Integer(record);
		}

		public static float Float(FuturePinballSourceStream stream, uint tag, float fallback = 0f)
		{
			var record = Find(stream, tag);
			if (record?.Value is float value) return value;
			// Some FP tags change representation by element type and remain opaque in the generic reader.
			return record?.Payload.Length >= 4 ? ReadSingle(record.Payload.Span) : fallback;
		}

		public static uint Color(FuturePinballSourceStream stream, uint tag, uint fallback = 0xffffffff)
		{
			var record = Find(stream, tag);
			if (record?.Value is uint value) return value;
			return record?.Payload.Length >= 4 ? ReadUInt32(record.Payload.Span) : fallback;
		}

		public static bool HasTag(FuturePinballSourceStream stream, uint tag)
		{
			return Find(stream, tag) != null;
		}

		private static FuturePinballRecord Find(FuturePinballSourceStream stream, uint tag)
		{
			return stream?.Records.FirstOrDefault(record => Matches(record, tag));
		}

		private static int Integer(FuturePinballRecord record)
		{
			if (record.Value is int value) return value;
			return record.Payload.Length >= 4 ? ReadInt32(record.Payload.Span) : 0;
		}

		private static FuturePinballVector2 Vector2(FuturePinballRecord record)
		{
			if (record.Value is FuturePinballVector2 value) return value;
			return record.Payload.Length >= 8
				? new FuturePinballVector2(ReadSingle(record.Payload.Span), ReadSingle(record.Payload.Span.Slice(4)))
				: new FuturePinballVector2();
		}

		private static bool PointOnSegment(FuturePinballVector2 point, FuturePinballVector2 a, FuturePinballVector2 b, float tolerance)
		{
			var segmentX = b.X - a.X;
			var segmentY = b.Y - a.Y;
			var lengthSquared = segmentX * segmentX + segmentY * segmentY;
			if (lengthSquared <= tolerance * tolerance) {
				var pointX = point.X - a.X;
				var pointY = point.Y - a.Y;
				return pointX * pointX + pointY * pointY <= tolerance * tolerance;
			}
			var cross = (point.Y - a.Y) * segmentX - (point.X - a.X) * segmentY;
			if (System.Math.Abs(cross) > tolerance * System.Math.Sqrt(lengthSquared)) return false;
			var dot = (point.X - a.X) * segmentX + (point.Y - a.Y) * segmentY;
			if (dot < 0f) return false;
			return dot <= lengthSquared;
		}

		private static bool Matches(FuturePinballRecord record, uint tag)
		{
			return record.CanonicalTag == tag || record.OriginalTag == tag || record.OriginalTag - LegacyTagOffset == tag;
		}

		private static int ReadInt32(ReadOnlySpan<byte> data) => unchecked((int)ReadUInt32(data));

		private static uint ReadUInt32(ReadOnlySpan<byte> data)
		{
			return (uint)(data[0] | data[1] << 8 | data[2] << 16 | data[3] << 24);
		}

		private static float ReadSingle(ReadOnlySpan<byte> data)
		{
			return BitConverter.ToSingle(data.Slice(0, 4).ToArray(), 0);
		}
	}

	public static class FuturePinballProceduralMeshBuilder
	{
		private const uint NameTag = 0xA4F4D1D7;
		private const uint CollidableTag = 0x9DF5C3E2;
		private const uint RenderObjectTag = 0x97FDC4D3;
		private const uint TopTextureTag = 0xA2F4C9D1;
		private const uint TextureTag = 0xA4FAC5DC;
		private const uint TopColorTag = 0x9DF2CFD1;
		private const uint ColorTag = 0x97F5C3E2;

		public static IReadOnlyList<FuturePinballGeneratedElement> Build(FuturePinballTable table)
		{
			if (table == null) throw new ArgumentNullException(nameof(table));
			var tableWidth = FuturePinballCoordinateConverter.ToVpx(table.TableData.Integer(0xA5F8BBD1) ?? 0);
			var tableLength = FuturePinballCoordinateConverter.ToVpx(table.TableData.Integer(0x9BFCC6D1) ?? 0);
			var result = new List<FuturePinballGeneratedElement>();
			foreach (var element in table.Elements) {
				FuturePinballGeneratedElement generated = null;
				switch (element.ElementType) {
					case FuturePinballElementType.Surface:
					case FuturePinballElementType.GuideWall:
						generated = Surface(element, tableWidth, tableLength);
						break;
					case FuturePinballElementType.Ramp:
					case FuturePinballElementType.WireRamp:
						generated = Ramp(element, tableWidth, tableLength);
						break;
				}
				if (generated != null) result.Add(generated);
			}
			return result;
		}

		private static FuturePinballGeneratedElement Surface(FuturePinballSourceStream element, float tableWidth, float tableLength)
		{
			if (!FuturePinballNativeItemConverter.TryConvert(element, out var converted)
				|| !(converted.Item is Surface surface)) return null;
			var data = surface.Data;
			var generator = new SurfaceMeshGenerator(data, new Vertex3D(0f, 0f, 0f));
			var meshes = new[] {
				generator.GetMesh(SurfaceMeshGenerator.Top, tableWidth, tableLength, 0f, false),
				generator.GetMesh(SurfaceMeshGenerator.Side, tableWidth, tableLength, 0f, false)
			}.Where(mesh => mesh?.IsSet == true).ToArray();
			return Generated(element, meshes, TopTextureTag, TopColorTag);
		}

		private static FuturePinballGeneratedElement Ramp(FuturePinballSourceStream element, float tableWidth, float tableLength)
		{
			var isWire = element.ElementType == FuturePinballElementType.WireRamp;
			if (!FuturePinballNativeItemConverter.TryConvert(element, out var converted)
				|| !(converted.Item is Ramp ramp)) return null;
			var data = ramp.Data;
			var generator = new RampMeshGenerator(data, new Vertex3D(0f, 0f, 0f));
			var ids = isWire
				? new[] { RampMeshGenerator.Wires }
				: new[] { RampMeshGenerator.Floor, RampMeshGenerator.Wall };
			var meshes = ids.Select(id => generator.GetMesh(tableWidth, tableLength, 0f, id))
				.Where(mesh => mesh?.IsSet == true && mesh.Vertices.Length > 0).ToArray();
			return Generated(element, meshes, TextureTag, ColorTag);
		}

		private static FuturePinballGeneratedElement Generated(
			FuturePinballSourceStream element,
			IReadOnlyList<Mesh> meshes,
			uint textureTag,
			uint colorTag)
		{
			var material = FuturePinballMaterialConverter.FromElement(element, textureTag, colorTag);
			return new FuturePinballGeneratedElement {
				SourceIndex = element.SourceIndex ?? -1,
				Name = Name(element),
				Type = element.ElementType.Value,
				IsCollidable = FuturePinballElementGeometry.Integer(element, CollidableTag, 1) != 0,
				Texture = FuturePinballElementGeometry.Text(element, textureTag),
				Color = FuturePinballElementGeometry.Color(element, colorTag),
				Material = material,
				Meshes = FuturePinballElementGeometry.Integer(element, RenderObjectTag, 1) == 0 ? Array.Empty<Mesh>() : meshes
			};
		}

		private static string Name(FuturePinballSourceStream element)
		{
			return FuturePinballElementGeometry.Text(element, NameTag, element.Name);
		}
	}
}
