#region ReSharper
// ReSharper disable UnassignedField.Global
// ReSharper disable StringLiteralTypo
#endregion

using System.Collections.Generic;
using System.IO;
using VisualPinball.Engine.IO;

namespace VisualPinball.Engine.VPT.Table
{
	public class CustomInfoTags : BiffData
	{
		[BiffString("CUST", TagAll = true)]
		public string[] TagNames;

		#region Data
		static CustomInfoTags()
		{
			Init(typeof(CustomInfoTags), Attributes);
		}

		public CustomInfoTags(BinaryReader reader) : base("CustomInfoTags")
		{
			Load(this, reader, Attributes);
		}

		public override void Write(BinaryWriter writer, HashWriter hashWriter)
		{
			Write(writer, Attributes, hashWriter);
			WriteEnd(writer, hashWriter);
		}

		private static readonly Dictionary<string, List<BiffAttribute>> Attributes = new Dictionary<string, List<BiffAttribute>>();

		#endregion
	}
}
