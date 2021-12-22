using System;
using System.Collections.Generic;
using System.Text;

namespace VisualPinball.Unity.Editor
{
	[Serializable]
	public class PinballTagsMetadata : PinballMetadataExtension
	{
		public PinballTagsMetadata() : base()	{}
		public List<PinballTag> Tags = new List<PinballTag>();
	}

}
