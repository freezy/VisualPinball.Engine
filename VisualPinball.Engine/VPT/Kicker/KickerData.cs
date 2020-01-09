#region ReSharper
// ReSharper disable UnassignedField.Global
// ReSharper disable StringLiteralTypo
// ReSharper disable FieldCanBeMadeReadOnly.Global
#endregion

using System.Collections.Generic;
using System.IO;
using VisualPinball.Engine.IO;
using VisualPinball.Engine.Math;

namespace VisualPinball.Engine.VPT.Kicker
{
	public class KickerData : ItemData
	{
		[BiffString("NAME", IsWideString = true)]
		public override string Name { get; set; }

		[BiffFloat("TYPE")]
		public float KickerType = VisualPinball.Engine.VPT.KickerType.KickerHole;

		[BiffVertex("VCEN")]
		public Vertex2D Center;

		[BiffFloat("RADI")]
		public float Radius = 25f;

		[BiffFloat("KSCT")]
		public float Scatter = 0.0f;

		[BiffFloat("KHAC")]
		public float HitAccuracy = 0.7f;

		[BiffFloat("KHHI")]
		public float HitHeight = 40.0f;

		[BiffFloat("KORI")]
		public float Orientation = 0.0f;

		[BiffString("MATR")]
		public string Material;

		[BiffString("SURF")]
		public string Surface;

		[BiffBool("FATH")]
		public bool FallThrough = false;

		[BiffBool("EBLD")]
		public bool IsEnabled = true;

		[BiffBool("LEMO")]
		public bool LegacyMode = false;

		static KickerData()
		{
			Init(typeof(KickerData), Attributes);
		}

		public KickerData(BinaryReader reader, string storageName) : base(storageName)
		{
			Load(this, reader, Attributes);
		}

		private static readonly Dictionary<string, List<BiffAttribute>> Attributes = new Dictionary<string, List<BiffAttribute>>();
	}
}
