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

// ReSharper disable MemberCanBePrivate.Global

using System;
using System.Linq;

namespace VisualPinball.Unity
{
	public class SoundPackable {

		public bool Interrupt;
		public float Volume;

		public static byte[] Pack(SoundComponent comp) => PackageApi.Packer.Pack(new SoundPackable {
			Interrupt = comp.Interrupt,
			Volume = comp.Volume,
		});

		public static void Unpack(byte[] bytes, SoundComponent comp)
		{
			var data = PackageApi.Packer.Unpack<SoundPackable>(bytes);
			comp.Interrupt = data.Interrupt;
			comp.Volume = data.Volume;
		}
	}

	public class BinaryEventSoundPackable : SoundPackable
	{
		public StartWhen StartWhen;
		public StopWhen StopWhen;
	}

	public class SwitchSoundPackable : BinaryEventSoundPackable
	{
		public string SwitchName;

		public static byte[] Pack(SwitchSoundComponent comp)
			=> PackageApi.Packer.Pack(new SwitchSoundPackable {
				Interrupt = comp.Interrupt,
				Volume = comp.Volume,
				StartWhen = comp.StartWhen,
				StopWhen = comp.StopWhen,
				SwitchName = comp.SwitchName
			});

		public static void Unpack(byte[] bytes, SwitchSoundComponent comp)
		{
			var data = PackageApi.Packer.Unpack<SwitchSoundPackable>(bytes);
			comp.Interrupt = data.Interrupt;
			comp.Volume = data.Volume;
			comp.StartWhen = data.StartWhen;
			comp.StopWhen = data.StopWhen;
			comp.SwitchName = data.SwitchName;
		}
	}

	public class CoilSoundPackable : BinaryEventSoundPackable
	{
		public string CoilName;

		public static byte[] Pack(CoilSoundComponent comp)
			=> PackageApi.Packer.Pack(new CoilSoundPackable {
				Interrupt = comp.Interrupt,
				Volume = comp.Volume,
				StartWhen = comp.StartWhen,
				StopWhen = comp.StopWhen,
				CoilName = comp.CoilName
			});

		public static void Unpack(byte[] bytes, CoilSoundComponent comp)
		{
			var data = PackageApi.Packer.Unpack<CoilSoundPackable>(bytes);
			comp.Interrupt = data.Interrupt;
			comp.Volume = data.Volume;
			comp.StartWhen = data.StartWhen;
			comp.StopWhen = data.StopWhen;
			comp.CoilName = data.CoilName;
		}
	}

	public struct SoundReferencesPackable {

		public int SoundAssetRef;
		public string[]	ClipRefs;

		public static byte[] PackReferences(SoundComponent comp, PackagedFiles files)
		{
			if (!comp.SoundAsset) {
				return Array.Empty<byte>();
			}

			// pack asset
			var assetRef = files.AddAsset(comp.SoundAsset);

			// pack sound files
			var clipRefs = comp.SoundAsset.Clips != null
				? comp.SoundAsset.Clips.Select(files.Add).ToArray()
				: Array.Empty<string>();

			return PackageApi.Packer.Pack(new SoundReferencesPackable {
				SoundAssetRef = assetRef,
				ClipRefs = clipRefs
			});
		}

		public static void Unpack(byte[] bytes, SoundComponent comp, PackagedFiles files)
		{
			var data = PackageApi.Packer.Unpack<SoundReferencesPackable>(bytes);
			comp.SoundAsset = files.GetAsset<SoundAsset>(data.SoundAssetRef);
			comp.SoundAsset.Clips = data.ClipRefs.Select(files.GetAudioClip).ToArray();
		}
	}

	public struct SoundMetaPackable
	{
		public string Guid;
		// will probably get more data in here
	}
}
