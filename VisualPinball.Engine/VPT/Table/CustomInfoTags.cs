#region ReSharper
// ReSharper disable UnassignedField.Global
// ReSharper disable StringLiteralTypo
#endregion

using System;
using System.Collections.Generic;
using System.IO;
using VisualPinball.Engine.IO;

namespace VisualPinball.Engine.VPT.Table
{
	[Serializable]
	public class CustomInfoTags : BiffData
	{
		[BiffString("CUST", TagAll = true)]
		public string[] TagNames;

		#region Data
		static CustomInfoTags()
		{
			Init(typeof(CustomInfoTags), Attributes);
		}

		public CustomInfoTags() : base("CustomInfoTags")
		{
		}

		public CustomInfoTags(BinaryReader reader) : this()
		{
			Load(this, reader, Attributes);
		}

		public override void Write(BinaryWriter writer, HashWriter hashWriter)
		{
			WriteRecord(writer, Attributes, hashWriter);
			WriteEnd(writer, hashWriter);
		}

		private static readonly Dictionary<string, List<BiffAttribute>> Attributes = new Dictionary<string, List<BiffAttribute>>();

		#endregion
	}
}
