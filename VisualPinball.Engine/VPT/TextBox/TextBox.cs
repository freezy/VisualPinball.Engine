using System.IO;

namespace VisualPinball.Engine.VPT.TextBox
{
	public class TextBox : Item<TextBoxData>
	{
		public TextBox(BinaryReader reader, string itemName) : base(new TextBoxData(reader, itemName))
		{
		}
	}
}
