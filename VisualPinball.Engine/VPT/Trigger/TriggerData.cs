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

namespace VisualPinball.Engine.VPT.Trigger
{
	public class TriggerData : ItemData
	{
		[BiffString("NAME", IsWideString = true, Pos = 14)]
		public override string Name { get; set; }

		[BiffDragPoint("DPNT", TagAll = true, Pos = 2000)]
		public DragPointData[] DragPoints;

		[BiffVertex("VCEN", Pos = 1)]
		public Vertex2D Center;

		[BiffFloat("RADI", Pos = 2)]
		public float Radius = 25f;

		[BiffFloat("ROTA", Pos = 3)]
		public float Rotation = 0f;

		[BiffFloat("SCAX", Pos = 5)]
		public float ScaleX = 1f;

		[BiffFloat("SCAY", Pos = 6)]
		public float ScaleY = 1f;

		[BiffString("MATR", Pos = 10)]
		public string Material;

		[BiffString("SURF", Pos = 9)]
		public string Surface;

		[BiffBool("VSBL", Pos = 12)]
		public bool IsVisible = true;

		[BiffBool("EBLD", Pos = 11)]
		public bool IsEnabled = true;

		[BiffFloat("THOT", Pos = 13)]
		public float HitHeight = 50f;

		[BiffInt("SHAP", Pos = 15)]
		public int Shape = TriggerShape.TriggerWireA;

		[BiffFloat("ANSP", Pos = 16)]
		public float AnimSpeed = 1f;

		[BiffFloat("WITI", Pos = 4)]
		public float WireThickness = 0f;

		[BiffBool("REEN", Pos = 17)]
		public bool IsReflectionEnabled = true;

		[BiffBool("TMON", Pos = 7)]
		public bool IsTimerEnabled;

		[BiffInt("TMIN", Pos = 8)]
		public int TimerInterval;

		#region BIFF

		static TriggerData()
		{
			Init(typeof(TriggerData), Attributes);
		}

		public TriggerData(BinaryReader reader, string storageName) : base(storageName)
		{
			Load(this, reader, Attributes);
		}

		public override void Write(BinaryWriter writer, HashWriter hashWriter)
		{
			writer.Write(ItemType.Trigger);
			Write(writer, Attributes, hashWriter);
			WriteEnd(writer, hashWriter);
		}

		private static readonly Dictionary<string, List<BiffAttribute>> Attributes = new Dictionary<string, List<BiffAttribute>>();

		#endregion
	}
}
