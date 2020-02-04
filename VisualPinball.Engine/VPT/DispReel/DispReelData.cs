#region ReSharper
// ReSharper disable UnassignedField.Global
// ReSharper disable StringLiteralTypo
// ReSharper disable FieldCanBeMadeReadOnly.Global
#endregion

using System.Collections.Generic;
using System.IO;
using VisualPinball.Engine.IO;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Engine.VPT.DispReel
{
	public class DispReelData : ItemData
	{
		[BiffString("NAME", IsWideString = true)]
		public override string Name { get; set; }

		[BiffVertex("VER1")]
		public Vertex2D V1;

		[BiffVertex("VER2")]
		public Vertex2D V2;

		[BiffFloat("WDTH")]
		public float Width = 30.0f;

		[BiffFloat("HIGH")]
		public float Height = 40.0f;

		[BiffColor("CLRB")]
		public Color BackColor = new Color(0x404040f, ColorFormat.Bgr);

		[BiffBool("TRNS")]
		public bool IsTransparent = false;

		[BiffBool("VISI")]
		public bool IsVisible = true;

		[BiffString("IMAG")]
		public string Image;

		[BiffFloat("RCNT", AsInt = true)]
		public int ReelCount = 5;

		[BiffFloat("RSPC")]
		public float ReelSpacing = 4.0f;

		[BiffFloat("MSTP", AsInt = true)]
		public int MotorSteps = 2;

		[BiffString("SOUN")]
		public string Sound;

		[BiffBool("UGRD")]
		public bool UseImageGrid = false;

		[BiffInt("GIPR")]
		public int ImagesPerGridRow = 1;

		[BiffFloat("RANG", AsInt = true)]
		public int DigitRange = 9;

		[BiffInt("UPTM")]
		public int UpdateInterval = 50;

		// [BiffString("FONT")]
		// public string Font = "";

		#region BIFF

		static DispReelData()
		{
			Init(typeof(DispReelData), Attributes);
		}

		public DispReelData(BinaryReader reader, string storageName) : base(storageName)
		{
			Load(this, reader, Attributes);
		}

		public override void Write(BinaryWriter writer, HashWriter hashWriter)
		{
			writer.Write(ItemType.DispReel);
			Write(writer, Attributes, hashWriter);
			WriteEnd(writer, hashWriter);
		}

		private static readonly Dictionary<string, List<BiffAttribute>> Attributes = new Dictionary<string, List<BiffAttribute>>();

		#endregion
	}
}
