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
using Unity.Mathematics;
using UnityEngine;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	public class SegDisplayAuthoring : DisplayAuthoring
	{
		public override string Id { get => _id; set => _id = value;}

		public float AspectRatio = 0.75f;

		private const int NumSegments = 15;

		protected override string ShaderName => "Visual Pinball/Alphanumeric Shader";
		protected override float MeshWidth => NumChars * MeshHeight * AspectRatio;
		protected override float MeshHeight => 0.2f;
		protected override float MeshDepth => 0.01f;

		[SerializeField]
		private string _id = "display0";
		private bool _isInitialized;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		#region Shader Prop Constants

		private static readonly int NumCharsProp = Shader.PropertyToID("_NumChars");
		private static readonly int NumLinesProp = Shader.PropertyToID("_NumLines");
		private static readonly int NumSegmentsProp = Shader.PropertyToID("_NumSegments");
		private static readonly int TargetWidthProp = Shader.PropertyToID("_TargetWidth");
		private static readonly int TargetHeightProp = Shader.PropertyToID("_TargetHeight");
		private static readonly int SegmentWidthProp = Shader.PropertyToID("_SegmentWidth");
		private static readonly int SkewAngleProp = Shader.PropertyToID("_SkewAngle");
		private static readonly int OuterPaddingX = Shader.PropertyToID("_OuterPaddingX");
		private static readonly int OuterPaddingY = Shader.PropertyToID("_OuterPaddingY");
		private static readonly int InnerPaddingX = Shader.PropertyToID("_InnerPaddingX");
		private static readonly int InnerPaddingY = Shader.PropertyToID("_InnerPaddingY");
		private static readonly int ColorProp = Shader.PropertyToID("_Color");
		private static readonly int SegmentTypeProp = Shader.PropertyToID("_SegmentType");

		#endregion

		#region Shader Props

		public int NumChars {
			get {
				var mr = gameObject.GetComponent<MeshRenderer>();
				return mr != null ? (int)mr.material.GetFloat(NumCharsProp) : 8;
			}
			set {
				var mr = gameObject.GetComponent<MeshRenderer>();
				if (mr) {
					mr.material.SetFloat(NumCharsProp, value);
				}
				RegenerateMesh();
			}
		}

		public int NumLines {
			get {
				var mr = gameObject.GetComponent<MeshRenderer>();
				return mr != null ? (int)mr.material.GetFloat(NumLinesProp) : 1;
			}
			set {
				var mr = gameObject.GetComponent<MeshRenderer>();
				if (mr) {
					mr.material.SetFloat(NumLinesProp, value);
				}
				RegenerateMesh();
			}
		}

		public override Color Color {
			get {
				var mr = gameObject.GetComponent<MeshRenderer>();
				return mr != null ? mr.material.GetColor(ColorProp) : Color.yellow;
			}
			set {
				var mr = gameObject.GetComponent<MeshRenderer>();
				if (mr) {
					mr.material.SetColor(ColorProp, value);
				}
			}
		}

		public float SkewAngle {
			get {
				var mr = gameObject.GetComponent<MeshRenderer>();
				if (mr != null) {
					return mr.material.GetFloat(SkewAngleProp);
				}
				return 0;
			}
			set {
				var mr = gameObject.GetComponent<MeshRenderer>();
				if (mr != null) {
					mr.material.SetFloat(SkewAngleProp, value);
				}
			}
		}

		public float SegmentWidth {
			get {
				var mr = gameObject.GetComponent<MeshRenderer>();
				if (mr != null) {
					return mr.material.GetFloat(SegmentWidthProp);
				}
				return 0;
			}
			set {
				var mr = gameObject.GetComponent<MeshRenderer>();
				if (mr != null) {
					mr.material.SetFloat(SegmentWidthProp, value);
				}
			}
		}

		public float2 OuterPadding {
			get {
				var mr = gameObject.GetComponent<MeshRenderer>();
				if (mr != null) {
					return new float2(
						mr.material.GetFloat(OuterPaddingX),
						mr.material.GetFloat(OuterPaddingY)
					);
				}
				return float2.zero;
			}
			set {
				var mr = gameObject.GetComponent<MeshRenderer>();
				if (mr != null) {
					mr.material.SetFloat(OuterPaddingX, value.x);
					mr.material.SetFloat(OuterPaddingY, value.y);
				}
			}
		}

		public float2 InnerPadding {
			get {
				var mr = gameObject.GetComponent<MeshRenderer>();
				if (mr != null) {
					return new float2(
						mr.material.GetFloat(InnerPaddingX),
						mr.material.GetFloat(InnerPaddingY)
					);
				}
				return float2.zero;
			}
			set {
				var mr = gameObject.GetComponent<MeshRenderer>();
				if (mr != null) {
					mr.material.SetFloat(InnerPaddingX, value.x);
					mr.material.SetFloat(InnerPaddingY, value.y);
				}
			}
		}

		#endregion

		public override void UpdateFrame(DisplayFrameFormat format, byte[] source)
		{
			if (!_isInitialized) {
				_isInitialized = true;
				switch (format) {
					case DisplayFrameFormat.Segment16:
						var mr = gameObject.GetComponent<MeshRenderer>();
						mr.material.SetFloat(SegmentTypeProp, 0);
						break;
					default:
						Logger.Error($"Invalid data format, must be segment data, got {format}.");
						return;
				}
			}

			var target = new ushort[source.Length / 2];
			Buffer.BlockCopy(source, 0, target, 0, source.Length);
			UpdateFrame(target);
		}

		public override void UpdateDimensions(int width, int height)
		{
			_texture = new Texture2D(NumSegments, width * height);
			var mr = gameObject.GetComponent<MeshRenderer>();
			if (mr) {
				mr.material.mainTexture = _texture;
				mr.material.SetFloat(NumCharsProp, width);
				mr.material.SetFloat(NumLinesProp, height);
			}
			RegenerateMesh();
		}

		protected override void InitMaterial(Material material)
		{
			var length = NumChars * MeshHeight * AspectRatio;
			material.SetFloat(NumCharsProp, NumChars);
			material.SetFloat(NumSegmentsProp, NumSegments);
			material.SetFloat(TargetWidthProp, length * 100);
			material.SetFloat(TargetHeightProp, MeshHeight * 100);
		}

		public void SetText(string text)
		{
			var source = GenerateAlphaNumeric(text);
			var target = new byte[source.Length * 2];
			Buffer.BlockCopy(source, 0, target, 0, source.Length * 2);

			UpdateDimensions(math.max(1, text.Length), 1);
			//UpdateFrame(DisplayFrameFormat.Segment, target);
			UpdateFrame(source);
		}

		public void SetTestData()
		{
			var data = new ushort[] {
				0x7fff, 0x7fff, 0, 1, 2, 4, 8, 16, 32, 64, 128, 256, 512, 1024, 2048, 4096, 8192, 16384, 0x7fff, 0x7fff
			};
			UpdateDimensions(data.Length, 1);
			UpdateFrame(data);
		}

		private void UpdateFrame(IReadOnlyList<ushort> data)
		{
			var height = NumChars;
			for (var y = 0; y < height; y++) {
				for (var x = 0; x < NumSegments; x++) {
					var seg = data[y] >> x & 0x1;
					var val = seg == 1 ? 1f : 0f;
					_texture.SetPixel(x, y, new Color(val, val, val, 1));
				}
			}
			_texture.Apply();
		}

		private static ushort[] GenerateAlphaNumeric(string text)
		{
			var data = new ushort[text.Length];
			for (var i = 0; i < text.Length; i++) {
				if (AlphaNumericMap.ContainsKey(text[i])) {
					data[i] = AlphaNumericMap[text[i]];
				} else {
					data[i] = AlphaNumericMap[' '];
				}
			}
			return data;
		}

		private static readonly Dictionary<char, ushort> AlphaNumericMap = new Dictionary<char, ushort> {
			{ '0', 0x443f },
			{ '1', 0x406 },
			{ '2', 0x85b },
			{ '3', 0x80f },
			{ '4', 0x866 },
			{ '5', 0x1069 },
			{ '6', 0x87d },
			{ '7', 0x7 },
			{ '8', 0x87f },
			{ '9', 0x86f },
			{ ' ', 0x0 },
			{ '!', 0x86 },
			{ '"', 0x202 },
			{ '#', 0x2a4e },
			{ '$', 0x2a6d },
			{ '%', 0x7f64 },
			{ '&', 0x1359 },
			{ '\'', 0x200 },
			{ '(', 0x1400 },
			{ ')', 0x4100 },
			{ '*', 0x7f40 },
			{ '+', 0x2a40 },
			{ ',', 0x4000 },
			{ '-', 0x840 },
			{ '.', 0x80 },
			{ '/', 0x4400 },
			{ ':', 0x2200 },
			{ ';', 0x4200 },
			{ '<', 0x1440 },
			{ '=', 0x848 },
			{ '>', 0x4900 },
			{ '?', 0x2883 },
			{ '@', 0xa3b },
			{ 'A', 0x877 },
			{ 'B', 0x2a0f },
			{ 'C', 0x39 },
			{ 'D', 0x220f },
			{ 'E', 0x79 },
			{ 'F', 0x71 },
			{ 'G', 0x83d },
			{ 'H', 0x876 },
			{ 'I', 0x2209 },
			{ 'J', 0x1e },
			{ 'K', 0x1470 },
			{ 'L', 0x38 },
			{ 'M', 0x536 },
			{ 'N', 0x1136 },
			{ 'O', 0x3f },
			{ 'P', 0x873 },
			{ 'Q', 0x103f },
			{ 'R', 0x1873 },
			{ 'S', 0x86d },
			{ 'T', 0x2201 },
			{ 'U', 0x3e },
			{ 'V', 0x4430 },
			{ 'W', 0x5036 },
			{ 'X', 0x5500 },
			{ 'Y', 0x86e },
			{ 'Z', 0x4409 },
			{ '[', 0x39 },
			{ '\\', 0x1100 },
			{ ']', 0xf },
			{ '^', 0x5000 },
			{ '_', 0x8 },
			{ '`', 0x100 },
			{ 'a', 0x2058 },
			{ 'b', 0x1078 },
			{ 'c', 0x858 },
			{ 'd', 0x480e },
			{ 'e', 0x4058 },
			{ 'f', 0x2c40 },
			{ 'g', 0xc0e },
			{ 'h', 0x2070 },
			{ 'i', 0x2000 },
			{ 'j', 0x4210 },
			{ 'k', 0x3600 },
			{ 'l', 0x30 },
			{ 'm', 0x2854 },
			{ 'n', 0x2050 },
			{ 'o', 0x85c },
			{ 'p', 0x170 },
			{ 'q', 0xc06 },
			{ 'r', 0x50 },
			{ 's', 0x1808 },
			{ 't', 0x78 },
			{ 'u', 0x1c },
			{ 'v', 0x4010 },
			{ 'w', 0x5014 },
			{ 'x', 0x5500 },
			{ 'y', 0xa0e },
			{ 'z', 0x4048 },
			{ '{', 0x4149 },
			{ '|', 0x2200 },
			{ '}', 0x1c09 },
			{ '~', 0x4c40 },
		};
	}
}
