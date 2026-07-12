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
}
