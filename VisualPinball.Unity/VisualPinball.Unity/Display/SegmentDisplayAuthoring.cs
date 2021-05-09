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
	[AddComponentMenu("Visual Pinball/Display/Segment Display")]
	public class SegmentDisplayAuthoring : DisplayAuthoring
	{
		public override string Id { get => _id; set => _id = value;}

		public override float AspectRatio { get; set; } = 0.6f;

		protected override float MeshWidth => NumChars * MeshHeight * AspectRatio;
		public override float MeshHeight => 0.2f;
		protected override float MeshDepth => 0.01f;

		[SerializeField] private string _id = "display0";
		[SerializeField] private int _numChars = 7;
		[SerializeField] private int _numSegments = 14;
		[SerializeField] private Color _litColor = new Color(1, 0.4f, 0);
		[SerializeField] private Color _unlitColor = new Color(0.25f, 0.25f, 0.25f);
		[SerializeField] private float _skewAngle = math.radians(7);
		[SerializeField] private float segmentWeight = 0.05f;
		[SerializeField] private float2 _padding = new float2(0.5f, 0.4f);
		[SerializeField] private float2 _separatorPos = new float2(1.5f, 0.0f);
		[SerializeField] private int _separatorType = 1;
		[SerializeField] private bool _separatorEveryThreeOnly;

		[NonSerialized] private Color32[] _colorBuffer;

		private const int MaxNumSegments = 16;
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		#region Shader Prop Constants

		private static readonly int LitColorProp = Shader.PropertyToID("__LitColor");
		private static readonly int UnlitColorProp = Shader.PropertyToID("__UnlitColor");
		private static readonly int DataProp = Shader.PropertyToID("__SegmentData");
		private static readonly int NumCharsProp = Shader.PropertyToID("__NumChars");
		private static readonly int NumSegmentsProp = Shader.PropertyToID("__NumSegments");
		private static readonly int SegmentWeightProp = Shader.PropertyToID("__SegmentWeight");
		private static readonly int SkewAngleProp = Shader.PropertyToID("__SkewAngle");
		private static readonly int PaddingProp = Shader.PropertyToID("__Padding");
		private static readonly int SeparatorPosProp = Shader.PropertyToID("__SeparatorPos");
		private static readonly int SeparatorTypeProp = Shader.PropertyToID("__SeparatorType");
		private static readonly int SeparatorEveryThreeOnlyProp = Shader.PropertyToID("__SeparatorEveryThreeOnly");

		#endregion

		#region Shader Props

		public int NumChars {
			get => _numChars;
			set {
				_numChars = value;
				var mr = gameObject.GetComponent<MeshRenderer>();
				if (mr) {
					mr.sharedMaterial.SetFloat(NumCharsProp, value);
				}
				RegenerateMesh();
			}
		}

		public int NumSegments {
			get => _numSegments;
			set {
				_numSegments = value;
				var mr = gameObject.GetComponent<MeshRenderer>();
				if (mr != null) {
					mr.sharedMaterial.SetFloat(NumSegmentsProp, value);
				}
			}
		}

		public override Color LitColor {
			get => _litColor;
			set {
				_litColor = value;
				var mr = gameObject.GetComponent<MeshRenderer>();
				if (mr) {
					mr.sharedMaterial.SetColor(LitColorProp, value);
				}
			}
		}

		public override Color UnlitColor {
			get => _unlitColor;
			set {
				_unlitColor = value;
				var mr = gameObject.GetComponent<MeshRenderer>();
				if (mr) {
					mr.sharedMaterial.SetColor(UnlitColorProp, value);
				}
			}
		}

		public float SkewAngle {
			get => _skewAngle;
			set {
				_skewAngle = value;
				var mr = gameObject.GetComponent<MeshRenderer>();
				if (mr) {
					mr.sharedMaterial.SetFloat(SkewAngleProp, value);
				}
			}
		}

		public float SegmentWeight {
			get => segmentWeight;
			set {
				segmentWeight = value;
				var mr = gameObject.GetComponent<MeshRenderer>();
				if (mr != null) {
					mr.sharedMaterial.SetFloat(SegmentWeightProp, value);
				}
			}
		}

		public float2 Padding {
			get => _padding;
			set {
				_padding = value;
				var mr = gameObject.GetComponent<MeshRenderer>();
				if (mr != null) {
					mr.sharedMaterial.SetVector(PaddingProp, new Vector4(value.x, value.y));
				}
			}
		}


		public float2 SeparatorPos {
			get => _separatorPos;
			set {
				_separatorPos = value;
				var mr = gameObject.GetComponent<MeshRenderer>();
				if (mr != null) {
					mr.sharedMaterial.SetVector(SeparatorPosProp, new Vector4(value.x, value.y));
				}
			}
		}

		public int SeparatorType {
			get => _separatorType;
			set {
				_separatorType = value;
				var mr = gameObject.GetComponent<MeshRenderer>();
				if (mr != null) {
					mr.sharedMaterial.SetInt(SeparatorTypeProp, value);
				}
			}
		}

		public bool SeparatorEveryThreeOnly {
			get => _separatorEveryThreeOnly;
			set {
				_separatorEveryThreeOnly = value;
				var mr = gameObject.GetComponent<MeshRenderer>();
				if (mr != null) {
					mr.sharedMaterial.SetInt(SeparatorEveryThreeOnlyProp, value ? 1 : 0);
				}
			}
		}

		public string SegmentTypeName;

		#endregion

		protected override Material CreateMaterial()
		{
			var material = Instantiate(RenderPipeline.Current.MaterialConverter.SegmentDisplay);

			material.mainTexture = _texture;
			material.SetTexture(DataProp, _texture);
			material.SetFloat(NumCharsProp, _numChars);
			material.SetFloat(NumSegmentsProp, _numSegments);
			material.SetColor(LitColorProp, _litColor);
			material.SetColor(UnlitColorProp, _unlitColor);
			material.SetFloat(SkewAngleProp, _skewAngle);
			material.SetFloat(SegmentWeightProp, segmentWeight);
			material.SetVector(PaddingProp, new Vector4(_padding.x, _padding.y));
			material.SetVector(PaddingProp, new Vector4(_padding.x, _padding.y));
			material.SetInt(SeparatorTypeProp, _separatorType);
			material.SetInt(SeparatorEveryThreeOnlyProp, _separatorEveryThreeOnly ? 1 : 0);

			Logger.Info("Recreating segment display material!");

			return material;
		}

		public override void UpdateFrame(DisplayFrameFormat format, byte[] source)
		{
			var target = new ushort[source.Length / sizeof(short)];
			Buffer.BlockCopy(source, 0, target, 0, source.Length);
			UpdateFrame(target);
		}

		public override void UpdateDimensions(int width, int height, bool _ = false)
		{
			_texture = new Texture2D(MaxNumSegments, width * height);
			_colorBuffer = new Color32[MaxNumSegments * width * height];
			RegenerateMesh();
		}

		public void SetText(string text)
		{
			var source = GenerateAlphaNumeric(text);
			const int size = sizeof(short);
			var target = new byte[_numChars * size];
			Buffer.BlockCopy(source, 0, target, 0, math.min(source.Length, _numChars) * size);

			if (_colorBuffer == null) {
				UpdateDimensions(_numChars, 1);
			}
			UpdateFrame(DisplayFrameFormat.Segment, target);
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
				for (var x = 0; x < MaxNumSegments; x++) {
					var seg = data[y] >> x & 0x1;
					var val = seg == 1 ? (byte)0xff : (byte)0x0;
					_colorBuffer[y * MaxNumSegments + x] = new Color32(val, val, val, 1);
				}
			}
			_texture.SetPixels32(_colorBuffer);
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
