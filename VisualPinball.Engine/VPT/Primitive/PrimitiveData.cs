#region ReSharper
// ReSharper disable UnassignedField.Global
// ReSharper disable StringLiteralTypo
// ReSharper disable FieldCanBeMadeReadOnly.Global
// ReSharper disable ConvertToConstant.Global
#endregion

using System;
using System.Collections.Generic;
using System.IO;
using VisualPinball.Engine.IO;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Engine.VPT.Primitive
{
	public class PrimitiveData : ItemData
	{
		[BiffString("NAME", IsWideString = true)]
		public override string Name { get; set; }

		[BiffVertex("VPOS")]
		public Vertex3D Position;

		[BiffVertex("VSIZ")]
		public Vertex3D Size = new Vertex3D(100, 100, 100);

		[BiffVertices("M3DX")]
		[BiffVertices("M3CX", IsCompressed = true)]
		[BiffIndices("M3DI")]
		[BiffIndices("M3CI", IsCompressed = true)]
		public readonly Mesh Mesh = new Mesh();

		[BiffFloat("RTV0", Index = 0)]
		[BiffFloat("RTV1", Index = 1)]
		[BiffFloat("RTV2", Index = 2)]
		[BiffFloat("RTV3", Index = 3)]
		[BiffFloat("RTV4", Index = 4)]
		[BiffFloat("RTV5", Index = 5)]
		[BiffFloat("RTV6", Index = 6)]
		[BiffFloat("RTV7", Index = 7)]
		[BiffFloat("RTV8", Index = 8)]
		public float[] RotAndTra = new float[9];

		[BiffString("IMAG")]
		public string Image;

		[BiffString("NRMA")]
		public string NormalMap;

		[BiffInt("SIDS")]
		public int Sides;

		[BiffString("MATR")]
		public string Material;

		[BiffColor("SCOL")]
		public Color SideColor = new Color(0x0, ColorFormat.Bgr);

		[BiffBool("TVIS")]
		public bool IsVisible;

		[BiffBool("REEN")]
		public bool IsReflectionEnabled;

		[BiffBool("DTXI")]
		public bool DrawTexturesInside;

		[BiffBool("HTEV")]
		public bool HitEvent;

		[BiffFloat("THRS")]
		public float Threshold;

		[BiffFloat("ELAS")]
		public float Elasticity;

		[BiffFloat("ELFO")]
		public float ElasticityFalloff;

		[BiffFloat("RFCT")]
		public float Friction;

		[BiffFloat("RSCT")]
		public float Scatter;

		[BiffFloat("EFUI")]
		public float EdgeFactorUi;

		[BiffFloat("CORF")]
		public float CollisionReductionFactor = 0;

		[BiffBool("CLDR")]
		public bool IsCollidable = true; // originally "CLDRP"

		[BiffBool("ISTO")]
		public bool IsToy;

		[BiffString("MAPH")]
		public string PhysicsMaterial;

		[BiffBool("OVPH")]
		public bool OverwritePhysics;

		[BiffBool("STRE")]
		public bool StaticRendering;

		[BiffFloat("DILI", QuantizedUnsignedBits = 8)]
		public float DisableLightingTop; // m_d.m_fDisableLightingTop = (tmp == 1) ? 1.f : dequantizeUnsigned<8>(tmp); // backwards compatible hacky loading!

		[BiffFloat("DILB")]
		public float DisableLightingBelow;

		[BiffBool("U3DM")]
		public bool Use3DMesh;

		[BiffBool("EBFC")]
		public bool BackfacesEnabled;

		[BiffBool("DIPT")]
		public bool DisplayTexture;

		[BiffString("M3DN")]
		public string MeshFileName;

		[BiffInt("M3VN")]
		public int NumVertices;

		[BiffInt("M3CY")]
		public int CompressedVertices;

		[BiffInt("M3FN")]
		public int NumIndices;

		[BiffInt("M3CJ")]
		public int CompressedIndices = 0;

		[BiffFloat("PIDB")]
		public float DepthBias = 0;

		#region BIFF

		static PrimitiveData()
		{
			Init(typeof(PrimitiveData), Attributes);
		}

		public PrimitiveData(BinaryReader reader, string storageName) : base(storageName)
		{
			Load(this, reader, Attributes);
			Mesh.Name = Name;
		}

		public override void Write(BinaryWriter writer, HashWriter hashWriter)
		{
			writer.Write(ItemType.Primitive);
			Write(writer, Attributes, hashWriter);
			WriteEnd(writer, hashWriter);
		}

		private static readonly Dictionary<string, List<BiffAttribute>> Attributes = new Dictionary<string, List<BiffAttribute>>();

		#endregion
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
			if (obj is PrimitiveData primitiveData) {
				try {
					ParseVertices(primitiveData, IsCompressed
						? BiffZlib.Decompress(reader.ReadBytes(len))
						: reader.ReadBytes(len));
				} catch (Exception e) {
					throw new Exception($"Error parsing vertices for {primitiveData.Name} ({primitiveData.StorageName}).", e);
				}
			}
		}

		public override void Write<TItem>(TItem obj, BinaryWriter writer, HashWriter hashWriter)
		{
			//throw new NotImplementedException();
		}

		private void ParseVertices(PrimitiveData data, byte[] bytes)
		{
			if (data.NumVertices == 0) {
				throw new ArgumentOutOfRangeException(nameof(data), "Cannot add vertices when size is unknown.");
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
			}
		}

		public override void Write<TItem>(TItem obj, BinaryWriter writer, HashWriter hashWriter)
		{
			//throw new NotImplementedException();
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
