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
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Display/Dot Matrix Display")]
	public class DotMatrixDisplayAuthoring : DisplayAuthoring
	{
		public override string Id { get => _id; set => _id = value; }
		public override float AspectRatio {
			get => (float)Width / Height;
			set => Width = (int)(Height * value);
		}

		protected override float MeshWidth => AspectRatio * MeshHeight;
		public override float MeshHeight => 0.4f;
		protected override float MeshDepth => 0.01f;

		[SerializeField] private string _id = "dmd0";
		[SerializeField] private Color _litColor = new Color(1, 0.18f, 0);
		[SerializeField] private Color _unlitColor = new Color(0.2f, 0.2f, 0.2f);
		[SerializeField] private int _width = 128;
		[SerializeField] private int _height = 32;
		[SerializeField] private float _padding = 0.2f;
		[SerializeField] private float _roundness = 0.35f;

		[NonSerialized] private DisplayFrameFormat _frameFormat = DisplayFrameFormat.Dmd4;
		[NonSerialized] private Color32[] _colorBuffer;

		private readonly Dictionary<DisplayFrameFormat, Dictionary<byte, Color32>> _map = new Dictionary<DisplayFrameFormat, Dictionary<byte, Color32>>();

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
		private static readonly int UnlitColorProp = Shader.PropertyToID("__UnlitColor");
		private static readonly int DimensionsProp = Shader.PropertyToID("__Dimensions");
		private static readonly int PaddingProp = Shader.PropertyToID("__Padding");
		private static readonly int RoundnessProp = Shader.PropertyToID("__Roundness");

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

		public override Color LitColor
		{
			get => _litColor;
			set {
				_litColor = value;
				UpdatePalette(_frameFormat);
			}
		}

		public override Color UnlitColor
		{
			get => _unlitColor;
			set {
				_unlitColor = value;
				var mr = gameObject.GetComponent<MeshRenderer>();
				if (mr != null) {
					mr.sharedMaterial.SetColor(UnlitColorProp, value);
				}
			}
		}

		public float Padding
		{
			get => _padding;
			set {
				_padding = value;
				var mr = gameObject.GetComponent<MeshRenderer>();
				if (mr != null) {
					mr.sharedMaterial.SetFloat(PaddingProp, value);
				}
			}
		}

		public float Roundness
		{
			get => _roundness;
			set {
				_roundness = value;
				var mr = gameObject.GetComponent<MeshRenderer>();
				if (mr != null) {
					mr.sharedMaterial.SetFloat(RoundnessProp, value);
				}
			}
		}

		public override void UpdateDimensions(int width, int height, bool flipX = false)
		{
			Logger.Info($"Updating dimensions for DMD \"{_id}\" to {width}x{height}.");
			_width = width;
			_height = height;
			_colorBuffer = new Color32[width * height];
			_texture = new Texture2D(width, height, TextureFormat.RGB24, false);
			_texture.SetPixels32(_colorBuffer);
			_texture.Apply();

			RegenerateMesh(flipX);

			var mr = gameObject.GetComponent<MeshRenderer>();
			mr.sharedMaterial.SetVector(DimensionsProp, new Vector4(_width, _height));
		}

		public override void UpdateColor(Color color)
		{
			base.UpdateColor(color);
			_map.Clear();
		}

		public override void Clear()
		{
			UpdateFrame(DisplayFrameFormat.Dmd2, new byte[_width * _height]);
		}

		protected override Material CreateMaterial()
		{
			var material = Instantiate(RenderPipeline.Current.MaterialConverter.DotMatrixDisplay);
			material.mainTexture = _texture;
			material.SetVector(DimensionsProp, new Vector4(_width, _height));
			material.SetColor(UnlitColorProp, _unlitColor);
			material.SetFloat(PaddingProp, _padding);
			return material;
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
						_frameFormat = format;
					}
					var map = _map[format];
					if (frame.Length == _width * _height) {
						for (var y = 0; y < _height; y++) {
							for (var x = 0; x < _width; x++) {
								var pixel = frame[(_height - y - 1) * _width + x];
								_colorBuffer[y * _width + x] = map.ContainsKey(pixel) ? map[pixel] : (Color32)Color.magenta;
							}
						}
						_texture.SetPixels32(_colorBuffer);
						_texture.Apply();

					} else {
						Logger.Error($"Cannot render {frame.Length} bytes of frame data to {_width}x{_height}.");
					}
					break;

				case DisplayFrameFormat.Dmd24:
					if (frame.Length == _width * _height * 3) {

						// the texture has RGB24 format, so we can just copy all into the texture directly.
						CopyData(frame, 0, frame.Length, _texture.GetRawTextureData<byte>());

						// still need to apply it.
						_texture.Apply();

					} else {
						Logger.Error($"Cannot render {frame.Length} bytes of RGB data to {_width}x{_height}.");
					}
					break;

				case DisplayFrameFormat.Segment:
					Logger.Error("This is a DMD component that cannot render segment data. Use a segment component!");
					break;

				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		private static unsafe void CopyData<T>(T[] array, int offset, int count, NativeArray<T> dst) where T : unmanaged
		{
			fixed (T * srcPtr = array) {
				var dstPtr = dst.GetUnsafePtr();
				UnsafeUtility.MemCpy(dstPtr,srcPtr + offset, sizeof(T) * count);
			}
		}

		private void UpdatePalette(DisplayFrameFormat format)
		{
			if (!_map.ContainsKey(format)) {
				_map[format] = new Dictionary<byte, Color32>();
			} else {
				_map[format].Clear();
			}
			Logger.Info($"Regenerating palette for format {format} and color {LitColor}.");

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
				_map[format].Add((byte)i, Color.Lerp(Color.black, _litColor, i * (1f / (numColors - 1))));
			}
		}
	}
}
