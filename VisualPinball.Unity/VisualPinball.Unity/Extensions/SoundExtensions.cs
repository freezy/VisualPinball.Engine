// Visual Pinball Engine
// Copyright (C) 2020 freezy and VPE Team
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
using UnityEngine;

namespace VisualPinball.Unity
{
	public static class SoundExtensions
	{
		/// <summary>
		/// Convert SoundData samples to -1.0/1.0 range floats
		/// </summary>
		/// <param name="sndData"></param>
		/// <returns></returns>
		public static float[] ToFloats(this Engine.VPT.Sound.SoundData sndData)
		{
			var wfx = sndData.Wfx;
			var samples = new List<float>();

			switch (wfx.BitsPerSample) {
				case 8: {
					foreach (var data in sndData.Data) {
						samples.Add((data - 128) / 128.0f);
					}
					break;
				}

				case 16: {
					for (var i = 0; i < sndData.Data.Length; i += 2) {
						var data2 = sndData.Data[i + 1];
						var sndVal = sndData.Data[i] | (data2 < 128 ? (data2 << 8) : ((data2 - 256) << 8));
						samples.Add(sndVal / 32768.0f);
					}
					break;
				}

				case 24: {
					for (var i = 0; i < sndData.Data.Length; i += 3) {
						var data3 = sndData.Data[i + 2];
						var sndVal = sndData.Data[i] | (sndData.Data[i + 1] << 8) | (data3 < 128 ? (data3 << 16) : ((data3 - 256) << 16));
						samples.Add(sndVal / 8388608.0f);
					}
					break;
				}

				default:
					break;
			}

			return samples.ToArray();
		}
	}
}
