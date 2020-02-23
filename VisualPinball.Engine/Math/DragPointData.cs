#region ReSharper
// ReSharper disable UnassignedField.Global
// ReSharper disable StringLiteralTypo
// ReSharper disable FieldCanBeMadeReadOnly.Global
// ReSharper disable ConvertToConstant.Global
// ReSharper disable CompareOfFloatsByEqualityOperator
#endregion

using System;
using System.Collections.Generic;
using System.IO;
using VisualPinball.Engine.IO;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Engine.Math
{
	[Serializable]
	public class DragPointData : BiffData
	{
		[BiffVertex("VCEN", Pos = 1, WriteAsVertex2D = true)]
		public Vertex3D Vertex;

		[BiffFloat("POSZ", Pos = 2)]
		public float PosZ { set => Vertex.Z = value; get => Vertex.Z; }

		[BiffBool("SMTH", Pos = 3)]
		public bool IsSmooth;

		[BiffBool("SLNG", Pos = 4)]
		public bool IsSlingshot;

		[BiffBool("ATEX", Pos = 5)]
		public bool HasAutoTexture;

		[BiffFloat("TEXC", Pos = 6)]
		public float TextureCoord;

		[BiffBool("LOCK", Pos = 7)]
		public bool IsLocked;

		[BiffInt("LAYR", Pos = 8)]
		public int EditorLayer;

		public float CalcHeight;

		public override string ToString()
		{
			return $"DragPoint({Vertex.X}/{Vertex.Y}/{Vertex.Z}, {(IsSmooth ? "S" : "")}{(IsSlingshot ? "SS" : "")}{(HasAutoTexture ? "A" : "")})";
		}

		#region BIFF

		static DragPointData()
		{
			Init(typeof(DragPointData), Attributes);
		}

		public DragPointData(BinaryReader reader) : base(null)
		{
			Load(this, reader, Attributes);
		}

		public override void Write(BinaryWriter writer, HashWriter hashWriter)
		{
			Write(writer, Attributes, hashWriter);
			WriteEnd(writer, hashWriter);
		}

		private static readonly Dictionary<string, List<BiffAttribute>> Attributes = new Dictionary<string, List<BiffAttribute>>();

		#endregion
	}
}
