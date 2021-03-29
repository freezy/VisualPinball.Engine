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

using System;
using System.Collections.Generic;
using NLog;
using UnityEngine;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	public class DisplayAuthoring : MonoBehaviour
	{
		public string Id = "dmd";
		public Color color = new Color(1, 0.18f, 0);

		public DisplayType DisplayType {
			get => _displayType;
			set {
				_displayType = value;
				UpdatePalette();
			}
		}

		private int _width;
		private int _height;
		private DisplayType _displayType;

		private Texture2D _texture;
		private readonly Dictionary<byte, Color> _map = new Dictionary<byte, Color>();

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
		private static readonly int ShaderDmdWidth = Shader.PropertyToID("_Width");
		private static readonly int ShaderDmdHeight = Shader.PropertyToID("_Height");

		public void UpdateDimensions(int width, int height)
		{
			_width = width;
			_height = height;
			_texture = new Texture2D(width, height);
			var material = GetComponent<Renderer>().sharedMaterial;
			material.mainTexture = _texture;
			material.SetFloat(ShaderDmdWidth, width);
			material.SetFloat(ShaderDmdHeight, height);
		}

		public void UpdateFrame(byte[] frame)
		{
			if (_texture == null) {
				Logger.Error($"Cannot render DMD for unknown size, UpdateDimensions() first!");
				return;
			}

			switch (_displayType) {
				case DisplayType.Dmd2PinMame:
				case DisplayType.Dmd2:
				case DisplayType.Dmd4:
				case DisplayType.Dmd8:
					if (frame.Length == _width * _height) {
						for (var y = 0; y < _height; y++) {
							for (var x = 0; x < _width; x++) {
								var pixel = frame[y * _width + x];
								_texture.SetPixel(_width - x, _height - y, _map.ContainsKey(pixel) ? _map[pixel] : Color.magenta);
							}
						}
						_texture.Apply();
					} else {
						Logger.Error($"Cannot render {frame.Length} bytes of frame data to {_width}x{_height}.");
					}
					break;

				case DisplayType.Dmd24:
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
				case DisplayType.Seg7:
				case DisplayType.Seg9:
				case DisplayType.Seg14:
				case DisplayType.Seg16:
				case DisplayType.Pixel:
					throw new NotImplementedException();
				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		public void UpdatePalette()
		{
			_map.Clear();
			var numColors = 0;
			switch (_displayType) {
				case DisplayType.Dmd2PinMame:
					_map.Add(0x0, Color.Lerp(Color.black, color, 0));
					_map.Add(0x14, Color.Lerp(Color.black, color, 0.33f));
					_map.Add(0x21, Color.Lerp(Color.black, color, 0.66f));
					_map.Add(0x43, Color.Lerp(Color.black, color, 1f));
					_map.Add(0x64, Color.Lerp(Color.black, color, 1f));
					break;

				case DisplayType.Dmd2:
					numColors = 4;
					break;
				case DisplayType.Dmd4:
					numColors = 16;
					break;
				case DisplayType.Dmd8:
					numColors = 256;
					break;
			}

			for (var i = 0; i < numColors; i++) {
				_map.Add((byte)i, Color.Lerp(Color.black, color, i * (1f / (numColors - 1))));
			}
		}
	}
}
