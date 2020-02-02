#region ReSharper
// ReSharper disable UnassignedField.Global
// ReSharper disable StringLiteralTypo
// ReSharper disable FieldCanBeMadeReadOnly.Global
#endregion

using System.Collections.Generic;
using System.IO;
using VisualPinball.Engine.IO;
using VisualPinball.Engine.Math;

namespace VisualPinball.Engine.VPT.Decal
{
	public class DecalData : ItemData
	{
		[BiffString("NAME", IsWideString = true)]
		public override string Name { get; set; }

		[BiffVertex("VCEN")]
		public Vertex2D Center;

		[BiffFloat("WDTH")]
		public float Width = 100.0f;

		[BiffFloat("HIGH")]
		public float Height = 100.0f;

		[BiffFloat("ROTA")]
		public float Rotation = 0.0f;

		[BiffString("IMAG")]
		public string Image;

		[BiffString("SURF")]
		public string Surface;

		[BiffString("TEXT")]
		public string Text;

		[BiffInt("TYPE")]
		public int DecalType = VisualPinball.Engine.VPT.DecalType.DecalImage;

		[BiffInt("SIZE")]
		public int SizingType = VisualPinball.Engine.VPT.SizingType.ManualSize;

		[BiffColor("XXXX")]
		public Color Color = new Color(0x000000, ColorFormat.Bgr);

		[BiffString("MATR")]
		public string Material;

		[BiffBool("VERT")]
		public bool VerticalText = false;

		[BiffBool("BGLS")]
		public bool Backglass = false;

		// [BiffString("FONT")]
		// public string Font = "";

		#region BIFF

		static DecalData()
		{
			Init(typeof(DecalData), Attributes);
		}

		public DecalData(BinaryReader reader, string storageName) : base(storageName)
		{
			Load(this, reader, Attributes);
		}

		public override void Write(BinaryWriter writer)
		{
			writer.Write(ItemType.Decal);
			Write(writer, Attributes);
			WriteEnd(writer);
		}

		private static readonly Dictionary<string, List<BiffAttribute>> Attributes = new Dictionary<string, List<BiffAttribute>>();

		#endregion
	}
}
