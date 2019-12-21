using System;
using System.Collections.Generic;
using System.IO;
using VisualPinball.Engine.IO;
using VisualPinball.Engine.Math;

namespace VisualPinball.Engine.VPT.Primitive
{
	public class PrimitiveData : ItemData
	{
		[Biff("VPOS")]
		public Vertex3D Position;

		[Biff("VSIZ")]
		public Vertex3D Size = new Vertex3D(100, 100, 100);

		[BiffVertices("M3DX")]
		[BiffVertices("M3CX", IsCompressed = true)]
		[BiffIndices("M3DI")]
		[BiffIndices("M3CI", IsCompressed = true)]
		public readonly Mesh Mesh = new Mesh();

		[Biff("RTV0", Index = 0)]
		[Biff("RTV1", Index = 1)]
		[Biff("RTV2", Index = 2)]
		[Biff("RTV3", Index = 3)]
		[Biff("RTV4", Index = 4)]
		[Biff("RTV5", Index = 5)]
		[Biff("RTV6", Index = 6)]
		[Biff("RTV7", Index = 7)]
		[Biff("RTV8", Index = 8)]
		public float[] RotAndTra = new float[9];

		[Biff("IMAG")]
		public string Image;

		[Biff("NRMA")]
		public string NormalMap;

		[Biff("SIDS")]
		public int Sides;

		[Biff("MATR")]
		public string Material;

		[Biff("SCOL")]
		public int SideColor;

		[Biff("TVIS")]
		public bool IsVisible;

		[Biff("REEN")]
		public bool IsReflectionEnabled;

		[Biff("DTXI")]
		public bool DrawTexturesInside;

		[Biff("HTEV")]
		public bool HitEvent;

		[Biff("THRS")]
		public float Threshold;

		[Biff("ELAS")]
		public float Elasticity;

		[Biff("ELFO")]
		public float ElasticityFalloff;

		[Biff("RFCT")]
		public float Friction;

		[Biff("RSCT")]
		public float Scatter;

		[Biff("EFUI")]
		public float EdgeFactorUi;

		[Biff("CORF")]
		public float CollisionReductionFactor = 0;

		[Biff("CLDR")]
		public bool IsCollidable = true; // originally "CLDRP"

		[Biff("ISTO")]
		public bool IsToy;

		[Biff("MAPH")]
		public string PhysicsMaterial;

		[Biff("OVPH")]
		public bool OverwritePhysics;

		[Biff("STRE")]
		public bool StaticRendering;

		[Biff("DILI")]
		public float DisableLightingTop; // m_d.m_fDisableLightingTop = (tmp == 1) ? 1.f : dequantizeUnsigned<8>(tmp); // backwards compatible hacky loading!

		[Biff("DILB")]
		public float DisableLightingBelow;

		[Biff("U3DM")]
		public bool Use3DMesh;

		[Biff("EBFC")]
		public bool BackfacesEnabled;

		[Biff("DIPT")]
		public bool DisplayTexture;

		[Biff("M3DN", IsWideString = true)]
		public string MeshFileName;

		[Biff("M3VN")]
		public int NumVertices = 0;

		[Biff("M3CY")]
		public int CompressedVertices = 0;

		[Biff("M3FN")]
		public int NumIndices = 0;

		[Biff("M3CJ")]
		public int CompressedIndices = 0;

		[Biff("PIDB")]
		public float DepthBias = 0;

		static PrimitiveData()
		{
			Init(typeof(PrimitiveData), Attributes);
		}

		public PrimitiveData(BinaryReader reader, string storageName) : base(storageName)
		{
			Load(this, reader, Attributes);
		}

		private static readonly Dictionary<string, BiffAttribute> Attributes = new Dictionary<string, BiffAttribute>();
	}

	/// <summary>
	/// Parses vertex data.<p/>
	///
	/// Since we additionally need <see cref="PrimitiveData.NumVertices"/> in
	/// order to know how many vertices to parse, we can't use the standard
	/// BiffAttribute.
	/// </summary>
	public class BiffVerticesAttribute : BiffAttribute
	{
		/// <summary>
		/// If set, the vertices are Zlib-compressed.
		/// </summary>
		public bool IsCompressed;

		public BiffVerticesAttribute(string name) : base(name) { }

		public override void Parse<T>(T obj, BinaryReader reader, int len)
		{
			if (obj is PrimitiveData tableData) {
				try {
					ParseVertices(tableData, IsCompressed
						? BiffZlib.Decompress(reader.ReadBytes(len))
						: reader.ReadBytes(len));
				} catch (Exception e) {
					throw new Exception($"Error parsing vertices for {obj.Name} ({obj.StorageName}).", e);
				}

			} else {
				base.Parse(obj, reader, len);
			}
		}

		private void ParseVertices(PrimitiveData data, byte[] bytes)
		{
			if (data.NumVertices == 0) {
				throw new ArgumentOutOfRangeException($"Cannot add vertices when size is unknown.");
			}

			if (bytes.Length < data.NumVertices * Vertex3DNoTex2.Size) {
				throw new ArgumentOutOfRangeException($"Tried to read {data.NumVertices} vertices for primitive item \"${data.Name}\" (${data.StorageName}), but only ${bytes.Length} bytes available.");
			}

			if (!(GetValue(data) is Mesh mesh)) {
				throw new ArgumentException("BiffVertices attribute must sit on a Mesh object.");
			}

			var vertices = new Vertex3DNoTex2[data.NumVertices];
			using (var stream = new MemoryStream(bytes))
			using (var reader = new BinaryReader(stream)) {
				for (var i = 0; i < data.NumVertices; i++) {
					vertices[i] = new Vertex3DNoTex2(reader);
				}
			}
			mesh.Vertices = vertices;
		}
	}

	/// <summary>
	/// Parses index data.<p/>
	///
	/// Since we additionally need <see cref="PrimitiveData.NumIndices"/> in
	/// order to know how many indices to parse, we can't use the standard
	/// BiffAttribute.
	/// </summary>
	public class BiffIndicesAttribute : BiffAttribute
	{
		public bool IsCompressed;

		public BiffIndicesAttribute(string name) : base(name) { }

		public override void Parse<T>(T obj, BinaryReader reader, int len)
		{
			if (obj is PrimitiveData tableData) {
				ParseIndices(tableData, IsCompressed
					? BiffZlib.Decompress(reader.ReadBytes(len))
					: reader.ReadBytes(len));

			} else {
				base.Parse(obj, reader, len);
			}
		}

		private void ParseIndices(PrimitiveData data, byte[] bytes)
		{
			if (data.NumIndices == 0) {
				throw new ArgumentOutOfRangeException($"Cannot add indices when size is unknown.");
			}

			if (!(GetValue(data) is Mesh mesh)) {
				throw new ArgumentException("BiffIndices attribute must sit on a Mesh object.");
			}

			var indices = new int[data.NumIndices];
			using (var stream = new MemoryStream(bytes))
			using (var reader = new BinaryReader(stream)) {
				for (var i = 0; i < data.NumIndices; i++) {
					indices[i] = data.NumVertices > 65535 ? (int)reader.ReadUInt32() : reader.ReadUInt16();
				}
			}
			mesh.Indices = indices;
		}
	}

}
