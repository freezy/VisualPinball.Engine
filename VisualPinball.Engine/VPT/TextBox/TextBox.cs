using System.IO;

namespace VisualPinball.Engine.VPT.TextBox
{
	public class TextBox : Item<TextBoxData>
	{
		public TextBox(TextBoxData data) : base(data)
		{
		}

		public TextBox(BinaryReader reader, string itemName) : this(new TextBoxData(reader, itemName))
		{
		}
	}
}
