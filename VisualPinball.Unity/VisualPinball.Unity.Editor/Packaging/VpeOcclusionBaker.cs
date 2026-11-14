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
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using UnityEngine;
using UnityEngine.Rendering;
using Logger = NLog.Logger;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Editor
{
	/// <summary>How the export bakes the playfield's ambient occlusion, see <see cref="VpeOcclusionBaker"/>.</summary>
	public sealed class VpeOcclusionBakeSettings
	{
		/// <summary>Texels along the longer side of the occlusion map.</summary>
		public int Resolution = 2048;
		/// <summary>Directions over the hemisphere, cosine-weighted.</summary>
		public int Directions = 256;
		/// <summary>Texels along each side of the per-direction occluder map.</summary>
		public int OccluderResolution = 2048;
		/// <summary>In meters: an occluder this far away along a direction no longer shades.</summary>
		public float FalloffDistance = 0.08f;
		/// <summary>How much of the light a transparent occluder (a clear plastic, a ramp) blocks.</summary>
		public float TransparentOcclusion = 0.5f;
		/// <summary>0 bakes no occlusion, 1 the full occlusion.</summary>
		public float Strength = 1f;
	}

	/// <summary>
	/// Bakes the ambient occlusion of the playfield: how much of the sky above each texel the table's static
	/// geometry hides within <see cref="VpeOcclusionBakeSettings.FalloffDistance"/>. For each of a few hundred
	/// directions, it renders the occluders above the playfield into a map that keeps the distance of the
	/// nearest one along that direction, then rasterizes the playfield in its UV space and adds up what each
	/// texel sees. Parts that move during play (balls, flippers, targets, gates, spinners, …) are left out.
	/// </summary>
	public static class VpeOcclusionBaker
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public const string ShaderName = "Hidden/VPE/OcclusionBake";

		// Points this close above the playfield plane belong to it (decals, the tops of sunk inserts).
		private const float PlaneTolerance = 0.0002f;
		// A receiver is a mesh no thicker than this whose top is this close to the highest receiver.
		private const float MaxReceiverThickness = 0.05f;
		private const float ReceiverTopTolerance = 0.005f;
		private const float MinReceiverShare = 0.5f;
		// Receivers share a UV space when their UVs map to the same playfield positions this closely.
		private const float UvSpaceTolerance = 0.005f;
		// The playfield's sides and bottom don't receive.
		private const float MinReceiverNormal = 0.3f;
		// Directions submitted per command buffer, between which the editor gets a frame.
		private const int DirectionsPerBatch = 8;
		private const float NoOccluder = 1e9f;

		private static readonly Type[] MovingTypes = {
			typeof(BallComponent), typeof(FlipperComponent), typeof(PlungerComponent), typeof(TurntableComponent),
		};

		public sealed class Result
		{
			public byte[] Png;
			public int Width;
			public int Height;
			/// <summary>The meshes the occlusion was baked for.</summary>
			public Renderer[] Receivers;
			/// <summary>The materials of the receivers' submeshes the occlusion was baked for.</summary>
			public Material[] Materials;
		}

		private readonly struct Receiver
		{
			public readonly Renderer Renderer;
			public readonly Mesh Mesh;
			public readonly Material Material;
			// The triangles that receive (vertex index triples): those facing up whose UVs lie on the mapping.
			public readonly int[] Triangles;

			public Receiver(Renderer renderer, Mesh mesh, Material material, int[] triangles)
			{
				Renderer = renderer;
				Mesh = mesh;
				Material = material;
				Triangles = triangles;
			}
		}

		private readonly struct Draw
		{
			public readonly Mesh Mesh;
			public readonly Matrix4x4 Matrix;
			public readonly int SubMesh;
			public readonly MaterialPropertyBlock Properties;

			public Draw(Mesh mesh, Matrix4x4 matrix, int subMesh, MaterialPropertyBlock properties)
			{
				Mesh = mesh;
				Matrix = matrix;
				SubMesh = subMesh;
				Properties = properties;
			}
		}

		/// <summary>
		/// Bakes the occlusion of the table's playfield. Returns null when the table has no playfield mesh the
		/// occlusion can be baked for. Must be awaited on the main thread.
		/// </summary>
		public static async Task<Result> BakePlayfieldAsync(GameObject table, VpeOcclusionBakeSettings settings, Action<float> progress, CancellationToken ct)
		{
			var playfield = table ? table.GetComponentInChildren<PlayfieldComponent>() : null;
			if (!playfield) {
				Logger.Info("Not baking ambient occlusion: the table has no playfield.");
				return null;
			}
			if (!SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RFloat) || !SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBFloat)) {
				Logger.Warn("Not baking ambient occlusion: the graphics device can't render into float textures.");
				return null;
			}
			var shader = Shader.Find(ShaderName);
			if (!shader || !shader.isSupported) {
				Logger.Warn($"Not baking ambient occlusion: shader {ShaderName} is missing or not supported.");
				return null;
			}

			var stopwatch = Stopwatch.StartNew();
			var up = playfield.transform.up.normalized;
			var tangent = Vector3.Cross(up, Mathf.Abs(up.z) < 0.9f ? Vector3.forward : Vector3.right).normalized;
			var bitangent = Vector3.Cross(up, tangent);

			if (!FindReceivers(playfield, up, tangent, bitangent, out var receiverParts, out var rejectedMaterials, out var plane, out var uvTransform, out var uvChannel, out var aspect)) {
				return null;
			}
			var receivers = receiverParts.Select(r => r.Renderer).Distinct().ToArray();
			var receiverSet = new HashSet<Renderer>(receivers);
			var falloff = Mathf.Max(0.001f, settings.FalloffDistance);
			var region = Footprint(receivers, tangent, bitangent);
			region.xMin -= falloff; region.yMin -= falloff; region.xMax += falloff; region.yMax += falloff;

			var material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
			var opaque = new List<Draw>();
			var transparent = new List<Draw>();
			CollectOccluders(table, receiverSet, up, tangent, bitangent, plane, falloff, region, Footprint(receivers, tangent, bitangent).Area(), opaque, transparent);
			Logger.Info($"Baking ambient occlusion for {string.Join(", ", receivers.Select(r => r.name))}: " +
				$"{opaque.Count} opaque and {transparent.Count} transparent occluder draw(s), {settings.Directions} direction(s).");

			var width = aspect >= 1f ? settings.Resolution : Mathf.Max(4, Mathf.RoundToInt(settings.Resolution * aspect));
			var height = aspect >= 1f ? Mathf.Max(4, Mathf.RoundToInt(settings.Resolution / aspect)) : settings.Resolution;
			var occluderSize = Mathf.Clamp(settings.OccluderResolution, 256, 8192);

			RenderTexture opaqueMap = null, transparentMap = null, accumulation = null;
			var receiverMeshes = new List<Mesh>();
			try {
				var receiverDraws = new List<Draw>();
				foreach (var receiver in receiverParts) {
					var mesh = ReceiverMesh(receiver);
					receiverMeshes.Add(mesh);
					receiverDraws.Add(new Draw(mesh, receiver.Renderer.localToWorldMatrix, 0, null));
				}

				opaqueMap = NewTarget(occluderSize, occluderSize, RenderTextureFormat.RFloat, "Occlusion Bake Opaque");
				transparentMap = NewTarget(occluderSize, occluderSize, RenderTextureFormat.RFloat, "Occlusion Bake Transparent");
				accumulation = NewTarget(width, height, RenderTextureFormat.ARGBFloat, "Occlusion Bake Accumulation");

				material.SetVector("_VpeUp", new Vector4(up.x, up.y, up.z, plane + PlaneTolerance));
				material.SetVector("_VpeParams", new Vector4(falloff, Mathf.Clamp01(settings.TransparentOcclusion), MinReceiverNormal, 0));
				material.SetVector("_VpeUvTransform", uvTransform);
				material.SetFloat("_VpeUvChannel", uvChannel);

				using (var clear = new CommandBuffer { name = "Occlusion Bake Clear" }) {
					clear.SetRenderTarget(accumulation);
					clear.ClearRenderTarget(false, true, Color.clear);
					Graphics.ExecuteCommandBuffer(clear);
				}

				var directions = Directions(Mathf.Max(1, settings.Directions), up, tangent, bitangent);
				var noOccluder = new Color(NoOccluder, NoOccluder, NoOccluder, NoOccluder);
				for (var start = 0; start < directions.Length; start += DirectionsPerBatch) {
					ct.ThrowIfCancellationRequested();
					using (var cmd = new CommandBuffer { name = "Occlusion Bake" }) {
						for (var d = start; d < Mathf.Min(start + DirectionsPerBatch, directions.Length); d++) {
							var dir = directions[d];
							cmd.SetGlobalMatrix("_VpeLightVp", LightMatrix(dir, up, tangent, bitangent, region, plane, falloff));
							cmd.SetGlobalVector("_VpeDir", dir);

							cmd.SetRenderTarget(opaqueMap);
							cmd.ClearRenderTarget(false, true, noOccluder);
							foreach (var draw in opaque) {
								cmd.DrawMesh(draw.Mesh, draw.Matrix, material, draw.SubMesh, 0, draw.Properties);
							}
							cmd.SetRenderTarget(transparentMap);
							cmd.ClearRenderTarget(false, true, noOccluder);
							foreach (var draw in transparent) {
								cmd.DrawMesh(draw.Mesh, draw.Matrix, material, draw.SubMesh, 0, draw.Properties);
							}

							cmd.SetRenderTarget(accumulation);
							cmd.SetGlobalTexture("_VpeOpaque", opaqueMap);
							cmd.SetGlobalTexture("_VpeTransparent", transparentMap);
							foreach (var draw in receiverDraws) {
								cmd.DrawMesh(draw.Mesh, draw.Matrix, material, draw.SubMesh, 1);
							}
						}
						Graphics.ExecuteCommandBuffer(cmd);
					}
					progress?.Invoke((float)Mathf.Min(start + DirectionsPerBatch, directions.Length) / directions.Length);
					await Task.Yield();
				}

				var occlusion = ReadBack(accumulation, width, height, Mathf.Clamp01(settings.Strength));
				var png = EncodeGray(occlusion, width, height);
				Logger.Info($"Baked ambient occlusion {width}x{height} in {stopwatch.ElapsedMilliseconds}ms ({png.Length / 1024f:F0} KB).");
				return new Result { Png = png, Width = width, Height = height, Receivers = receivers, Materials = receiverParts.Select(r => r.Material).Where(m => !rejectedMaterials.Contains(m)).Distinct().ToArray() };

			} finally {
				Release(opaqueMap);
				Release(transparentMap);
				Release(accumulation);
				Object.DestroyImmediate(material);
				foreach (var mesh in receiverMeshes) {
					Object.DestroyImmediate(mesh);
				}
			}
		}

		// The receiving triangles of a receiver with what the bake shader reads of their vertices.
		private static Mesh ReceiverMesh(Receiver receiver)
		{
			var source = receiver.Mesh;
			var mesh = new Mesh {
				name = source.name + " (occlusion bake)",
				hideFlags = HideFlags.HideAndDontSave,
				indexFormat = source.vertexCount > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16,
			};
			mesh.SetVertices(source.vertices);
			mesh.SetNormals(source.normals);
			var uvs = new List<Vector2>();
			for (var channel = 0; channel < 2; channel++) {
				source.GetUVs(channel, uvs);
				if (uvs.Count == source.vertexCount) {
					mesh.SetUVs(channel, uvs);
				}
			}
			mesh.SetTriangles(receiver.Triangles, 0, false);
			mesh.bounds = source.bounds;
			return mesh;
		}

		// The playfield meshes: the biggest flat meshes under the playfield whose tops share its plane. The baked
		// submeshes all share one UV space: their materials apply the same channel and tiling, their UVs stay in
		// the unit square (tiled UVs would share occlusion texels), and the same UV is the same playfield position
		// on all of them (separately unwrapped meshes would mix each other's occlusion).
		private static bool FindReceivers(PlayfieldComponent playfield, Vector3 up, Vector3 tangent, Vector3 bitangent,
			out Receiver[] receivers, out HashSet<Material> rejected, out float plane, out Vector4 uvTransform, out int uvChannel, out float aspect)
		{
			receivers = null;
			// Materials of submeshes the bake skipped: they'd get occlusion baked for other submeshes' UVs.
			rejected = new HashSet<Material>();
			plane = 0;
			uvTransform = new Vector4(1, 1, 0, 0);
			uvChannel = 0;
			aspect = 1;

			var candidates = new List<(Renderer Renderer, Mesh Mesh, float Area, float Top, float Bottom)>();
			foreach (var renderer in playfield.GetComponentsInChildren<MeshRenderer>()) {
				if (!renderer.enabled || !renderer.gameObject.activeInHierarchy || IsTransparent(renderer)) {
					continue;
				}
				var mesh = renderer.GetComponent<MeshFilter>()?.sharedMesh;
				if (!mesh) {
					continue;
				}
				var corners = Corners(mesh.bounds, renderer.localToWorldMatrix);
				candidates.Add((renderer, mesh, Footprint(corners, tangent, bitangent).Area(), corners.Max(c => Vector3.Dot(c, up)), corners.Min(c => Vector3.Dot(c, up))));
			}
			if (candidates.Count == 0) {
				Logger.Info("Not baking ambient occlusion: the playfield has no mesh.");
				return false;
			}
			var maxArea = candidates.Max(c => c.Area);
			var flat = candidates.Where(c => c.Area >= maxArea * MinReceiverShare && c.Top - c.Bottom <= MaxReceiverThickness).ToList();
			if (flat.Count == 0) {
				Logger.Info("Not baking ambient occlusion: the playfield has no flat mesh.");
				return false;
			}
			var top = flat.Max(c => c.Top);
			flat = flat.Where(c => c.Top >= top - ReceiverTopTolerance).OrderByDescending(c => c.Area).ToList();

			// The biggest one's first submesh decides the UV space; the others must share it.
			var accepted = new List<Receiver>();
			var reference = Vector4.zero;
			var referenceChannel = -1;
			var referenceAspect = 1f;
			UvMapping referenceMapping = null;
			foreach (var candidate in flat) {
				var materials = candidate.Renderer.sharedMaterials;
				for (var subMesh = 0; subMesh < Mathf.Min(candidate.Mesh.subMeshCount, materials.Length); subMesh++) {
					var material = materials[subMesh];
					var label = candidate.Mesh.subMeshCount > 1 ? $"{candidate.Renderer.name} ({(material ? material.name : "no material")})" : candidate.Renderer.name;
					if (!TryGetUvSpace(material, out var transform, out var channel, out var textureAspect)) {
						Logger.Info($"Not baking ambient occlusion for {label}: its material maps no UV channel the bake supports.");
						rejected.Add(material);
						continue;
					}
					var mapping = UvMapping.Fit(candidate.Mesh, subMesh, channel, transform, candidate.Renderer.localToWorldMatrix, up, tangent, bitangent, UvSpaceTolerance);
					if (mapping == null) {
						Logger.Info($"Not baking ambient occlusion for {label}: it has no UVs in channel {channel}.");
						rejected.Add(material);
						continue;
					}
					if (!mapping.InUnitSquare) {
						Logger.Info($"Not baking ambient occlusion for {label}: its UVs leave the unit square.");
						rejected.Add(material);
						continue;
					}
					if (referenceMapping == null) {
						if (!mapping.IsAffine) {
							Logger.Info($"Not baking ambient occlusion for {label}: its UVs don't map to playfield positions evenly ({mapping.Describe()}).");
							rejected.Add(material);
							continue;
						}
						reference = transform;
						referenceChannel = channel;
						referenceAspect = textureAspect;
						referenceMapping = mapping;
					} else if (channel != referenceChannel || (transform - reference).sqrMagnitude > 1e-8f) {
						Logger.Info($"Not baking ambient occlusion for {label}: its material's UV mapping differs from {accepted[0].Renderer.name}'s.");
						rejected.Add(material);
						continue;
					} else if (!mapping.Matches(referenceMapping, UvSpaceTolerance)) {
						Logger.Info($"Not baking ambient occlusion for {label}: its UVs map to other playfield positions than {accepted[0].Renderer.name}'s ({mapping.Compare(referenceMapping)}).");
						rejected.Add(material);
						continue;
					}
					if (mapping.Strays > 0) {
						Logger.Info($"Leaving {mapping.Strays} triangle(s) of {label} out of the occlusion bake: their UVs map elsewhere on the playfield.");
					}
					accepted.Add(new Receiver(candidate.Renderer, candidate.Mesh, material, mapping.Triangles));
				}
			}
			if (accepted.Count == 0) {
				return false;
			}
			receivers = accepted.ToArray();
			plane = top;
			uvTransform = reference;
			uvChannel = referenceChannel;
			aspect = referenceAspect;
			return true;
		}

		// The affine map from a submesh's (tiled) UVs to positions on the playfield plane, over its triangles that
		// face up enough to receive (the bake skips the others, like the playfield's sides). Each triangle fixes
		// one exactly; the median of theirs ignores a few stray UVs, like those of a cutout's rim.
		private sealed class UvMapping
		{
			// Share of the receiving vertices that must lie where the mapping puts them for it to count as affine. Real
			// playfields have a few percent that don't (rims and floors of cutouts unwrapped on their own).
			private const double MinAffineShare = 0.9;
			// Triangles the median is taken over, at most.
			private const int MaxSampledTriangles = 4096;

			private double[] _t; // tangent coordinate = t0 u + t1 v + t2
			private double[] _b; // bitangent coordinate = b0 u + b1 v + b2
			// Share of the receiving vertices within the tolerance of where the mapping puts them.
			private double _share;
			public bool InUnitSquare { get; private set; }
			// The receiving triangles on the mapping (vertex index triples), and how many receiving ones are off it.
			public int[] Triangles { get; private set; }
			public int Strays { get; private set; }
			public bool IsAffine => _share >= MinAffineShare;

			public static UvMapping Fit(Mesh mesh, int subMesh, int channel, Vector4 transform, Matrix4x4 localToWorld, Vector3 up, Vector3 tangent, Vector3 bitangent, float tolerance)
			{
				var uvs = new List<Vector2>();
				try {
					mesh.GetUVs(channel, uvs);
				} catch (Exception e) {
					Logger.Debug(e, $"Cannot read the UVs of {mesh.name}.");
					return null;
				}
				if (uvs.Count == 0 || mesh.GetTopology(subMesh) != MeshTopology.Triangles) {
					return null;
				}
				var vertices = mesh.vertices;
				if (uvs.Count < vertices.Length) {
					return null;
				}
				var normals = mesh.normals;
				var normalMatrix = localToWorld.inverse.transpose;
				var triangles = mesh.GetTriangles(subMesh);
				var receiving = new List<int>();
				for (var i = 0; i + 2 < triangles.Length; i += 3) {
					int i0 = triangles[i], i1 = triangles[i + 1], i2 = triangles[i + 2];
					if (i0 >= vertices.Length || i1 >= vertices.Length || i2 >= vertices.Length) {
						return null;
					}
					// The test the shader makes, on the vertex normals.
					var receives = normals.Length == vertices.Length
						? new[] { i0, i1, i2 }.Any(v => Vector3.Dot(normalMatrix.MultiplyVector(normals[v]).normalized, up) >= MinReceiverNormal)
						: Vector3.Dot(Vector3.Cross(localToWorld.MultiplyPoint3x4(vertices[i1]) - localToWorld.MultiplyPoint3x4(vertices[i0]),
							localToWorld.MultiplyPoint3x4(vertices[i2]) - localToWorld.MultiplyPoint3x4(vertices[i0])).normalized, up) >= MinReceiverNormal;
					if (receives) {
						receiving.Add(i);
					}
				}
				if (receiving.Count == 0) {
					return null;
				}

				(double U, double V, double T, double B) Point(int index)
				{
					var world = localToWorld.MultiplyPoint3x4(vertices[index]);
					return (uvs[index].x * transform.x + transform.z, uvs[index].y * transform.y + transform.w, Vector3.Dot(world, tangent), Vector3.Dot(world, bitangent));
				}

				var coefficients = new List<double>[6];
				for (var k = 0; k < 6; k++) {
					coefficients[k] = new List<double>();
				}
				var step = System.Math.Max(1, receiving.Count / MaxSampledTriangles);
				for (var n = 0; n < receiving.Count; n += step) {
					var i = receiving[n];
					var p0 = Point(triangles[i]);
					var p1 = Point(triangles[i + 1]);
					var p2 = Point(triangles[i + 2]);
					var m = new double[,] { { p0.U, p0.V, 1 }, { p1.U, p1.V, 1 }, { p2.U, p2.V, 1 } };
					var t = Solve(m, new[] { p0.T, p1.T, p2.T });
					var b = Solve(m, new[] { p0.B, p1.B, p2.B });
					if (t == null || b == null) {
						continue; // no UV area
					}
					for (var k = 0; k < 3; k++) {
						coefficients[k].Add(t[k]);
						coefficients[k + 3].Add(b[k]);
					}
				}
				if (coefficients[0].Count == 0) {
					return null;
				}
				var median = coefficients.Select(list => { list.Sort(); return list[list.Count / 2]; }).ToArray();
				var mapping = new UvMapping { _t = median.Take(3).ToArray(), _b = median.Skip(3).ToArray(), InUnitSquare = true };

				// Vertices where the mapping puts them. The others' triangles are left out of the bake: they'd write
				// their occlusion into texels of other positions.
				const double unitSquareTolerance = 0.01;
				var onMapping = new Dictionary<int, bool>();
				foreach (var index in receiving.SelectMany(i => new[] { triangles[i], triangles[i + 1], triangles[i + 2] })) {
					if (onMapping.ContainsKey(index)) {
						continue;
					}
					var (u, v, t, b) = Point(index);
					var (pt, pb) = mapping.At(u, v);
					var on = System.Math.Sqrt((pt - t) * (pt - t) + (pb - b) * (pb - b)) <= tolerance;
					onMapping.Add(index, on);
					if (on && (u < -unitSquareTolerance || u > 1 + unitSquareTolerance || v < -unitSquareTolerance || v > 1 + unitSquareTolerance)) {
						mapping.InUnitSquare = false;
					}
				}
				mapping._share = (double)onMapping.Values.Count(on => on) / onMapping.Count;
				var kept = new List<int>(receiving.Count * 3);
				foreach (var i in receiving) {
					if (onMapping[triangles[i]] && onMapping[triangles[i + 1]] && onMapping[triangles[i + 2]]) {
						kept.Add(triangles[i]);
						kept.Add(triangles[i + 1]);
						kept.Add(triangles[i + 2]);
					}
				}
				mapping.Triangles = kept.ToArray();
				mapping.Strays = receiving.Count - kept.Count / 3;
				return mapping;
			}

			// Whether both map every UV of the unit square to the same position, give or take the tolerance. A
			// mapping that isn't affine (UV islands laid out freely) matches nothing.
			public bool Matches(UvMapping other, float tolerance)
				=> _share >= MinAffineShare && other._share >= MinAffineShare && CornerDistance(other) <= tolerance;

			public string Describe() => FormattableString.Invariant($"{_share:P1} of the vertices on the fitted mapping");

			public string Compare(UvMapping other)
				=> FormattableString.Invariant($"corners {CornerDistance(other) * 1000:F1} mm apart, {_share:P1} and {other._share:P1} of the vertices on the fitted mapping");

			private double CornerDistance(UvMapping other)
				=> new[] { (0.0, 0.0), (1.0, 0.0), (0.0, 1.0), (1.0, 1.0) }.Max(c => {
					var (t0, b0) = At(c.Item1, c.Item2);
					var (t1, b1) = other.At(c.Item1, c.Item2);
					return System.Math.Sqrt((t0 - t1) * (t0 - t1) + (b0 - b1) * (b0 - b1));
				});

			private (double T, double B) At(double u, double v) => (_t[0] * u + _t[1] * v + _t[2], _b[0] * u + _b[1] * v + _b[2]);

			private static double[] Solve(double[,] m, double[] r)
			{
				var a = (double[,])m.Clone();
				var x = (double[])r.Clone();
				for (var col = 0; col < 3; col++) {
					var pivot = col;
					for (var row = col + 1; row < 3; row++) {
						if (System.Math.Abs(a[row, col]) > System.Math.Abs(a[pivot, col])) {
							pivot = row;
						}
					}
					if (System.Math.Abs(a[pivot, col]) < 1e-12) {
						return null;
					}
					for (var k = 0; k < 3; k++) {
						(a[col, k], a[pivot, k]) = (a[pivot, k], a[col, k]);
					}
					(x[col], x[pivot]) = (x[pivot], x[col]);
					for (var row = 0; row < 3; row++) {
						if (row == col) {
							continue;
						}
						var f = a[row, col] / a[col, col];
						for (var k = 0; k < 3; k++) {
							a[row, k] -= f * a[col, k];
						}
						x[row] -= f * x[col];
					}
				}
				return new[] { x[0] / a[0, 0], x[1] / a[1, 1], x[2] / a[2, 2] };
			}
		}

		private static void CollectOccluders(GameObject table, HashSet<Renderer> receivers, Vector3 up, Vector3 tangent, Vector3 bitangent,
			float plane, float falloff, Rect region, float receiverArea, List<Draw> opaque, List<Draw> transparent)
		{
			var properties = new Dictionary<Material, MaterialPropertyBlock>();
			foreach (var renderer in table.GetComponentsInChildren<MeshRenderer>()) {
				if (receivers.Contains(renderer) || !renderer.enabled || !renderer.gameObject.activeInHierarchy
					|| renderer.shadowCastingMode == ShadowCastingMode.Off || IsMoving(renderer.transform, table.transform)) {
					continue;
				}
				var mesh = renderer.GetComponent<MeshFilter>()?.sharedMesh;
				if (!mesh) {
					continue;
				}
				var corners = Corners(mesh.bounds, renderer.localToWorldMatrix);
				var top = corners.Max(c => Vector3.Dot(c, up));
				var bottom = corners.Min(c => Vector3.Dot(c, up));
				var footprint = Footprint(corners, tangent, bitangent);
				// Entirely below the playfield, too high above it to shade it, or beside it.
				if (top <= plane + PlaneTolerance || bottom > plane + falloff || !footprint.Overlaps(region)) {
					continue;
				}
				var materials = renderer.sharedMaterials;
				for (var i = 0; i < mesh.subMeshCount; i++) {
					var subMaterial = materials.Length == 0 ? null : materials[Mathf.Min(i, materials.Length - 1)];
					var isTransparent = subMaterial && subMaterial.renderQueue > (int)RenderQueue.GeometryLast;
					// The glass covers the whole playfield: as an occluder, it would shade all of it alike.
					if (isTransparent && footprint.Area() >= receiverArea * MinReceiverShare) {
						continue;
					}
					if (!subMaterial || !properties.TryGetValue(subMaterial, out var block)) {
						block = AlphaProperties(subMaterial);
						if (subMaterial) {
							properties.Add(subMaterial, block);
						}
					}
					(isTransparent ? transparent : opaque).Add(new Draw(mesh, renderer.localToWorldMatrix, i, block));
				}
			}
		}

		// Parts that move during play would leave their occlusion behind where they started.
		private static bool IsMoving(Transform transform, Transform root)
		{
			for (var t = transform; t && t != root.parent; t = t.parent) {
				foreach (var component in t.GetComponents<MonoBehaviour>()) {
					if (!component) {
						continue;
					}
					var type = component.GetType();
					if (MovingTypes.Any(moving => moving.IsAssignableFrom(type)) || IsAnimation(type)
						|| component is IKinematicTransformComponent { IsKinematic: true }) {
						return true;
					}
				}
			}
			return false;
		}

		private static bool IsAnimation(Type type)
		{
			for (var t = type; t != null && t != typeof(MonoBehaviour); t = t.BaseType) {
				if (t.IsGenericType && (t.GetGenericTypeDefinition() == typeof(AnimationComponent<>)
					|| t.GetGenericTypeDefinition() == typeof(AnimationComponentLegacy<,>))) {
					return true;
				}
			}
			return false;
		}

		private static MaterialPropertyBlock AlphaProperties(Material material)
		{
			var block = new MaterialPropertyBlock();
			block.SetFloat("_VpeAlphaCutoff", -1f);
			if (!material || !IsAlphaTested(material)) {
				return block;
			}
			foreach (var name in new[] { "_BaseColorMap", "_BaseMap", "_MainTex" }) {
				if (material.HasProperty(name) && material.GetTexture(name) is Texture texture && texture) {
					var scale = material.GetTextureScale(name);
					var offset = material.GetTextureOffset(name);
					block.SetTexture("_VpeAlphaTex", texture);
					block.SetVector("_VpeAlphaTex_ST", new Vector4(scale.x, scale.y, offset.x, offset.y));
					block.SetFloat("_VpeAlphaCutoff", GetFloat(material, "_AlphaCutoff", GetFloat(material, "_Cutoff", 0.5f)));
					break;
				}
			}
			return block;
		}

		private static bool IsAlphaTested(Material material)
			=> material.IsKeywordEnabled("_ALPHATEST_ON") || GetFloat(material, "_AlphaCutoffEnable", 0f) > 0.5f || GetFloat(material, "_AlphaClip", 0f) > 0.5f;

		private static bool IsTransparent(Renderer renderer)
			=> renderer.sharedMaterials.Any(m => m && m.renderQueue > (int)RenderQueue.GeometryLast);

		private static float GetFloat(Material material, string name, float fallback)
			=> material.HasProperty(name) ? material.GetFloat(name) : fallback;

		// The UV channel and the tiling the material's base map applies, and the base map's aspect ratio.
		private static bool TryGetUvSpace(Material material, out Vector4 transform, out int channel, out float aspect)
		{
			transform = new Vector4(1, 1, 0, 0);
			channel = 0;
			aspect = 1;
			if (!material) {
				return false;
			}
			if (material.HasProperty("_UVBase")) {
				// HDRP Lit: 0 and 1 are UV channels; the others are planar and triplanar projections.
				channel = Mathf.RoundToInt(material.GetFloat("_UVBase"));
				if (channel != 0 && channel != 1) {
					return false;
				}
			}
			foreach (var name in new[] { "_BaseColorMap", "_BaseMap", "_MainTex" }) {
				if (material.HasProperty(name)) {
					var scale = material.GetTextureScale(name);
					var offset = material.GetTextureOffset(name);
					transform = new Vector4(scale.x, scale.y, offset.x, offset.y);
					if (material.GetTexture(name) is Texture texture && texture && texture.height > 0) {
						aspect = (float)texture.width / texture.height;
					}
					break;
				}
			}
			return true;
		}

		// Cosine-weighted directions over the hemisphere around up (Hammersley points), pointing to the sky.
		private static Vector3[] Directions(int count, Vector3 up, Vector3 tangent, Vector3 bitangent)
		{
			var directions = new Vector3[count];
			for (var i = 0; i < count; i++) {
				var u = (i + 0.5f) / count;
				var bits = (uint)i;
				bits = (bits << 16) | (bits >> 16);
				bits = ((bits & 0x55555555u) << 1) | ((bits & 0xAAAAAAAAu) >> 1);
				bits = ((bits & 0x33333333u) << 2) | ((bits & 0xCCCCCCCCu) >> 2);
				bits = ((bits & 0x0F0F0F0Fu) << 4) | ((bits & 0xF0F0F0F0u) >> 4);
				bits = ((bits & 0x00FF00FFu) << 8) | ((bits & 0xFF00FF00u) >> 8);
				var phi = 2f * Mathf.PI * (bits * 2.3283064365386963e-10f);
				var r = Mathf.Sqrt(u);
				directions[i] = (tangent * (r * Mathf.Cos(phi)) + bitangent * (r * Mathf.Sin(phi)) + up * Mathf.Sqrt(1f - u)).normalized;
			}
			return directions;
		}

		// World to the orthographic clip space looking along the direction, covering the region around the
		// receivers from just below the playfield to the falloff distance above it.
		private static Matrix4x4 LightMatrix(Vector3 dir, Vector3 up, Vector3 tangent, Vector3 bitangent, Rect region, float plane, float falloff)
		{
			var right = Vector3.Cross(dir, Mathf.Abs(Vector3.Dot(dir, up)) < 0.99f ? up : tangent).normalized;
			var forward = Vector3.Cross(right, dir);
			float a0 = float.MaxValue, a1 = float.MinValue, b0 = float.MaxValue, b1 = float.MinValue;
			foreach (var x in new[] { region.xMin, region.xMax }) {
				foreach (var y in new[] { region.yMin, region.yMax }) {
					foreach (var h in new[] { plane - 0.01f, plane + falloff }) {
						var p = tangent * x + bitangent * y + up * h;
						var a = Vector3.Dot(p, right);
						var b = Vector3.Dot(p, forward);
						a0 = Mathf.Min(a0, a); a1 = Mathf.Max(a1, a);
						b0 = Mathf.Min(b0, b); b1 = Mathf.Max(b1, b);
					}
				}
			}
			var m = Matrix4x4.zero;
			var sa = 2f / Mathf.Max(1e-6f, a1 - a0);
			var sb = 2f / Mathf.Max(1e-6f, b1 - b0);
			m.SetRow(0, new Vector4(right.x * sa, right.y * sa, right.z * sa, -(a0 + a1) / (a1 - a0)));
			m.SetRow(1, new Vector4(forward.x * sb, forward.y * sb, forward.z * sb, -(b0 + b1) / (b1 - b0)));
			m.SetRow(2, new Vector4(0, 0, 0, 0.5f));
			m.SetRow(3, new Vector4(0, 0, 0, 1));
			return m;
		}

		// Visibility per texel, texels no receiver covered filled from their neighbors.
		private static float[] ReadBack(RenderTexture accumulation, int width, int height, float strength)
		{
			var previous = RenderTexture.active;
			var readable = new Texture2D(width, height, TextureFormat.RGBAFloat, false, true);
			Color[] pixels;
			try {
				RenderTexture.active = accumulation;
				readable.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
				readable.Apply(false, false);
				RenderTexture.active = previous;
				pixels = readable.GetPixels();
			} finally {
				RenderTexture.active = previous;
				Object.DestroyImmediate(readable);
			}

			var value = new float[width * height];
			var covered = new bool[width * height];
			for (var i = 0; i < pixels.Length; i++) {
				if (pixels[i].g > 0f) {
					value[i] = Mathf.Clamp01(pixels[i].r / pixels[i].g);
					covered[i] = true;
				}
			}
			// UV seams and gaps between UV islands: grow the covered texels outwards, so filtering and mips never
			// pull in the unshaded background.
			for (var pass = 0; pass < 16; pass++) {
				var grown = (bool[])covered.Clone();
				var any = false;
				for (var y = 0; y < height; y++) {
					for (var x = 0; x < width; x++) {
						var i = y * width + x;
						if (covered[i]) {
							continue;
						}
						float sum = 0;
						var count = 0;
						for (var dy = -1; dy <= 1; dy++) {
							for (var dx = -1; dx <= 1; dx++) {
								var nx = x + dx;
								var ny = y + dy;
								if (nx < 0 || ny < 0 || nx >= width || ny >= height || !covered[ny * width + nx]) {
									continue;
								}
								sum += value[ny * width + nx];
								count++;
							}
						}
						if (count > 0) {
							value[i] = sum / count;
							grown[i] = true;
							any = true;
						}
					}
				}
				covered = grown;
				if (!any) {
					break;
				}
			}
			for (var i = 0; i < value.Length; i++) {
				value[i] = covered[i] ? 1f - (1f - value[i]) * strength : 1f;
			}
			return value;
		}

		private static byte[] EncodeGray(float[] value, int width, int height)
		{
			var bytes = new byte[value.Length];
			for (var i = 0; i < value.Length; i++) {
				bytes[i] = (byte)Mathf.Clamp(Mathf.RoundToInt(value[i] * 255f), 0, 255);
			}
			var texture = new Texture2D(width, height, TextureFormat.R8, false, true);
			try {
				texture.SetPixelData(bytes, 0);
				texture.Apply(false, false);
				return texture.EncodeToPNG();
			} finally {
				Object.DestroyImmediate(texture);
			}
		}

		private static RenderTexture NewTarget(int width, int height, RenderTextureFormat format, string name)
		{
			var target = new RenderTexture(width, height, 0, format, RenderTextureReadWrite.Linear) {
				name = name,
				filterMode = FilterMode.Point,
				wrapMode = TextureWrapMode.Clamp,
				useMipMap = false,
				hideFlags = HideFlags.HideAndDontSave,
			};
			target.Create();
			return target;
		}

		private static void Release(RenderTexture target)
		{
			if (target) {
				target.Release();
				Object.DestroyImmediate(target);
			}
		}

		private static Vector3[] Corners(Bounds bounds, Matrix4x4 localToWorld)
		{
			var min = bounds.min;
			var max = bounds.max;
			var corners = new Vector3[8];
			for (var i = 0; i < 8; i++) {
				corners[i] = localToWorld.MultiplyPoint3x4(new Vector3((i & 1) == 0 ? min.x : max.x, (i & 2) == 0 ? min.y : max.y, (i & 4) == 0 ? min.z : max.z));
			}
			return corners;
		}

		private static Rect Footprint(IEnumerable<Vector3> points, Vector3 tangent, Vector3 bitangent)
		{
			float x0 = float.MaxValue, x1 = float.MinValue, y0 = float.MaxValue, y1 = float.MinValue;
			foreach (var p in points) {
				var x = Vector3.Dot(p, tangent);
				var y = Vector3.Dot(p, bitangent);
				x0 = Mathf.Min(x0, x); x1 = Mathf.Max(x1, x);
				y0 = Mathf.Min(y0, y); y1 = Mathf.Max(y1, y);
			}
			return Rect.MinMaxRect(x0, y0, x1, y1);
		}

		private static Rect Footprint(IEnumerable<Renderer> renderers, Vector3 tangent, Vector3 bitangent)
			=> Footprint(renderers.SelectMany(r => Corners(r.GetComponent<MeshFilter>().sharedMesh.bounds, r.localToWorldMatrix)), tangent, bitangent);

		private static float Area(this Rect rect) => rect.width * rect.height;
	}
}
