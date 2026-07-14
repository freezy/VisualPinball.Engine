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

namespace VisualPinball.Engine.IO.FuturePinball
{
	public enum FuturePinballColliderKind
	{
		None,
		VerticalCylinder,
		Sphere,
		TaperedCapsule,
		Box,
		HorizontalCylinder,
		Mesh
	}

	public enum FuturePinballColliderStatus
	{
		Generated,
		Skipped,
		Unsupported,
		Invalid
	}

	public sealed class FuturePinballColliderDescription
	{
		public uint SourceType { get; internal set; }
		public FuturePinballColliderKind Kind { get; internal set; }
		public FuturePinballColliderStatus Status { get; internal set; }
		public FuturePinballWorldPoint Center { get; internal set; }
		public float Radius { get; internal set; }
		public float SecondaryRadius { get; internal set; }
		public float HalfLength { get; internal set; }
		public float HalfHeight { get; internal set; }
		public FuturePinballWorldPoint Size { get; internal set; }
		public bool GenerateHitEvent { get; internal set; }
		public uint EventId { get; internal set; }
		public string Reason { get; internal set; }
		public Mesh Mesh { get; internal set; }
	}

	public sealed class FuturePinballColliderOptions
	{
		public bool EnableAnalyticShapes { get; set; } = true;
		public bool EnablePerPolygonCollision { get; set; } = true;
		public bool GenerateRenderMeshFallback { get; set; }
	}

	public static class FuturePinballColliderBuilder
	{
		private const float Scale = FuturePinballCoordinateConverter.WorldUnitsPerMillimeter;

		public static IReadOnlyList<FuturePinballColliderDescription> FromModel(
			FuturePinballSourceStream pinModel,
			MilkShapeModel primaryModel = null,
			FuturePinballColliderOptions options = null)
		{
			if (pinModel == null) throw new ArgumentNullException(nameof(pinModel));
			options ??= new FuturePinballColliderOptions();
			var result = new List<FuturePinballColliderDescription>();
			var shapesEnabled = RecordInteger(pinModel, "collision_shapes_enabled", 1) != 0;
			var shapes = pinModel.Records.FirstOrDefault(record => record.Name == "collision_shapes")?.Value
				as IReadOnlyList<FuturePinballCollisionShape>;
			if (shapes != null) {
				if (options.EnableAnalyticShapes && shapesEnabled) result.AddRange(FromShapes(shapes));
				else result.AddRange(shapes.Select(shape => Skipped(shape, "Analytic collision shapes are disabled")));
			}

			var perPolygon = RecordInteger(pinModel, "per_polygon_collision") != 0;
			if (perPolygon && primaryModel != null) {
				if (options.EnablePerPolygonCollision) result.Add(FromMesh(primaryModel.CreateMeshes().Select(mesh => mesh.Mesh)));
				else result.Add(new FuturePinballColliderDescription {
					Kind = FuturePinballColliderKind.Mesh,
					Status = FuturePinballColliderStatus.Skipped,
					Reason = "Per-polygon collision is disabled"
				});
			} else if (!perPolygon && options.GenerateRenderMeshFallback && primaryModel != null) {
				result.Add(FromMesh(primaryModel.CreateMeshes().Select(mesh => mesh.Mesh)));
			}
			return result;
		}

		public static IReadOnlyList<FuturePinballColliderDescription> FromShapes(
			IEnumerable<FuturePinballCollisionShape> shapes)
		{
			if (shapes == null) throw new ArgumentNullException(nameof(shapes));
			return shapes.Select(FromShape).ToArray();
		}

		public static FuturePinballColliderDescription FromShape(FuturePinballCollisionShape shape)
		{
			if (shape == null) throw new ArgumentNullException(nameof(shape));
			if (!shape.AffectsBall) return Skipped(shape, "The source shape does not affect the ball");
			if (!Finite(shape.X, shape.Y, shape.Z, shape.Value1, shape.Value2, shape.Value3, shape.Value4)) {
				return Invalid(shape, "Collision shape contains a non-finite value");
			}

			var collider = Base(shape);
			switch (shape.Type) {
				case 1:
					if (!Positive(shape.Value1, shape.Value2)) return Invalid(shape, "Cylinder radius and half-length must be positive");
					collider.Kind = FuturePinballColliderKind.VerticalCylinder;
					collider.Radius = shape.Value1 * Scale;
					collider.HalfLength = shape.Value2 * Scale;
					break;
				case 2:
					if (!Positive(shape.Value1)) return Invalid(shape, "Sphere radius must be positive");
					collider.Kind = FuturePinballColliderKind.Sphere;
					collider.Radius = shape.Value1 * Scale;
					break;
				case 3:
					if (!Positive(shape.Value1, shape.Value2, shape.Value3, shape.Value4)) return Invalid(shape, "Flipper dimensions must be positive");
					collider.Kind = FuturePinballColliderKind.TaperedCapsule;
					collider.Radius = shape.Value1 * Scale;
					collider.SecondaryRadius = shape.Value2 * Scale;
					collider.HalfLength = shape.Value3 * Scale * 0.5f;
					collider.HalfHeight = shape.Value4 * Scale;
					collider.Center = FuturePinballCoordinateConverter.ModelToWorld(
						shape.X, shape.Y, shape.Z - shape.Value3 * 0.5f
					);
					break;
				case 5:
					if (!Positive(shape.Value1, shape.Value2, shape.Value3)) return Invalid(shape, "Box half-extents must be positive");
					collider.Kind = FuturePinballColliderKind.Box;
					collider.Size = new FuturePinballWorldPoint(shape.Value1 * Scale * 2f, shape.Value2 * Scale * 2f, shape.Value3 * Scale * 2f);
					break;
				case 7:
					if (!Positive(shape.Value1, shape.Value2)) return Invalid(shape, "Cylinder radius and half-length must be positive");
					collider.Kind = FuturePinballColliderKind.HorizontalCylinder;
					collider.Radius = shape.Value1 * Scale;
					collider.SecondaryRadius = shape.Value3 * Scale;
					collider.HalfLength = shape.Value2 * Scale;
					break;
				default:
					collider.Kind = FuturePinballColliderKind.None;
					collider.Status = FuturePinballColliderStatus.Unsupported;
					collider.Reason = $"Unsupported Future Pinball collision shape type {shape.Type}";
					break;
			}
			return collider;
		}

		public static FuturePinballColliderDescription FromMesh(IEnumerable<Mesh> meshes)
		{
			if (meshes == null) throw new ArgumentNullException(nameof(meshes));
			var merged = new Mesh();
			foreach (var source in meshes.Where(mesh => mesh?.IsSet == true)) {
				var world = FuturePinballCoordinateConverter.ModelMeshToWorld(source);
				var validIndices = new List<int>();
				for (var i = 0; i + 2 < world.Indices.Length; i += 3) {
					if (world.Indices[i] < 0 || world.Indices[i] >= world.Vertices.Length
						|| world.Indices[i + 1] < 0 || world.Indices[i + 1] >= world.Vertices.Length
						|| world.Indices[i + 2] < 0 || world.Indices[i + 2] >= world.Vertices.Length) continue;
					var a = world.Vertices[world.Indices[i]];
					var b = world.Vertices[world.Indices[i + 1]];
					var c = world.Vertices[world.Indices[i + 2]];
					if (TriangleAreaSquared(a, b, c) > 1e-16f) {
						validIndices.Add(world.Indices[i]);
						validIndices.Add(world.Indices[i + 1]);
						validIndices.Add(world.Indices[i + 2]);
					}
				}
				world.Indices = validIndices.ToArray();
				if (world.Indices.Length > 0) merged.Merge(world);
			}
			return new FuturePinballColliderDescription {
				Kind = FuturePinballColliderKind.Mesh,
				Status = merged.IsSet && merged.Indices.Length > 0 ? FuturePinballColliderStatus.Generated : FuturePinballColliderStatus.Invalid,
				Reason = merged.IsSet && merged.Indices.Length > 0 ? null : "No non-degenerate model triangles are available",
				Mesh = merged.IsSet ? merged : null
			};
		}

		private static FuturePinballColliderDescription Base(FuturePinballCollisionShape shape)
		{
			return new FuturePinballColliderDescription {
				SourceType = shape.Type,
				Status = FuturePinballColliderStatus.Generated,
				Center = FuturePinballCoordinateConverter.ModelToWorld(shape.X, shape.Y, shape.Z),
				GenerateHitEvent = shape.GenerateHitEvent,
				EventId = shape.EventId
			};
		}

		private static FuturePinballColliderDescription Skipped(FuturePinballCollisionShape shape, string reason)
		{
			var result = Base(shape);
			result.Status = FuturePinballColliderStatus.Skipped;
			result.Reason = reason;
			return result;
		}

		private static FuturePinballColliderDescription Invalid(FuturePinballCollisionShape shape, string reason)
		{
			var result = Base(shape);
			result.Status = FuturePinballColliderStatus.Invalid;
			result.Reason = reason;
			return result;
		}

		private static int RecordInteger(FuturePinballSourceStream stream, string name, int fallback = 0)
		{
			var value = stream.Records.FirstOrDefault(record => record.Name == name)?.Value;
			return value is int integer ? integer : fallback;
		}

		private static bool Positive(params float[] values) => values.All(value => value > 0f);

		private static bool Finite(params float[] values) => values.All(value => !float.IsNaN(value) && !float.IsInfinity(value));

		private static float TriangleAreaSquared(Vertex3DNoTex2 a, Vertex3DNoTex2 b, Vertex3DNoTex2 c)
		{
			var abX = b.X - a.X;
			var abY = b.Y - a.Y;
			var abZ = b.Z - a.Z;
			var acX = c.X - a.X;
			var acY = c.Y - a.Y;
			var acZ = c.Z - a.Z;
			var x = abY * acZ - abZ * acY;
			var y = abZ * acX - abX * acZ;
			var z = abX * acY - abY * acX;
			return x * x + y * y + z * z;
		}
	}

	public static class FuturePinballColliderMeshBuilder
	{
		public static Mesh Build(FuturePinballColliderDescription collider, int radialSegments = 24, int sphereStacks = 12)
		{
			if (collider == null) throw new ArgumentNullException(nameof(collider));
			if (collider.Status != FuturePinballColliderStatus.Generated) return null;
			radialSegments = System.Math.Max(8, (radialSegments + 3) / 4 * 4);
			sphereStacks = System.Math.Max(4, sphereStacks + sphereStacks % 2);
			switch (collider.Kind) {
				case FuturePinballColliderKind.Mesh:
					return collider.Mesh?.Clone("Future Pinball collider mesh");
				case FuturePinballColliderKind.Box:
					return Box(collider.Size);
				case FuturePinballColliderKind.VerticalCylinder:
					return Cylinder(collider.Radius, collider.Radius, collider.HalfLength, radialSegments, true);
				case FuturePinballColliderKind.HorizontalCylinder:
					return Cylinder(collider.Radius, collider.SecondaryRadius > 0f ? collider.SecondaryRadius : collider.Radius,
						collider.HalfLength, radialSegments, false);
				case FuturePinballColliderKind.Sphere:
					return Sphere(collider.Radius, radialSegments, sphereStacks);
				case FuturePinballColliderKind.TaperedCapsule:
					return TaperedCapsule(collider.Radius, collider.SecondaryRadius, collider.HalfLength,
						collider.HalfHeight, radialSegments);
				default:
					return null;
			}
		}

		public static bool IsTessellatedApproximation(FuturePinballColliderKind kind)
		{
			return kind == FuturePinballColliderKind.VerticalCylinder
				|| kind == FuturePinballColliderKind.HorizontalCylinder
				|| kind == FuturePinballColliderKind.Sphere
				|| kind == FuturePinballColliderKind.TaperedCapsule;
		}

		private static Mesh Box(FuturePinballWorldPoint size)
		{
			var x = size.X * 0.5f;
			var y = size.Y * 0.5f;
			var z = size.Z * 0.5f;
			return NamedMesh("Future Pinball box collider", new[] {
				Vertex(-x, -y, -z), Vertex(x, -y, -z), Vertex(x, y, -z), Vertex(-x, y, -z),
				Vertex(-x, -y, z), Vertex(x, -y, z), Vertex(x, y, z), Vertex(-x, y, z)
			}, new[] {
				0, 2, 1, 0, 3, 2, 4, 5, 6, 4, 6, 7,
				0, 1, 5, 0, 5, 4, 3, 7, 6, 3, 6, 2,
				0, 4, 7, 0, 7, 3, 1, 2, 6, 1, 6, 5
			});
		}

		private static Mesh Cylinder(float radiusX, float radiusY, float halfLength, int segments, bool vertical)
		{
			var vertices = new List<Vertex3DNoTex2>(segments * 2 + 2);
			var indices = new List<int>(segments * 12);
			for (var i = 0; i < segments; i++) {
				var angle = (float)(System.Math.PI * 2.0 * i / segments);
				var x = (float)System.Math.Cos(angle) * radiusX;
				var radial = (float)System.Math.Sin(angle) * radiusY;
				vertices.Add(vertical ? Vertex(x, -halfLength, radial) : Vertex(x, radial, -halfLength));
				vertices.Add(vertical ? Vertex(x, halfLength, radial) : Vertex(x, radial, halfLength));
			}
			var lowCenter = vertices.Count;
			vertices.Add(vertical ? Vertex(0f, -halfLength, 0f) : Vertex(0f, 0f, -halfLength));
			var highCenter = vertices.Count;
			vertices.Add(vertical ? Vertex(0f, halfLength, 0f) : Vertex(0f, 0f, halfLength));
			for (var i = 0; i < segments; i++) {
				var next = (i + 1) % segments;
				var low = i * 2;
				var high = low + 1;
				var nextLow = next * 2;
				var nextHigh = nextLow + 1;
				if (vertical) {
					indices.Add(low); indices.Add(high); indices.Add(nextHigh);
					indices.Add(low); indices.Add(nextHigh); indices.Add(nextLow);
					indices.Add(lowCenter); indices.Add(low); indices.Add(nextLow);
					indices.Add(highCenter); indices.Add(nextHigh); indices.Add(high);
				} else {
					indices.Add(low); indices.Add(nextHigh); indices.Add(high);
					indices.Add(low); indices.Add(nextLow); indices.Add(nextHigh);
					indices.Add(lowCenter); indices.Add(nextLow); indices.Add(low);
					indices.Add(highCenter); indices.Add(high); indices.Add(nextHigh);
				}
			}
			return NamedMesh(vertical ? "Future Pinball vertical cylinder collider" : "Future Pinball horizontal cylinder collider",
				vertices.ToArray(), indices.ToArray());
		}

		private static Mesh Sphere(float radius, int segments, int stacks)
		{
			var vertices = new List<Vertex3DNoTex2>((stacks - 1) * segments + 2) { Vertex(0f, -radius, 0f) };
			for (var stack = 1; stack < stacks; stack++) {
				var latitude = -System.Math.PI * 0.5 + System.Math.PI * stack / stacks;
				var y = (float)System.Math.Sin(latitude) * radius;
				var ringRadius = (float)System.Math.Cos(latitude) * radius;
				for (var segment = 0; segment < segments; segment++) {
					var longitude = System.Math.PI * 2.0 * segment / segments;
					vertices.Add(Vertex((float)System.Math.Cos(longitude) * ringRadius, y,
						(float)System.Math.Sin(longitude) * ringRadius));
				}
			}
			var top = vertices.Count;
			vertices.Add(Vertex(0f, radius, 0f));
			var indices = new List<int>(segments * stacks * 6);
			for (var segment = 0; segment < segments; segment++) {
				var next = (segment + 1) % segments;
				indices.Add(0); indices.Add(1 + segment); indices.Add(1 + next);
				for (var stack = 0; stack < stacks - 2; stack++) {
					var ring = 1 + stack * segments;
					var nextRing = ring + segments;
					indices.Add(ring + segment); indices.Add(nextRing + segment); indices.Add(nextRing + next);
					indices.Add(ring + segment); indices.Add(nextRing + next); indices.Add(ring + next);
				}
				var lastRing = 1 + (stacks - 2) * segments;
				indices.Add(top); indices.Add(lastRing + next); indices.Add(lastRing + segment);
			}
			return NamedMesh("Future Pinball sphere collider", vertices.ToArray(), indices.ToArray());
		}

		private static Mesh TaperedCapsule(float startRadius, float endRadius, float halfLength, float halfHeight, int segments)
		{
			var samples = new List<Point2>(segments * 2);
			for (var i = 0; i < segments; i++) {
				var angle = System.Math.PI * 2.0 * i / segments;
				var x = (float)System.Math.Cos(angle);
				var z = (float)System.Math.Sin(angle);
				samples.Add(new Point2(x * startRadius, z * startRadius - halfLength));
				samples.Add(new Point2(x * endRadius, z * endRadius + halfLength));
			}
			var outline = ConvexHull(samples);
			var vertices = new List<Vertex3DNoTex2>(outline.Count * 2);
			foreach (var point in outline) vertices.Add(Vertex(point.X, -halfHeight, point.Z));
			foreach (var point in outline) vertices.Add(Vertex(point.X, halfHeight, point.Z));
			var indices = new List<int>(outline.Count * 12);
			for (var i = 1; i + 1 < outline.Count; i++) {
				indices.Add(0); indices.Add(i); indices.Add(i + 1);
				indices.Add(outline.Count); indices.Add(outline.Count + i + 1); indices.Add(outline.Count + i);
			}
			for (var i = 0; i < outline.Count; i++) {
				var next = (i + 1) % outline.Count;
				indices.Add(i); indices.Add(outline.Count + i); indices.Add(outline.Count + next);
				indices.Add(i); indices.Add(outline.Count + next); indices.Add(next);
			}
			return NamedMesh("Future Pinball tapered capsule collider", vertices.ToArray(), indices.ToArray());
		}

		private static List<Point2> ConvexHull(IEnumerable<Point2> points)
		{
			var sorted = points.OrderBy(point => point.X).ThenBy(point => point.Z).ToArray();
			var hull = new List<Point2>(sorted.Length * 2);
			foreach (var point in sorted) {
				while (hull.Count >= 2 && Cross(hull[hull.Count - 2], hull[hull.Count - 1], point) <= 0f) hull.RemoveAt(hull.Count - 1);
				hull.Add(point);
			}
			var lowerCount = hull.Count;
			for (var i = sorted.Length - 2; i >= 0; i--) {
				var point = sorted[i];
				while (hull.Count > lowerCount && Cross(hull[hull.Count - 2], hull[hull.Count - 1], point) <= 0f) hull.RemoveAt(hull.Count - 1);
				hull.Add(point);
			}
			if (hull.Count > 1) hull.RemoveAt(hull.Count - 1);
			return hull;
		}

		private static float Cross(Point2 origin, Point2 a, Point2 b)
		{
			return (a.X - origin.X) * (b.Z - origin.Z) - (a.Z - origin.Z) * (b.X - origin.X);
		}

		private static Vertex3DNoTex2 Vertex(float x, float y, float z) => new Vertex3DNoTex2(x, y, z);

		private static Mesh NamedMesh(string name, Vertex3DNoTex2[] vertices, int[] indices)
		{
			return new Mesh(vertices, indices) { Name = name };
		}

		private readonly struct Point2
		{
			public float X { get; }
			public float Z { get; }

			public Point2(float x, float z)
			{
				X = x;
				Z = z;
			}
		}
	}
}
