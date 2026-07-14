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
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT;

namespace VisualPinball.Engine.IO.FuturePinball
{
	public sealed class MilkShapeVertex
	{
		public byte Flags { get; internal set; }
		public float X { get; internal set; }
		public float Y { get; internal set; }
		public float Z { get; internal set; }
		public sbyte BoneId { get; internal set; }
		public byte ReferenceCount { get; internal set; }
	}

	public sealed class MilkShapeTriangle
	{
		public ushort Flags { get; internal set; }
		public ushort[] VertexIndices { get; internal set; }
		public float[,] Normals { get; internal set; }
		public float[] U { get; internal set; }
		public float[] V { get; internal set; }
		public byte SmoothingGroup { get; internal set; }
		public byte GroupIndex { get; internal set; }
	}

	public sealed class MilkShapeGroup
	{
		public byte Flags { get; internal set; }
		public string Name { get; internal set; }
		public ushort[] TriangleIndices { get; internal set; }
		public sbyte MaterialIndex { get; internal set; }
	}

	public sealed class MilkShapeMaterial
	{
		public string Name { get; internal set; }
		public float[] Ambient { get; internal set; }
		public float[] Diffuse { get; internal set; }
		public float[] Specular { get; internal set; }
		public float[] Emissive { get; internal set; }
		public float Shininess { get; internal set; }
		public float Transparency { get; internal set; }
		public byte Mode { get; internal set; }
		public string Texture { get; internal set; }
		public string AlphaMap { get; internal set; }
	}

	public sealed class MilkShapeGroupMesh
	{
		public string Name { get; internal set; }
		public int MaterialIndex { get; internal set; }
		public Mesh Mesh { get; internal set; }
	}

	public sealed class MilkShapeModel
	{
		public int Version { get; internal set; }
		public string SourceSha256 { get; internal set; }
		public IReadOnlyList<MilkShapeVertex> Vertices { get; internal set; }
		public IReadOnlyList<MilkShapeTriangle> Triangles { get; internal set; }
		public IReadOnlyList<MilkShapeGroup> Groups { get; internal set; }
		public IReadOnlyList<MilkShapeMaterial> Materials { get; internal set; }
		public byte[] TrailingData { get; internal set; }

		public IReadOnlyList<MilkShapeGroupMesh> CreateMeshes()
		{
			var groups = Groups.Count == 0
				? new[] { new MilkShapeGroup { Name = "mesh", MaterialIndex = -1, TriangleIndices = Enumerable.Range(0, Triangles.Count).Select(index => (ushort)index).ToArray() } }
				: Groups;
			var meshes = new List<MilkShapeGroupMesh>(groups.Count);
			foreach (var group in groups) {
				var vertices = new List<Vertex3DNoTex2>(group.TriangleIndices.Length * 3);
				var indices = new List<int>(group.TriangleIndices.Length * 3);
				foreach (var triangleIndex in group.TriangleIndices) {
					var triangle = Triangles[triangleIndex];
					for (var corner = 0; corner < 3; corner++) {
						var position = Vertices[triangle.VertexIndices[corner]];
						indices.Add(vertices.Count);
						vertices.Add(new Vertex3DNoTex2(
							position.X, position.Y, position.Z,
							triangle.Normals[corner, 0], triangle.Normals[corner, 1], triangle.Normals[corner, 2],
							triangle.U[corner], triangle.V[corner]
						));
					}
				}
				var name = string.IsNullOrWhiteSpace(group.Name) ? "mesh" : group.Name;
				meshes.Add(new MilkShapeGroupMesh {
					Name = name,
					MaterialIndex = group.MaterialIndex,
					Mesh = new Mesh(vertices.ToArray(), indices.ToArray()) { Name = name }
				});
			}
			return meshes;
		}
	}

	public sealed class FuturePinballModelVariant
	{
		public string Role { get; internal set; }
		public MilkShapeModel Model { get; internal set; }
	}

	public sealed class FuturePinballModelAssets
	{
		public IReadOnlyList<FuturePinballModelVariant> Variants { get; internal set; } = Array.Empty<FuturePinballModelVariant>();
		public ReadOnlyMemory<byte> Preview { get; internal set; }
	}

	public sealed class MilkShapeModelCache
	{
		private readonly Dictionary<string, MilkShapeModel> _models = new Dictionary<string, MilkShapeModel>();

		public MilkShapeModel Parse(byte[] data, string sourceName = null)
		{
			var hash = Sha256(data);
			if (_models.TryGetValue(hash, out var model)) return model;
			model = MilkShapeModelReader.Parse(data, sourceName);
			_models[hash] = model;
			return model;
		}

		private static string Sha256(byte[] data)
		{
			using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(data)).Replace("-", string.Empty).ToLowerInvariant();
		}
	}

	public static class FuturePinballModelAssetReader
	{
		public static FuturePinballModelAssets ReadEmbedded(
			FuturePinballSourceStream pinModel,
			MilkShapeModelCache cache = null)
		{
			if (pinModel == null) throw new ArgumentNullException(nameof(pinModel));
			cache ??= new MilkShapeModelCache();
			var variants = new List<FuturePinballModelVariant>();
			ReadVariant(pinModel, "primary_model_data", variants, cache);
			ReadVariant(pinModel, "secondary_model_data", variants, cache);
			ReadVariant(pinModel, "mask_model_data", variants, cache);
			ReadVariant(pinModel, "reflection_model_data", variants, cache);
			var preview = pinModel.Records.FirstOrDefault(record => record.Name == "preview_data")?.Value as FuturePinballCompressedData;
			return new FuturePinballModelAssets { Variants = variants, Preview = preview?.DecodedBytes ?? Array.Empty<byte>() };
		}

		private static void ReadVariant(
			FuturePinballSourceStream pinModel,
			string role,
			ICollection<FuturePinballModelVariant> variants,
			MilkShapeModelCache cache)
		{
			var data = pinModel.Records.FirstOrDefault(record => record.Name == role)?.Value as FuturePinballCompressedData;
			if (data?.DecodedBytes == null || data.DecodedBytes.Length == 0) return;
			variants.Add(new FuturePinballModelVariant { Role = role, Model = cache.Parse(data.DecodedBytes, pinModel.Name + "/" + role) });
		}
	}

	public static class MilkShapeModelReader
	{
		private const string Signature = "MS3D000000";

		public static MilkShapeModel Parse(byte[] data, string sourceName = null)
		{
			if (data == null) throw new ArgumentNullException(nameof(data));
			try {
				using (var stream = new MemoryStream(data, false))
				using (var reader = new BinaryReader(stream, Encoding.ASCII)) {
					if (ReadFixedString(reader, 10) != Signature) throw Error("Invalid MilkShape signature", sourceName, stream.Position - 10);
					var version = reader.ReadInt32();
					if (version < 3 || version > 4) throw Error($"Unsupported MilkShape version {version}", sourceName, stream.Position - 4);

					var vertices = ReadVertices(reader, sourceName);
					var triangles = ReadTriangles(reader, vertices.Count, sourceName);
					var groups = ReadGroups(reader, triangles.Count, sourceName);
					var materials = ReadMaterials(reader, sourceName);
					foreach (var group in groups) {
						if (group.MaterialIndex < -1 || group.MaterialIndex >= materials.Count) {
							throw Error($"Group '{group.Name}' references material {group.MaterialIndex} of {materials.Count}", sourceName, stream.Position);
						}
					}
					var trailing = reader.ReadBytes((int)(stream.Length - stream.Position));
					return new MilkShapeModel {
						Version = version,
						SourceSha256 = Sha256(data),
						Vertices = vertices,
						Triangles = triangles,
						Groups = groups,
						Materials = materials,
						TrailingData = trailing
					};
				}
			} catch (EndOfStreamException exception) {
				throw Error("Truncated MilkShape model", sourceName, -1, exception);
			}
		}

		private static List<MilkShapeVertex> ReadVertices(BinaryReader reader, string sourceName)
		{
			var count = reader.ReadUInt16();
			RequireRemaining(reader, count * 15L, sourceName, "vertices");
			var result = new List<MilkShapeVertex>(count);
			for (var i = 0; i < count; i++) {
				var vertex = new MilkShapeVertex {
					Flags = reader.ReadByte(),
					X = ReadFinite(reader, sourceName, "vertex x"),
					Y = ReadFinite(reader, sourceName, "vertex y"),
					Z = ReadFinite(reader, sourceName, "vertex z"),
					BoneId = reader.ReadSByte(),
					ReferenceCount = reader.ReadByte()
				};
				result.Add(vertex);
			}
			return result;
		}

		private static List<MilkShapeTriangle> ReadTriangles(BinaryReader reader, int vertexCount, string sourceName)
		{
			var count = reader.ReadUInt16();
			RequireRemaining(reader, count * 70L, sourceName, "triangles");
			var result = new List<MilkShapeTriangle>(count);
			for (var i = 0; i < count; i++) {
				var triangle = new MilkShapeTriangle {
					Flags = reader.ReadUInt16(),
					VertexIndices = new[] { reader.ReadUInt16(), reader.ReadUInt16(), reader.ReadUInt16() },
					Normals = new float[3, 3],
					U = new float[3],
					V = new float[3]
				};
				if (triangle.VertexIndices.Any(index => index >= vertexCount)) {
					throw Error($"Triangle {i} references a vertex outside 0..{vertexCount - 1}", sourceName, reader.BaseStream.Position - 6);
				}
				for (var corner = 0; corner < 3; corner++)
				for (var component = 0; component < 3; component++)
					triangle.Normals[corner, component] = ReadFinite(reader, sourceName, "normal");
				for (var corner = 0; corner < 3; corner++) triangle.U[corner] = ReadFinite(reader, sourceName, "texture u");
				for (var corner = 0; corner < 3; corner++) triangle.V[corner] = ReadFinite(reader, sourceName, "texture v");
				triangle.SmoothingGroup = reader.ReadByte();
				triangle.GroupIndex = reader.ReadByte();
				result.Add(triangle);
			}
			return result;
		}

		private static List<MilkShapeGroup> ReadGroups(BinaryReader reader, int triangleCount, string sourceName)
		{
			var count = reader.ReadUInt16();
			var result = new List<MilkShapeGroup>(count);
			for (var i = 0; i < count; i++) {
				RequireRemaining(reader, 36, sourceName, "group header");
				var flags = reader.ReadByte();
				var name = ReadFixedString(reader, 32);
				var triangleIndexCount = reader.ReadUInt16();
				RequireRemaining(reader, triangleIndexCount * 2L + 1, sourceName, "group triangles");
				var triangleIndices = new ushort[triangleIndexCount];
				for (var j = 0; j < triangleIndices.Length; j++) {
					triangleIndices[j] = reader.ReadUInt16();
					if (triangleIndices[j] >= triangleCount) {
						throw Error($"Group '{name}' references triangle {triangleIndices[j]} of {triangleCount}", sourceName, reader.BaseStream.Position - 2);
					}
				}
				result.Add(new MilkShapeGroup {
					Flags = flags,
					Name = name,
					TriangleIndices = triangleIndices,
					MaterialIndex = reader.ReadSByte()
				});
			}
			return result;
		}

		private static List<MilkShapeMaterial> ReadMaterials(BinaryReader reader, string sourceName)
		{
			var count = reader.ReadUInt16();
			RequireRemaining(reader, count * 361L, sourceName, "materials");
			var result = new List<MilkShapeMaterial>(count);
			for (var i = 0; i < count; i++) {
				result.Add(new MilkShapeMaterial {
					Name = ReadFixedString(reader, 32),
					Ambient = ReadVector4(reader, sourceName),
					Diffuse = ReadVector4(reader, sourceName),
					Specular = ReadVector4(reader, sourceName),
					Emissive = ReadVector4(reader, sourceName),
					Shininess = ReadFinite(reader, sourceName, "shininess"),
					Transparency = ReadFinite(reader, sourceName, "transparency"),
					Mode = reader.ReadByte(),
					Texture = ReadFixedString(reader, 128),
					AlphaMap = ReadFixedString(reader, 128)
				});
			}
			return result;
		}

		private static float[] ReadVector4(BinaryReader reader, string sourceName)
		{
			return new[] {
				ReadFinite(reader, sourceName, "material component"), ReadFinite(reader, sourceName, "material component"),
				ReadFinite(reader, sourceName, "material component"), ReadFinite(reader, sourceName, "material component")
			};
		}

		private static string ReadFixedString(BinaryReader reader, int length)
		{
			var data = reader.ReadBytes(length);
			if (data.Length != length) throw new EndOfStreamException();
			var terminator = Array.IndexOf(data, (byte)0);
			return Encoding.ASCII.GetString(data, 0, terminator < 0 ? data.Length : terminator);
		}

		private static float ReadFinite(BinaryReader reader, string sourceName, string valueName)
		{
			var value = reader.ReadSingle();
			if (float.IsNaN(value) || float.IsInfinity(value)) {
				throw Error($"MilkShape {valueName} is not finite", sourceName, reader.BaseStream.Position - 4);
			}
			return value;
		}

		private static void RequireRemaining(BinaryReader reader, long bytes, string sourceName, string section)
		{
			if (bytes < 0 || reader.BaseStream.Position > reader.BaseStream.Length - bytes) {
				throw Error($"Truncated MilkShape {section}", sourceName, reader.BaseStream.Position);
			}
		}

		private static FuturePinballFormatException Error(string message, string sourceName, long offset, Exception inner = null)
		{
			return new FuturePinballFormatException(message, sourceName, offset, inner);
		}

		private static string Sha256(byte[] data)
		{
			using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(data)).Replace("-", string.Empty).ToLowerInvariant();
		}
	}
}
