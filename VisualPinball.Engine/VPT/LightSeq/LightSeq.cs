using System.IO;

namespace VisualPinball.Engine.VPT.LightSeq
{
	public class LightSeq : Item<LightSeqData>
	{
		public LightSeq(LightSeqData data) : base(data)
		{
		}

		public LightSeq(BinaryReader reader, string itemName) : this(new LightSeqData(reader, itemName))
		{
		}
	}
}
