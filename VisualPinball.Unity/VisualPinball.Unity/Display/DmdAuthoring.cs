// Visual Pinball Engine
// Copyright (C) 2021 freezy and VPE Team
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

// ReSharper disable InconsistentNaming
// ReSharper disable CheckNamespace

using System;
using System.Collections.Generic;
using NLog;
using UnityEngine;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Display/Dot Matrix Display")]
	public class DmdAuthoring : DisplayAuthoring
	{
		public override string Id { get => _id; set => _id = value; }
		public override Color Color { get; set; } = new Color(1, 0.18f, 0);

		[SerializeField]
		private string _id = "display0";
		[SerializeField]
		private int _width = 128;
		[SerializeField]
		private int _height = 32;

		protected override string ShaderName => "Visual Pinball/DMD Shader";
		protected override float MeshWidth => (float)Width / Height * MeshHeight;
		protected override float MeshHeight => 0.4f;
		protected override float MeshDepth => 0.01f;

		private readonly Dictionary<DisplayFrameFormat, Dictionary<byte, Color>> _map = new Dictionary<DisplayFrameFormat, Dictionary<byte, Color>>();

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
		private static readonly int ShaderDmdWidth = Shader.PropertyToID("_Width");
		private static readonly int ShaderDmdHeight = Shader.PropertyToID("_Height");

		public int Width
		{
			get => _width;
			set => UpdateDimensions(value, _height);
		}

		public int Height
		{
			get => _height;
			set => UpdateDimensions(_width, value);
		}

		public override void UpdateDimensions(int width, int height)
		{
			_width = width;
			_height = height;
			_texture = new Texture2D(width, height);
			var mr = GetComponent<MeshRenderer>();
			if (mr != null) {
				mr.material.mainTexture = _texture;
				mr.material.SetFloat(ShaderDmdWidth, width);
				mr.material.SetFloat(ShaderDmdHeight, height);
			}

			RegenerateMesh();
		}

		public override void UpdateColor(Color color)
		{
			base.UpdateColor(color);
			_map.Clear();
		}


		protected override void InitMaterial(Material material)
		{
		}

		public override void UpdateFrame(DisplayFrameFormat format, byte[] frame)
		{
			if (_texture == null) {
				Logger.Error("Cannot render DMD for unknown size, UpdateDimensions() first!");
				return;
			}

			switch (format) {
				case DisplayFrameFormat.Dmd2:
				case DisplayFrameFormat.Dmd4:
				case DisplayFrameFormat.Dmd8:
					if (!_map.ContainsKey(format)) {
						UpdatePalette(format);
					}
					var map = _map[format];
					if (frame.Length == _width * _height) {
						for (var y = 0; y < _height; y++) {
							for (var x = 0; x < _width; x++) {
								var pixel = frame[y * _width + x];
								_texture.SetPixel(x, y, map.ContainsKey(pixel) ? map[pixel] : Color.magenta);
							}
						}
						_texture.Apply();

					} else {
						Logger.Error($"Cannot render {frame.Length} bytes of frame data to {_width}x{_height}.");
					}
					break;

				case DisplayFrameFormat.Dmd24:
					if (frame.Length == _width * _height * 3) {
						for (var y = 0; y < _height; y++) {
							for (var x = 0; x < _width; x++) {
								var pos = (y * _width + x) * 3;
								_texture.SetPixel(_width - x, _height - y, new Color(frame[pos] / 255f, frame[pos + 1] / 255f, frame[pos + 2] / 255f));
							}
						}
						_texture.Apply();
					} else {
						Logger.Error($"Cannot render {frame.Length} bytes of RGB data to {_width}x{_height}.");
					}
					break;

				case DisplayFrameFormat.Segment16:
					Logger.Error($"This is a DMD component that cannot render segment data. Use a segment component!");
					break;

				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		private void UpdatePalette(DisplayFrameFormat format)
		{
			if (!_map.ContainsKey(format)) {
				_map[format] = new Dictionary<byte, Color>();
			} else {
				_map[format].Clear();
			}
			Logger.Info($"Regenerating palette for format {format} and color {Color}.");

			var numColors = 0;
			switch (format) {
				case DisplayFrameFormat.Dmd2:
					numColors = 4;
					break;

				case DisplayFrameFormat.Dmd4:
					numColors = 16;
					break;

				case DisplayFrameFormat.Dmd8:
					numColors = 256;
					break;

				case DisplayFrameFormat.Dmd24:
					// no palette to handle here
					break;

				default:
					throw new ArgumentOutOfRangeException(nameof(format), format, null);
			}

			for (var i = 0; i < numColors; i++) {
				_map[format].Add((byte)i, Color.Lerp(Color.black, Color, i * (1f / (numColors - 1))));
			}
		}
	}
}
