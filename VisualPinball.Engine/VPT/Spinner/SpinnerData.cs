#region ReSharper
// ReSharper disable UnassignedField.Global
// ReSharper disable StringLiteralTypo
// ReSharper disable FieldCanBeMadeReadOnly.Global
// ReSharper disable ConvertToConstant.Global
#endregion

using System.Collections.Generic;
using System.IO;
using VisualPinball.Engine.IO;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Engine.VPT.Spinner
{
	public class SpinnerData : ItemData
	{
		[BiffString("NAME", IsWideString = true)]
		public override string Name { get; set; }

		[BiffVertex("VCEN")]
		public Vertex2D Center;

		[BiffFloat("ROTA")]
		public float Rotation = 0f;

		[BiffString("MATR")]
		public string Material;

		[BiffBool("SSUP")]
		public bool ShowBracket = true;

		[BiffFloat("HIGH")]
		public float Height = 60f;

		[BiffFloat("LGTH")]
		public float Length = 80f;

		[BiffFloat("AFRC")]
		public float Damping;

		[BiffFloat("SMAX")]
		public float AngleMax = 0f;

		[BiffFloat("SMIN")]
		public float AngleMin = 0f;

		[BiffFloat("SELA")]
		public float Elasticity;

		[BiffBool("SVIS")]
		public bool IsVisible = true;

		[BiffString("IMGF")]
		public string Image;

		[BiffString("SURF")]
		public string Surface;

		#region BIFF

		static SpinnerData()
		{
			Init(typeof(SpinnerData), Attributes);
		}

		public SpinnerData(BinaryReader reader, string storageName) : base(storageName)
		{
			Load(this, reader, Attributes);
		}

		public override void Write(BinaryWriter writer, HashWriter hashWriter)
		{
			writer.Write(ItemType.Spinner);
			Write(writer, Attributes, hashWriter);
			WriteEnd(writer, hashWriter);
		}

		private static readonly Dictionary<string, List<BiffAttribute>> Attributes = new Dictionary<string, List<BiffAttribute>>();

		#endregion
	}
}
