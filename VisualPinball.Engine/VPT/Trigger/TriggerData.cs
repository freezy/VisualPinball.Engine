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
		[BiffString("NAME", IsWideString = true)]
		public override string Name { get; set; }

		[BiffDragPoint("DPNT")]
		public DragPointData[] DragPoints;

		[BiffVertex("VCEN")]
		public Vertex2D Center;

		[BiffFloat("RADI")]
		public float Radius = 25f;

		[BiffFloat("ROTA")]
		public float Rotation = 0f;

		[BiffFloat("SCAX")]
		public float ScaleX = 1f;

		[BiffFloat("SCAY")]
		public float ScaleY = 1f;

		[BiffString("MATR")]
		public string Material;

		[BiffString("SURF")]
		public string Surface;

		[BiffBool("VSBL")]
		public bool IsVisible = true;

		[BiffBool("EBLD")]
		public bool IsEnabled = true;

		[BiffFloat("THOT")]
		public float HitHeight = 50f;

		[BiffInt("SHAP")]
		public int Shape = TriggerShape.TriggerWireA;

		[BiffFloat("ANSP")]
		public float AnimSpeed = 1f;

		[BiffFloat("WITI")]
		public float WireThickness = 0f;

		[BiffBool("REEN")]
		public bool IsReflectionEnabled = true;

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
