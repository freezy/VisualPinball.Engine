using System.IO;

namespace VisualPinball.Engine.VPT.TextBox
{
	public class TextBox : Item<TextBoxData>, ITimer, IEditable
	{
		public bool IsTimerEnabled { get => Data.IsTimerEnabled; set => Data.IsTimerEnabled = value; }
		public int TimerInterval { get => Data.TimerInterval; set => Data.TimerInterval = value; }
		public bool IsLocked { get => Data.IsLocked; set => Data.IsLocked = value; }
		public int EditorLayer { get => Data.EditorLayer; set => Data.EditorLayer = value; }

		public TextBox(BinaryReader reader, string itemName) : base(new TextBoxData(reader, itemName))
		{
		}
	}
}
