// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.

using System;
using System.IO;
using System.Text;
using NetVips;
using NLog;
using OpenMcdf;
using VisualPinball.Engine.Resources;

namespace VisualPinball.Engine.VPT
{
	public class Texture : Item<TextureData>
	{
		public override string ItemGroupName => "Textures";

		public static readonly Texture BallDebug = new Texture(Resource.BallDebug);
		public static readonly Texture BumperBase = new Texture(Resource.BumperBase);
		public static readonly Texture BumperCap = new Texture(Resource.BumperCap);
		public static readonly Texture BumperRing = new Texture(Resource.BumperRing);
		public static readonly Texture BumperSocket = new Texture(Resource.BumperSocket);

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public static readonly Texture[] LocalTextures = {
			BumperBase, BumperCap, BumperRing, BumperSocket, BallDebug
		};

		public int Width => Data.Width;
		public int Height => Data.Height;
		public bool IsHdr => (Data.Path?.EndsWith(".hdr", StringComparison.OrdinalIgnoreCase) ?? false)
		                  || (Data.Path?.EndsWith(".exr", StringComparison.OrdinalIgnoreCase) ?? false);
		public bool IsWebp => Data.Path?.EndsWith(".webp", StringComparison.OrdinalIgnoreCase) ?? false;

		public bool ConvertToPng => Data.Bitmap != null;

		public string FileExtension {
			get {
				if (Data.Path == null || ConvertToPng) {
					return ".png";
				}
				var ext = Path.GetExtension(Data.Path).ToLower();
				if (ext == ".jpeg") {
					ext = ".jpg";
				}
				return ext;
			}
		}

		/// <summary>
		/// Data as read from the .vpx file. Note that for bitmaps, it doesn't
		/// contain the header.
		/// </summary>
		/// <see cref="FileContent"/>
		public byte[] Content => ImageData?.Bytes ?? _screenshot;

		/// <summary>
		/// Data as it would written to an image file (incl headers).
		/// </summary>
		public byte[] FileContent => ImageData?.FileContent ?? _screenshot;

		private IImageData ImageData => Data.Binary as IImageData ?? Data.Bitmap;

		/// <summary>
		/// VPX can store texture data as screenshot in the TableInfo storage, this is it if it's the case.
		/// </summary>
		private byte[] _screenshot;

		public bool HasTransparentFormat => Data.HasBitmap || Data.Path != null && Data.Path.ToLower().EndsWith(".png");

		public bool UsageNormalMap;

		private TextureStats _stats;

		public Texture(string name) : base(new TextureData(name))
		{
			Name = name;
		}

		public Texture(TextureData data, CFStorage tableInfoStorage) : base(data)
		{
			if (data.Binary == null && data.Bitmap == null) {
				if (data.LinkId == 1) {
					if (tableInfoStorage == null) {
						Logger.Warn($"Texture {Name} has no binary data and no storage to load from.");
						return;
					}
					// load screenshot
					_screenshot = tableInfoStorage.GetStream("Screenshot")?.GetData();
				} else {
					Logger.Warn($"Could not load texture {Name} from storage. No binaries and link is {data.LinkId}.");
				}
			}
		}

		public Texture(BinaryReader reader, string itemName, CFStorage tableInfoStorage) : this(new TextureData(reader, itemName), tableInfoStorage) { }

		private Texture(Resource res) : this(new TextureData(res), null) { }

		public void Analyze()
		{
			if (!HasTransparentFormat) {
				_stats = new TextureStats(1, 0, 0);
				return;
			}
			using (var image = GetImage()) {
				_stats = image != null && image.HasAlpha()
					? AnalyzeAlpha()
					: new TextureStats(1, 0, 0);
			}
		}

		/// <summary>
		/// Returns statistics about transparent and translucent pixels in the
		/// texture.
		/// </summary>
		/// <returns>Statistics</returns>
		public TextureStats GetStats()
		{
			if (_stats == null) {
				Analyze();
			}
			return _stats;
		}

		/// <summary>
		/// Retrieves metrics on how many pixels are opaque (no alpha),
		/// translucent (some alpha), and transparent (100% alpha). <p/>
		///
		/// It uses the native libvips, so speed is decent.
		/// </summary>
		/// <returns></returns>
		private TextureStats AnalyzeAlpha()
		{
			using (var image = GetImage()) {

				// libvips couldn't load
				if (image == null) {
					return new TextureStats(Width * Height, 0, 0);
				}

				if (image.Bands < 4) {
					return new TextureStats(image.Width * image.Height, 0, 0);
				}

				// https://github.com/libvips/libvips/issues/1535
				var hist = image[3].HistFind();
				var total = image.Width * image.Height;
				var alpha0 = BandStats(hist, 0);
				var alpha1 = BandStats(hist, 254);

				var opaque = (int)alpha1;
				var translucent = (int)(alpha0 - alpha1);
				var transparent = (int)(total - alpha0);

				return new TextureStats(opaque, translucent, transparent);
			}
		}

		public Image GetImage()
		{
			try {
				var data = Data.Binary != null ? Data.Binary.Data : Data.Bitmap != null ? Data.Bitmap.Bytes : _screenshot;
				if (data.Length == 0) {
					throw new InvalidDataException("Image data is empty.");
				}
				return Data.Binary != null || _screenshot != null
					? Image.NewFromBuffer(data)
					: Image.NewFromMemory(data, Width, Height, 4, Enums.BandFormat.Uchar);

			} catch (Exception e) {
				Logger.Warn(e, "Error reading {0} ({1}) with libvips.", Name, Path.GetFileName(Data.Path));
			}

			return null;
		}

		private static double BandStats(Image hist, int val)
		{
			var mask = (Image.Identity() > val) / 255;
			return (hist * mask).Avg() * 256;
		}

		public override string ToString()
		{
			var sb = new StringBuilder();

			sb.AppendLine($"{Name}");
			sb.AppendLine($"    Resolution  {Width}x{Height}");
			sb.AppendLine($"    Extension   {FileExtension}");
			if (_stats == null) {
				sb.AppendLine("    Stats       none");
			} else {
				sb.AppendLine("    Stats:");
				sb.AppendLine($"       Opaque      {_stats.Opaque}");
				sb.AppendLine($"       Translucent {_stats.Translucent}");
				sb.AppendLine($"       Transparent {_stats.Transparent}");
			}

			return sb.ToString();
		}
	}

	public class TextureStats
	{
		/// <summary>
		/// How many opaque pixels found relative to total number of pixels
		/// </summary>
		public float Opaque => (float) _numOpaquePixels / _numTotalPixels;

		/// <summary>
		/// How many translucent pixels found relative to total number of pixels
		/// </summary>
		public float Translucent => (float) _numTranslucentPixels / _numTotalPixels;

		/// <summary>
		/// How many transparent pixels found relative to total number of pixels
		/// </summary>
		public float Transparent => (float) _numTransparentPixels / _numTotalPixels;

		public bool HasTransparentPixels => _numTransparentPixels > 0;
		public bool IsOpaque => _numTranslucentPixels == 0 && _numTransparentPixels == 0;

		private readonly int _numOpaquePixels;
		private readonly int _numTranslucentPixels;
		private readonly int _numTransparentPixels;
		private readonly int _numTotalPixels;

		public TextureStats(int numOpaquePixels, int numTranslucentPixels, int numTransparentPixels)
		{
			_numOpaquePixels = numOpaquePixels;
			_numTranslucentPixels = numTranslucentPixels;
			_numTransparentPixels = numTransparentPixels;
			_numTotalPixels = numOpaquePixels + numTranslucentPixels + numTransparentPixels;
		}
	}

	public interface IImageData
	{
		byte[] Bytes { get; }

		byte[] FileContent { get; }
	}
}
