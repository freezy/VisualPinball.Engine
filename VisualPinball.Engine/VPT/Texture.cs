using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
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
		public int Height => Data.Width;

		/// <summary>
		/// Data as read from the .vpx file. Note that for bitmaps, it doesn't
		/// contain the header.
		/// </summary>
		/// <see cref="FileContent"/>
		public byte[] Content => GetBinaryData().Bytes;

		/// <summary>
		/// Data as it would written to an image file (incl headers).
		/// </summary>
		public byte[] FileContent => GetBinaryData().FileContent;

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
			if (Data.Path == null || !Data.Path.ToLower().EndsWith(".png")) {
				return null;
			}
			var img = Decode();
			if (img == null) {
				return null;
			}
			var data = MemoryMarshal.AsBytes(img.GetPixelSpan()).ToArray();
			return Analyze(data, img.Width, img.Height, threshold);
		}

		/// <summary>
		/// Retrieves metrics on how many pixels are opaque (no alpha),
		/// translucent (some alpha), and transparent (100% alpha). <p/>
		///
		/// It loops through the image through blocks, progressing rapidly
		/// through the image, in order to get a good average fast.
		/// </summary>
		/// <param name="data">Bitmap data, as RGBA</param>
		/// <param name="width">Width of the image</param>
		/// <param name="height">Height of the image</param>
		/// <param name="threshold">How many transparent or translucent pixels to count before aborting</param>
		/// <param name="numBlocks">In how many blocks the loop is divided</param>
		/// <returns></returns>
		private static TextureStats Analyze(IReadOnlyList<byte> data, int width, int height, int threshold, int numBlocks = 10)
		{
			var opaque = 0;
			var translucent = 0;
			var transparent = 0;
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

		private Image<Rgba32> Decode()
		{
			if (Data.Binary == null) {
				return null;
			}

			using (var stream = new MemoryStream(Data.Binary.Data)) {
				try {
					return Image.Load<Rgba32>(stream, new PngDecoder());

				} catch (Exception) {
					return null;
				}
			}
		}

		private IBinaryData GetBinaryData()
		{
			return Data.Binary as IBinaryData ?? Data.Bitmap;
		}
	}

	public class TextureStats
	{
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
}
