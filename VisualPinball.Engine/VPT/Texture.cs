using System.Collections.Generic;
using System.IO;
using VisualPinball.Engine.Resources;

namespace VisualPinball.Engine.VPT
{
	public class Texture : Item<TextureData>
	{
		public static readonly Texture BumperBase = new Texture(Resource.BumperBase);
		public static readonly Texture BumperCap = new Texture(Resource.BumperCap);
		public static readonly Texture BumperRing = new Texture(Resource.BumperRing);
		public static readonly Texture BumperSocket = new Texture(Resource.BumperSocket);

		public static readonly Texture[] LocalTextures = {
			BumperBase, BumperCap, BumperRing, BumperSocket
		};

		public int Width => Data.Width;
		public int Height => Data.Height;
		public bool IsHdr => (Data.Path?.ToLower().EndsWith(".hdr") ?? false) || (Data.Path?.ToLower().EndsWith(".exr") ?? false);

		/// <summary>
		/// Data as read from the .vpx file. Note that for bitmaps, it doesn't
		/// contain the header.
		/// </summary>
		/// <see cref="FileContent"/>
		public byte[] Content => Image.Bytes;

		/// <summary>
		/// Data as it would written to an image file (incl headers).
		/// </summary>
		public byte[] FileContent => Image.FileContent;

		private IImageData Image => Data.Binary as IImageData ?? Data.Bitmap;

		/// <summary>
		/// Returns true if at least one pixel is not opaque. <p/>
		///
		/// Loops through the bitmap if necessary, but only the first time.
		/// </summary>
		public bool HasTransparentPixels {
			get {
				if (!HasTransparentFormat) {
					return false;
				}

				if (_lastStats != null) {
					return !_lastStats.IsOpaque;
				}

				if (_hasTransparentPixels == null) {
					_hasTransparentPixels = FindTransparentPixel(Image.GetRawData());
				}

				return (bool) _hasTransparentPixels;
			}
		}

		public bool HasTransparentFormat => Data.Bitmap != null || Data.Path != null && Data.Path.ToLower().EndsWith(".png");

		private TextureStats _lastStats;
		private bool? _hasTransparentPixels;

		public Texture(BinaryReader reader, string itemName) : base(new TextureData(reader, itemName)) { }

		private Texture(Resource res) : base(new TextureData(res)) { }

		/// <summary>
		/// Returns statistics about transparent and translucent pixels in the
		/// texture.
		/// </summary>
		/// <param name="threshold">How many transparent or translucent pixels to count before aborting</param>
		/// <returns>Statistics</returns>
		public TextureStats GetStats(int threshold)
		{
			if (!HasTransparentFormat) {
				return null;
			}

			_lastStats = Analyze(Image.GetRawData(), threshold);
			return _lastStats;
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
		private TextureStats Analyze(IReadOnlyList<byte> data, int threshold, int numBlocks = 10)
		{
			var opaque = 0;
			var translucent = 0;
			var transparent = 0;
			var width = Width;
			var height = Height;
			var dx = (int)System.Math.Ceiling((float)width / numBlocks);
			var dy = (int)System.Math.Ceiling((float)height / numBlocks);
			for (var yy= 0; yy < dy; yy ++) {
				for (var xx = 0; xx < dx; xx++) {
					for (var y = 0; y < height; y += dy) {
						var posY = y + yy;
						if (posY >= height) {
							break;
						}
						for (var x = 0; x < width; x += dx) {
							var posX = x + xx;
							if (posX >= width) {
								break;
							}
							var a = data[posY * 4 * width + posX * 4 + 3];
							switch (a) {
								case 0x0: transparent++; break;
								case 0xff: opaque++; break;
								default: translucent++; break;
							}

							if (translucent + transparent > threshold) {
								return new TextureStats(opaque, translucent, transparent);
							}
						}
					}
				}
			}
			return new TextureStats(opaque, translucent, transparent);
		}

		/// <summary>
		/// Loops intelligently through all pixels and breaks at the first
		/// non-opaque pixel.<p/>
		///
		/// The loop is brute force to default maximum steps required to check
		/// every pixel within the loop a second guessing approximation index
		/// is used. It tries to look ahead in larger steps to find a hit while
		/// brute force continues if this index becomes greater than the array
		/// length, the approximationStepDistance is scaled.
		/// So the guessing starts wildly for a chance of a early out hit , but
		/// if not successful, it keeps guessing , but refines the distance.
		/// When the guessing restarts it does not recheck the pixel already
		/// check via brute force , so it always starts at the
		/// bruteForceIndex + 2, + 2 and not + 1 is to avoid a overlap on the
		/// loop after setting approximationIndex;
		///
		/// using 254 instead of 255, just to count out miniscule hits or even precision errors
		/// </summary>
		/// <param name="data"></param>
		/// <returns></returns>
		private bool FindTransparentPixel(IReadOnlyList<byte> data)
		{
			var width = Width;
			var height = Height;
			var numPixels = width * height;
			var approximationIndex = 0;
			var approximationStepDistance = (int)((float)numPixels / 10); // this is how many pixels the approximationIndex is incremented by
			const float approximationStepDistanceScalar = 0.8f;
			var mustCalculateApproximationIndexStartValue = true;

			for (var i = 0; i < numPixels; i++) {
				if (data[i * 4 + 3] < 254) {
					return true;
				}

				// approximation
				if (mustCalculateApproximationIndexStartValue) {
					approximationIndex = System.Math.Min(numPixels - 1, i + 2);
					mustCalculateApproximationIndexStartValue = false;
				}

				if (data[approximationIndex * 4 + 3] < 254) {
					return true;
				}
				approximationIndex += approximationStepDistance;
				//need to see if the index of approximation is larger than array
				//if so then refine the guessing distance and start again at new approximationIndex;
				if (approximationIndex >= numPixels) {
					mustCalculateApproximationIndexStartValue = true;
					approximationStepDistance = (int)(approximationStepDistance * approximationStepDistanceScalar);
				}
			}
			return false;
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

		byte[] GetRawData();
	}
}
