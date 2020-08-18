using System;
using System.IO;
using System.Text;
using NetVips;
using NLog;
using VisualPinball.Resources;

namespace VisualPinball.Engine.VPT
{
	public class Texture : Item<TextureData>
	{
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
		public bool IsHdr => (Data.Path?.ToLower().EndsWith(".hdr") ?? false) || (Data.Path?.ToLower().EndsWith(".exr") ?? false);

		public string FileExtension {
			get {
				if (Data.Path == null) {
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
		public byte[] Content => ImageData.Bytes;

		/// <summary>
		/// Data as it would written to an image file (incl headers).
		/// </summary>
		public byte[] FileContent => ImageData.FileContent;

		private IImageData ImageData => Data.Binary as IImageData ?? Data.Bitmap;

		public bool HasTransparentFormat => Data.HasBitmap || Data.Path != null && Data.Path.ToLower().EndsWith(".png");

		public bool UsageNormalMap;

		private TextureStats _stats;

		public Texture(string name) : base(new TextureData(name))
		{
			Name = name;
		}

		public Texture(TextureData data) : base(data) { }

		public Texture(BinaryReader reader, string itemName) : this(new TextureData(reader, itemName)) { }

		private Texture(Resource res) : this(new TextureData(res)) { }

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

		private Image GetImage()
		{
			try {
				return Data.Binary != null
					? Image.NewFromBuffer(Data.Binary.Data)
					: Image.NewFromMemory(Data.Bitmap.Bytes, Width, Height, 4, Enums.BandFormat.Uchar);

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
