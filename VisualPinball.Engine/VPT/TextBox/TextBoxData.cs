#region ReSharper
// ReSharper disable UnassignedField.Global
// ReSharper disable StringLiteralTypo
// ReSharper disable FieldCanBeMadeReadOnly.Global
#endregion

using System.Collections.Generic;
using System.IO;
using VisualPinball.Engine.IO;
using VisualPinball.Engine.Math;

namespace VisualPinball.Engine.VPT.TextBox
{
	public class TextBoxData : ItemData
	{
		[BiffString("NAME", IsWideString = true)]
		public override string Name { get; set; }

		[BiffVertex("VER1")]
		public Vertex2D V1;

		[BiffVertex("VER2")]
		public Vertex2D V2;

		[BiffColor("CLRB")]
		public Color BackColor = new Color(0x000000f, ColorFormat.Bgr);

		[BiffColor("CLRF")]
		public Color FontColor = new Color(0xfffffff, ColorFormat.Bgr);

		[BiffFloat("INSC")]
		public float IntensityScale = 1.0f;

		[BiffString("TEXT")]
		public string Text = "0";

		[BiffInt("ALGN")]
		public int Align = TextAlignment.TextAlignRight;

		[BiffBool("TRNS")]
		public bool IsTransparent = false;

		[BiffBool("IDMD")]
		public bool IsDMD = false;

		#region BIFF

		static TextBoxData()
		{
			Init(typeof(TextBoxData), Attributes);
		}

		public TextBoxData(BinaryReader reader, string storageName) : base(storageName)
		{
			Load(this, reader, Attributes);
		}

		public override void Write(BinaryWriter writer)
		{
			writer.Write(ItemType.Textbox);
			Write(writer, Attributes);
			WriteEnd(writer);
		}

		private static readonly Dictionary<string, List<BiffAttribute>> Attributes = new Dictionary<string, List<BiffAttribute>>();

		#endregion
	}
}
