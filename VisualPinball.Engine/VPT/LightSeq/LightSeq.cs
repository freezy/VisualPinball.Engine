using System.IO;

namespace VisualPinball.Engine.VPT.LightSeq
{
	public class LightSeq : Item<LightSeqData>
	{
		public LightSeq(BinaryReader reader, string itemName) : base(new LightSeqData(reader, itemName))
		{
		}
	}
}
