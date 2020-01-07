#region ReSharper
// ReSharper disable UnassignedField.Global
// ReSharper disable StringLiteralTypo
// ReSharper disable FieldCanBeMadeReadOnly.Global
// ReSharper disable ConvertToConstant.Global
#endregion

using System.Collections.Generic;
using System.IO;
using VisualPinball.Engine.IO;

namespace VisualPinball.Engine.Math
{
	public class DragPoint : BiffData
	{
		[BiffVertex("VCEN")]
		public Vertex3D Vertex;

		[BiffFloat("POSZ")]
		public float PosZ { set => Vertex.Z = value; }

		[BiffBool("SMTH")]
		public bool IsSmooth;

		[BiffBool("SLNG")]
		public bool IsSlingshot;

		[BiffBool("ATEX")]
		public bool HasAutoTexture;

		[BiffFloat("TEXC")]
		public float TextureCoord;

		public float CalcHeight;

		static DragPoint()
		{
			Init(typeof(DragPoint), Attributes);
		}

		public DragPoint(BinaryReader reader)
		{
			Load(this, reader, Attributes);
		}

		public override string ToString()
		{
			return $"DragPoint({Vertex.X}/{Vertex.Y}/{Vertex.Z}, {(IsSmooth ? "S" : "")}{(IsSlingshot ? "SS" : "S")}{(HasAutoTexture ? "A" : "")})";
		}

		private static readonly Dictionary<string, List<BiffAttribute>> Attributes = new Dictionary<string, List<BiffAttribute>>();
	}
}
