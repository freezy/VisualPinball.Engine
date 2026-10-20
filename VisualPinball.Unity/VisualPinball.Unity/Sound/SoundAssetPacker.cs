// Visual Pinball Engine
// Copyright (C) 2025 freezy and VPE Team
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
using UnityEngine;
using System.Linq;

namespace VisualPinball.Unity
{
	public class SoundAssetPacker : IPacker<SoundAsset>, IPacker<ScriptableObject>
	{
		MetaPackable IPacker<ScriptableObject>.Pack(int instanceId, ScriptableObject asset, PackagedFiles files)
			=> Pack(instanceId, (SoundAsset)asset, files);

		MetaPackable IPacker<ScriptableObject>.Unpack(byte[] bytes, ScriptableObject asset, PackagedFiles files)
			=> Unpack(bytes, (SoundAsset)asset, files);

		public MetaPackable Pack(int instanceId, SoundAsset soundAsset, PackagedFiles files)
		{
			var clipRefs = soundAsset.Clips != null
				? soundAsset.Clips.Select(files.Add).ToArray()
				: Array.Empty<string>();

			return new SoundAssetMetaPackable {
				ClipRefs = clipRefs,
				InstanceId = instanceId
			};
		}

		public MetaPackable Unpack(byte[] bytes, SoundAsset soundAsset, PackagedFiles files)
		{
			var data = PackageApi.Packer.Unpack<SoundAssetMetaPackable>(bytes);
			soundAsset.Clips = data.ClipRefs.Select(files.GetAudioClip).ToArray();
			return data;
		}
	}

	public class SoundAssetMetaPackable : MetaPackable
	{
		public string[] ClipRefs;
	}
}
