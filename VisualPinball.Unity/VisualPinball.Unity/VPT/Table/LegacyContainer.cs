// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Bumper;
using VisualPinball.Engine.VPT.Decal;
using VisualPinball.Engine.VPT.DispReel;
using VisualPinball.Engine.VPT.Flasher;
using VisualPinball.Engine.VPT.Flipper;
using VisualPinball.Engine.VPT.Gate;
using VisualPinball.Engine.VPT.HitTarget;
using VisualPinball.Engine.VPT.Kicker;
using VisualPinball.Engine.VPT.Light;
using VisualPinball.Engine.VPT.LightSeq;
using VisualPinball.Engine.VPT.Plunger;
using VisualPinball.Engine.VPT.Primitive;
using VisualPinball.Engine.VPT.Ramp;
using VisualPinball.Engine.VPT.Rubber;
using VisualPinball.Engine.VPT.Sound;
using VisualPinball.Engine.VPT.Spinner;
using VisualPinball.Engine.VPT.Surface;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Engine.VPT.TextBox;
using VisualPinball.Engine.VPT.Timer;
using VisualPinball.Engine.VPT.Trigger;
using VisualPinball.Engine.VPT.MetalWireGuide;
using Texture = UnityEngine.Texture;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Legacy in VPE is data from Visual Pinball 10 that isn't used in VPE,
	/// but still available to export.
	/// </summary>
	[Serializable]
	public class LegacyContainer : ScriptableObject
	{
		public TableData TableData;
		public SerializableDictionary<string, BumperData> Bumpers = new SerializableDictionary<string, BumperData>();
		public SerializableDictionary<string, FlipperData> Flippers = new SerializableDictionary<string, FlipperData>();
		public SerializableDictionary<string, GateData> Gates = new SerializableDictionary<string, GateData>();
		public SerializableDictionary<string, HitTargetData> HitTargets = new SerializableDictionary<string, HitTargetData>();
		public SerializableDictionary<string, KickerData> Kickers = new SerializableDictionary<string, KickerData>();
		public SerializableDictionary<string, LightData> Lights = new SerializableDictionary<string, LightData>();
		public SerializableDictionary<string, PlungerData> Plungers = new SerializableDictionary<string, PlungerData>();
		public SerializableDictionary<string, PrimitiveData> Primitives = new SerializableDictionary<string, PrimitiveData>();
		public SerializableDictionary<string, RampData> Ramps = new SerializableDictionary<string, RampData>();
		public SerializableDictionary<string, RubberData> Rubbers = new SerializableDictionary<string, RubberData>();
		public SerializableDictionary<string, SpinnerData> Spinners = new SerializableDictionary<string, SpinnerData>();
		public SerializableDictionary<string, SurfaceData> Surfaces = new SerializableDictionary<string, SurfaceData>();
		public SerializableDictionary<string, TriggerData> Triggers = new SerializableDictionary<string, TriggerData>();
		public SerializableDictionary<string, MetalWireGuideData> MetalWireGuides = new SerializableDictionary<string, MetalWireGuideData>();

		public DecalData[] Decals;
		public DispReelData[] DispReels;
		public FlasherData[] Flashers;
		public LightSeqData[] LightSeqs;
		public TextBoxData[] TextBoxes;
		public TimerData[] Timers;
		public List<LegacyTexture> Textures = new List<LegacyTexture>();
		public List<LegacySound> Sounds = new List<LegacySound>();
	}

	[Serializable]
	public class LegacySound
	{
		public string Name => AudioClip == null ? "<unset>" : AudioClip.name;
		public string InternalName;
		public string Path;
		public WaveFormat Wfx;
		public byte OutputTarget;
		public int Volume;
		public int Balance;
		public int Fade;

		public AudioClip AudioClip;

		public bool IsSet => AudioClip != null;

		public LegacySound()
		{
		}

		public LegacySound(SoundData data, AudioClip audioClip)
		{
			AudioClip = audioClip;
			InternalName = data.InternalName;
			Path = data.Path;
			Wfx = data.Wfx;
			OutputTarget = data.OutputTarget;
			Volume = data.Volume;
			Balance = data.Balance;
			Fade = data.Fade;
		}

		public LegacySound(AudioClip audioClip)
		{
			AudioClip = audioClip;
			InternalName = audioClip.name;
		}

		public Sound ToSound()
		{
			if (AudioClip == null) {
				throw new InvalidOperationException("Cannot convert to sound without audio clip!");
			}
			var data = new SoundData(AudioClip.name) {
				InternalName = InternalName,
				Path = Path,
				Wfx = Wfx,
				OutputTarget = OutputTarget,
				Volume = Volume,
				Balance = Balance,
				Fade = Fade
			};
			data.Wfx.FormatTag = 1;
			data.Wfx.Channels = (ushort)(AudioClip.channels == 2 ? 2 : 1);
			data.Wfx.SamplesPerSec = (uint)AudioClip.frequency;
			data.Wfx.BitsPerSample = 16;
			data.Wfx.BlockAlign = (ushort)(data.Wfx.BitsPerSample / 8 * data.Wfx.Channels);
			data.Wfx.AvgBytesPerSec = data.Wfx.SamplesPerSec * data.Wfx.BlockAlign;

			#if UNITY_EDITOR
			var path = UnityEditor.AssetDatabase.GetAssetPath(AudioClip);
			if (!string.IsNullOrEmpty(path)) {
				var bytes = File.ReadAllBytes(path);
				data.Data = path.ToLower().EndsWith(".wav")
					? bytes.Skip(44).ToArray()
					: bytes;
			}
			#endif

			return new Sound(data);
		}
	}

	[Serializable]
	public class LegacyTexture
	{
		public string Name => Texture == null ? "<unset>" : Texture.name;
		public string InternalName;
		public string Path;
		public float AlphaTestValue;
		public Texture Texture;

		/// <summary>
		/// As textures are converted (e.g. webp), we convert to png so it can
		/// be read by Unity, but keep the original file around to save it back
		/// later. This points to the original file.
		/// </summary>
		public string OriginalPath;

		public bool IsSet => Texture != null;

		public LegacyTexture()
		{
		}

		public LegacyTexture(TextureData data, Texture texture)
		{
			InternalName = data.InternalName;
			Path = data.Path;
			AlphaTestValue = data.AlphaTestValue;
			Texture = texture;
		}

		public LegacyTexture(Texture texture)
		{
			Texture = texture;
			InternalName = texture.name;
		}

		public Engine.VPT.Texture ToTexture()
		{
			if (Texture == null) {
				throw new InvalidOperationException("Cannot convert to texture without texture!");
			}
			var data = new TextureData(Texture.name) {
				InternalName = InternalName,
				Path = Path,
				AlphaTestValue = AlphaTestValue,
				Width = Texture.width,
				Height = Texture.height
			};

			#if UNITY_EDITOR
			var path = OriginalPath != null && File.Exists(OriginalPath)
				? OriginalPath
				: UnityEditor.AssetDatabase.GetAssetPath(Texture);
			if (!string.IsNullOrEmpty(path)) {
				var bytes = File.ReadAllBytes(path);
				data.Binary = new BinaryData(Texture.name, bytes) {
					InternalName = InternalName,
					Path = path,
					Size = bytes.Length,
				};
			}
			#endif

			return new Engine.VPT.Texture(data, null);
		}
	}
}
