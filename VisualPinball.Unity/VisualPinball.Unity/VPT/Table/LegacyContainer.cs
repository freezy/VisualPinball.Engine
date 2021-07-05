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
using System.IO;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Decal;
using VisualPinball.Engine.VPT.DispReel;
using VisualPinball.Engine.VPT.Flasher;
using VisualPinball.Engine.VPT.LightSeq;
using VisualPinball.Engine.VPT.TextBox;
using VisualPinball.Engine.VPT.Timer;
using Texture = UnityEngine.Texture;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Legacy in VPE is data from Visual Pinball 10 that isn't used in VPE,
	/// but still available to export.
	/// </summary>
	[Serializable]
	public class LegacyContainer
	{
		public DecalData[] decals;
		public DispReelData[] dispReels;
		public FlasherData[] flashers;
		public LightSeqData[] lightSeqs;
		public TextBoxData[] textBoxes;
		public TimerData[] timers;
		public List<LegacyTexture> textures = new List<LegacyTexture>();
	}

	[Serializable]
	public class LegacyTexture
	{
		public string InternalName;
		public string Path;
		public float AlphaTestValue;
		public Texture Texture;

		public LegacyTexture(TextureData data, Texture texture)
		{
			InternalName = data.InternalName;
			Path = data.Path;
			AlphaTestValue = data.AlphaTestValue;
			Texture = texture;
		}

		public Engine.VPT.Texture ToTexture()
		{
			var data = new TextureData(Texture.name) {
				InternalName = InternalName,
				Path = Path,
				AlphaTestValue = AlphaTestValue,
				Width = Texture.width,
				Height = Texture.height
			};

			#if UNITY_EDITOR
			var path = UnityEditor.AssetDatabase.GetAssetPath(Texture);
			if (!string.IsNullOrEmpty(path)) {
				var bytes = File.ReadAllBytes(path);
				data.Binary = new BinaryData(Texture.name, bytes) {
					InternalName = InternalName,
					Path = Path,
					Size = bytes.Length,
				};
			}
			#endif

			return new Engine.VPT.Texture(data);
		}
	}
}
