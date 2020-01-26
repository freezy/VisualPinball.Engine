#region ReSharper
// ReSharper disable UnassignedField.Global
// ReSharper disable StringLiteralTypo
// ReSharper disable FieldCanBeMadeReadOnly.Global
#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using VisualPinball.Engine.IO;
using VisualPinball.Engine.Resources;

namespace VisualPinball.Engine.VPT
{
	public class BinaryData : ItemData, IImageData
	{
		public byte[] Bytes => Data;
		public byte[] FileContent => Data;

		[BiffString("NAME", HasExplicitLength = true)]
		public override string Name { get; set; }

		[BiffString("INME")]
		public string InternalName;

		[BiffString("PATH")]
		public string Path;

		[BiffInt("SIZE")]
		public int Size;

		[BiffByte("DATA")]
		public byte[] Data;

		private byte[] _rawData;

		public BinaryData(Resource res) : base(res.Name)
		{
			Data = res.Data;
		}

		public byte[] GetRawData()
		{
			if (_rawData != null) {
				return _rawData;
			}
			var img = Decode();
			_rawData = img == null ? null : MemoryMarshal.AsBytes(img.GetPixelSpan()).ToArray();

			return _rawData;
		}

		private Image<Rgba32> Decode()
		{
			using (var stream = new MemoryStream(Data)) {
				try {
					return Image.Load<Rgba32>(stream, new PngDecoder());

				} catch (Exception) {
					return null;
				}
			}
		}

		#region BIFF
		static BinaryData()
		{
			Init(typeof(BinaryData), Attributes);
		}

		public BinaryData(BinaryReader reader, string storageName) : base(storageName)
		{
			Load(this, reader, Attributes);
		}

		private static readonly Dictionary<string, List<BiffAttribute>> Attributes = new Dictionary<string, List<BiffAttribute>>();
		#endregion
	}
}
