using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using NetVips;
using NLog;
using VisualPinball.Engine.IO;
using VisualPinball.Engine.Resources;

namespace VisualPinball.Engine.VPT
{
	public class Texture : Item<TextureData>
	{
		public static readonly Texture BumperBase = new Texture(Resource.BumperBase);
		public static readonly Texture BumperCap = new Texture(Resource.BumperCap);
		public static readonly Texture BumperRing = new Texture(Resource.BumperRing);
		public static readonly Texture BumperSocket = new Texture(Resource.BumperSocket);

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public static readonly Texture[] LocalTextures = {
			BumperBase, BumperCap, BumperRing, BumperSocket
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

		/// <summary>
		/// Returns true if at least one pixel is not opaque. <p/>
		///
		/// Loops through the bitmap if necessary, but only the first time.
		/// </summary>
		public bool HasTransparentPixels;

		public bool HasTransparentFormat => Data.Bitmap != null || Data.Path != null && Data.Path.ToLower().EndsWith(".png");

		public bool UsageNormalMap;

		public bool UsageOpaqueMaterial;

		private TextureStats _stats;

		public Texture(BinaryReader reader, string itemName) : base(new TextureData(reader, itemName)) { }

		private Texture(Resource res) : base(new TextureData(res)) { }

		public void Analyze()
		{
			using (var image = GetImage()) {
				HasTransparentPixels = image != null && image.HasAlpha();
				// if (UsageOpaqueMaterial && HasTransparentPixels) {
					var sw = new Stopwatch();
					sw.Start();
					_stats = AnalyzeAlpha(1, 254);
					sw.Stop();
				// 	Logger.Warn("Texture {0} is used by at least one opaque material but has transparent pixels. Analyzed in {1}ms to determine whether to render as cut-out or transparent.", Name, sw.ElapsedMilliseconds);
				// }
			}
		}

		private Image GetImage()
		{
			try {
				return Data.Binary != null
					? Image.NewFromBuffer(Data.Binary.Data)
					: Image.NewFromMemory(Data.Bitmap.Bytes, Width, Height, 4, Enums.BandFormat.Uchar);

			} catch (VipsException e) {
				Logger.Warn(e, "Error reading {0} ({1}) with libvips.", Name, Path.GetFileName(Data.Path));
			}

			return null;
		}

		/// <summary>
		/// Returns statistics about transparent and translucent pixels in the
		/// texture.
		/// </summary>
		/// <param name="threshold">How many transparent or translucent pixels to count before aborting</param>
		/// <returns>Statistics</returns>
		public TextureStats GetStats()
		{
			if (_stats == null && (!HasTransparentFormat || !HasTransparentPixels)) {
				_stats = new TextureStats(1, 0, 0);
			}

			if (_stats == null) {
				throw new InvalidOperationException("Unknown stats. Please pre-compute in parallel.");
			}

			return _stats;
		}

		/// <summary>
		/// Retrieves metrics on how many pixels are opaque (no alpha),
		/// translucent (some alpha), and transparent (100% alpha). <p/>
		///
		/// It loops through the image through blocks, progressing rapidly
		/// through the image, in order to get a good average fast.
		/// </summary>
		/// <param name="data">Bitmap data, as RGBA</param>
		/// <param name="threshold">How many transparent or translucent pixels to count before aborting</param>
		/// <param name="numBlocks">In how many blocks the loop is divided</param>
		/// <returns></returns>
		private TextureStats AnalyzeAlpha(int translucentStart, int translucentEnd)
		{
			Profiler.Start("AnalyzeAlpha");
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

				Profiler.Stop("AnalyzeAlpha");
				return new TextureStats(opaque, translucent, transparent);
			}
		}

		private static double BandStats(Image hist, int val)
		{
			var mask = (Image.Identity() > val) / 255;
			return (hist * mask).Avg() * 256;
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

		/// <summary>
		/// True if no translucent or transparent pixels found, false otherwise.
		/// </summary>
		public bool IsOpaque => _numTranslucentPixels == 0 && _numTranslucentPixels == 0;

		public bool IsEmpty => _numTotalPixels == 0;

		private readonly int _numOpaquePixels;
		private readonly int _numTranslucentPixels;
		private readonly int _numTransparentPixels;
		private readonly int _numTotalPixels;

		public TextureStats()
		{
		}

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

		//byte[] GetRawData();
	}
}
