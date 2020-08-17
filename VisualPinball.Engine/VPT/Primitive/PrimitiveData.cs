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
	[Serializable]
	public class PrimitiveData : ItemData
	{
		public override string GetName() => Name;
		public override void SetName(string name) { Name = name; }

		[BiffString("NAME", IsWideString = true, Pos = 15)]
		public string Name;

		[BiffVertex("VPOS", IsPadded = true, Pos = 1)]
		public Vertex3D Position;

		[BiffVertex("VSIZ", IsPadded = true, Pos = 2)]
		public Vertex3D Size = new Vertex3D(100, 100, 100);

		[BiffInt("M3VN", Pos = 40)]
		public int NumVertices;

		[BiffInt("M3CY", Pos = 41)]
		public int CompressedVertices;

		[BiffInt("M3FN", Pos = 43)]
		public int NumIndices;

		[BiffInt("M3CJ", Pos = 44)]
		public int CompressedIndices = 0;

		[BiffVertices("M3DX", SkipWrite = true)]
		[BiffVertices("M3CX", IsCompressed = true, Pos = 42)]
		[BiffIndices("M3DI", SkipWrite = true)]
		[BiffIndices("M3CI", IsCompressed = true, Pos = 45)]
		[BiffAnimation("M3AX", IsCompressed = true, Pos = 333)]
		public Mesh Mesh = new Mesh();

		[BiffFloat("RTV0", Index = 0, Pos = 3)]
		[BiffFloat("RTV1", Index = 1, Pos = 4)]
		[BiffFloat("RTV2", Index = 2, Pos = 5)]
		[BiffFloat("RTV3", Index = 3, Pos = 6)]
		[BiffFloat("RTV4", Index = 4, Pos = 7)]
		[BiffFloat("RTV5", Index = 5, Pos = 8)]
		[BiffFloat("RTV6", Index = 6, Pos = 9)]
		[BiffFloat("RTV7", Index = 7, Pos = 10)]
		[BiffFloat("RTV8", Index = 8, Pos = 11)]
		public float[] RotAndTra = new float[9];

		[TextureReference]
		[BiffString("IMAG", Pos = 12)]
		public string Image = string.Empty;

		[TextureReference]
		[BiffString("NRMA", Pos = 13)]
		public string NormalMap = string.Empty;

		[BiffInt("SIDS", Pos = 14)]
		public int Sides = 4;

		[MaterialReference]
		[BiffString("MATR", Pos = 16)]
		public string Material = string.Empty;

		[BiffColor("SCOL", Pos = 17)]
		public Color SideColor = new Color(0x0, ColorFormat.Bgr);

		[BiffBool("TVIS", Pos = 18)]
		public bool IsVisible = true;

		[BiffBool("REEN", Pos = 34)]
		public bool IsReflectionEnabled = true;

		[BiffBool("DTXI", Pos = 19)]
		public bool DrawTexturesInside;

		[BiffBool("HTEV", Pos = 20)]
		public bool HitEvent = true;

		[BiffFloat("THRS", Pos = 21)]
		public float Threshold = 2f;

		[BiffFloat("ELAS", Pos = 22)]
		public float Elasticity = 0.3f;

		[BiffFloat("ELFO", Pos = 23)]
		public float ElasticityFalloff = 0.5f;

		[BiffFloat("RFCT", Pos = 24)]
		public float Friction = 0.3f;

		[BiffFloat("RSCT", Pos = 25)]
		public float Scatter;

		[BiffFloat("EFUI", Pos = 26)]
		public float EdgeFactorUi = 0.25f;

		[BiffFloat("CORF", Pos = 27)]
		public float CollisionReductionFactor = 0;

		[BiffBool("CLDR", Pos = 28)]
		public bool IsCollidable = true; // originally "CLDRP"

		[BiffBool("ISTO", Pos = 29)]
		public bool IsToy;

		[MaterialReference]
		[BiffString("MAPH", Pos = 36)]
		public string PhysicsMaterial = string.Empty;

		[BiffBool("OVPH", Pos = 37)]
		public bool OverwritePhysics = true;

		[BiffBool("STRE", Pos = 31)]
		public bool StaticRendering = true;

		[BiffFloat("DILI", QuantizedUnsignedBits = 8, Pos = 32)]
		public float DisableLightingTop; // m_d.m_fDisableLightingTop = (tmp == 1) ? 1.f : dequantizeUnsigned<8>(tmp); // backwards compatible hacky loading!

		[BiffFloat("DILB", Pos = 33)]
		public float DisableLightingBelow;

		[BiffBool("U3DM", Pos = 30)]
		public bool Use3DMesh;

		[BiffBool("EBFC", Pos = 35)]
		public bool BackfacesEnabled;

		[BiffBool("DIPT", Pos = 38)]
		public bool DisplayTexture;

		[BiffBool("OSNM", Pos = 38.5)]
		public bool ObjectSpaceNormalMap;

		[BiffString("M3DN", Pos = 39)]
		public string MeshFileName = string.Empty;

		[BiffFloat("PIDB", Pos = 46)]
		public float DepthBias = 0;

		protected override bool SkipWrite(BiffAttribute attr)
		{
			if (!Use3DMesh) {
				switch (attr.Name) {
					case "M3VN":
					case "M3CY":
					case "M3FN":
					case "M3CJ":
						return true;
				}
			}
			return false;
		}

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

		public PrimitiveData(string name, float x, float y) : base(StoragePrefix.GameItem)
		{
			Name = name;
			Position = new Vertex3D(x, y, 0f);
		}

		public override void Write(BinaryWriter writer, HashWriter hashWriter)
		{
			writer.Write((int)ItemType.Primitive);
			WriteRecord(writer, Attributes, hashWriter);
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
			if (obj is PrimitiveData primitiveData) {
				if (!primitiveData.Use3DMesh) {
					// don't write vertices if not using 3d mesh
					return;
				}
				var vertexData = SerializeVertices(primitiveData);
				var data = IsCompressed ? BiffZlib.Compress(vertexData) : vertexData;
				WriteStart(writer, data.Length, hashWriter);
				writer.Write(data);
				hashWriter?.Write(data);

			} else {
				throw new InvalidOperationException("Unknown type for [" + GetType().Name + "] on field \"" + Name + "\".");
			}
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

		private static byte[] SerializeVertices(PrimitiveData data)
		{
			using (var stream = new MemoryStream())
			using (var writer = new BinaryWriter(stream)) {
				for (var i = 0; i < data.NumVertices; i++) {
					data.Mesh.Vertices[i].Write(writer);
				}
				return stream.ToArray();
			}
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
			if (obj is PrimitiveData primitiveData) {
				if (!primitiveData.Use3DMesh) {
					return;
				}
				var indexData = SerializeIndices(primitiveData);
				var data = IsCompressed ? BiffZlib.Compress(indexData) : indexData;
				WriteStart(writer, data.Length, hashWriter);
				writer.Write(data);
				hashWriter?.Write(data);

			} else {
				throw new InvalidOperationException("Unknown type for [" + GetType().Name + "] on field \"" + Name + "\".");
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

		private static byte[] SerializeIndices(PrimitiveData data)
		{
			using (var stream = new MemoryStream())
			using (var writer = new BinaryWriter(stream)) {
				for (var i = 0; i < data.NumIndices; i++) {
					if (data.NumVertices > 65535) {
						writer.Write((uint) data.Mesh.Indices[i]);

					} else {
						writer.Write((ushort) data.Mesh.Indices[i]);
					}
				}
				return stream.ToArray();
			}
		}
	}

	/// <summary>
	/// Parses animated vertex data.<p/>
	///
	/// </summary>
	public class BiffAnimationAttribute : BiffAttribute
	{
		/// <summary>
		/// If set, the vertices are Zlib-compressed.
		/// </summary>
		public bool IsCompressed;

		public BiffAnimationAttribute(string name) : base(name) { }

		public override void Parse<T>(T obj, BinaryReader reader, int len)
		{
			if (obj is PrimitiveData primitiveData)
			{
				try
				{
					ParseAnimation(primitiveData, IsCompressed
						? BiffZlib.Decompress(reader.ReadBytes(len))
						: reader.ReadBytes(len));
				}
				catch (Exception e)
				{
					throw new Exception($"Error parsing animation data for {primitiveData.Name} ({primitiveData.StorageName}).", e);
				}
			}
		}

		public override void Write<TItem>(TItem obj, BinaryWriter writer, HashWriter hashWriter)
		{
			if (obj is PrimitiveData primitiveData)
			{
				if (!primitiveData.Use3DMesh)
				{
					// don't write animation if not using 3d mesh
					return;
				}

				for (var i = 0; i < primitiveData.Mesh.AnimationFrames.Count; i++)
				{
					var animationData = SerializeAnimation(primitiveData.Mesh.AnimationFrames[i]);
					var data = IsCompressed ? BiffZlib.Compress(animationData) : animationData;
					WriteStart(writer, data.Length, hashWriter);
					writer.Write(data);
					hashWriter?.Write(data);
				}

			}
			else
			{
				throw new InvalidOperationException("Unknown type for [" + GetType().Name + "] on field \"" + Name + "\".");
			}
		}

		private void ParseAnimation(PrimitiveData data, byte[] bytes)
		{
			if (data.NumVertices == 0)
			{
				throw new ArgumentOutOfRangeException(nameof(data), "Cannot create animation when size is unknown.");
			}

			if (bytes.Length < data.NumVertices * Mesh.VertData.Size)
			{
				throw new ArgumentOutOfRangeException($"Tried to read {data.NumVertices} vertex animations for primitive item \"${data.Name}\" (${data.StorageName}), but only ${bytes.Length} bytes available.");
			}

			if (!(GetValue(data) is Mesh mesh))
			{
				throw new ArgumentException("BiffAnimationAttribute attribute must sit on a Mesh object.");
			}

			var vertices = new Mesh.VertData[data.NumVertices];
			using (var stream = new MemoryStream(bytes))
			using (var reader = new BinaryReader(stream))
			{
				for (var i = 0; i < data.NumVertices; i++)
				{
					vertices[i] = new Mesh.VertData(reader);
				}
			}
			mesh.AnimationFrames.Add(vertices);
		}

		private static byte[] SerializeAnimation(Mesh.VertData[] data)
		{
			using (var stream = new MemoryStream())
			using (var writer = new BinaryWriter(stream))
			{
				for (var i = 0; i < data.Length; i++)
				{
					data[i].Write(writer);
				}
				return stream.ToArray();
			}
		}
	}
}
