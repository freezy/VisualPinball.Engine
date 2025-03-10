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
using System.Collections.Generic;
using System.Linq;
using NLog;
using UnityEngine;
using UnityEngine.Serialization;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	public enum MultiPlayMode
	{
		PlayInParallel,
		DoNotPlay,
		FadeOutPrevious,
		StopPrevious,
	}

	/// <summary>
	/// Base component for playing a <c>SoundAsset</c> using the public methods <c>Play</c> and <c>Stop</c>.
	/// </summary>
	[PackAs("Sound")]
	[AddComponentMenu("Pinball/Sound/Sound")]
	public class SoundComponent : EnableAfterAwakeComponent, IPackable
	{
		public SoundAsset SoundAsset;
		public MultiPlayMode MultiPlayMode;
		[Range(0f, 1f)] public float Volume = 1f;
		public SoundPriority Priority = SoundPriority.Medium;
		public float CalloutMaxQueueTime = -1;

		private CalloutCoordinator _calloutCoordinator;
		private MusicCoordinator _musicCoordinator;
		private readonly List<ISoundComponentSoundPlayer> _soundPlayers = new();

		protected static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public bool IsPlayingOrRequestingSound()
		{
			return _soundPlayers.Any(x => x.IsPlayingOrRequestingSound());
		}

		#region Packaging

		public byte[] Pack() => SoundPackable.Pack(this);

		public byte[] PackReferences(Transform root, PackagedRefs refs, PackagedFiles files) =>
			SoundReferencesPackable.PackReferences(this, files);

		public void Unpack(byte[] bytes) => SoundPackable.Unpack(bytes, this);

		public void UnpackReferences(
			byte[] data,
			Transform root,
			PackagedRefs refs,
			PackagedFiles files
		) => SoundReferencesPackable.Unpack(data, this, files);

		#endregion

		protected override void OnEnableAfterAfterAwake()
		{
			base.OnEnableAfterAfterAwake();
			_calloutCoordinator = GetComponentInParent<CalloutCoordinator>();
			if (_calloutCoordinator == null)
				Logger.Error("No callout coordinator found in parents. Callouts will not work!");
			_musicCoordinator = GetComponentInParent<MusicCoordinator>();
			if (_musicCoordinator == null)
				Logger.Error("No music coordinator found in parents. Music will not work!");
		}

		protected void Update()
		{
			for (int i = _soundPlayers.Count - 1; i >= 0; i--)
			{
				if (!_soundPlayers[i].IsPlayingOrRequestingSound())
				{
					_soundPlayers[i].Dispose();
					_soundPlayers.RemoveAt(i);
				}
			}
		}

		protected virtual void OnDisable()
		{
			StopAllSounds(allowFade: true);
		}

		protected virtual void OnDestroy()
		{
			StopAllSounds(allowFade: false);
			_soundPlayers.ForEach(x => x.Dispose());
		}

		protected void StartSound()
		{
			if (!isActiveAndEnabled)
			{
				Logger.Warn("Cannot play a disabled sound component.");
				return;
			}

			if (SoundAsset == null)
			{
				Logger.Warn("Cannot play without sound asset. Assign it in the inspector.");
				return;
			}

			if (IsPlayingOrRequestingSound())
			{
				if (SoundAsset is MusicAsset)
				{
					// We never want to have multiple active music requests. Makes no sense.
					StopAllSounds(allowFade: true);
				}
				else
				{
					switch (MultiPlayMode)
					{
						case MultiPlayMode.PlayInParallel:
							// Don't need to do anything.
							break;
						case MultiPlayMode.DoNotPlay:
							return;
						case MultiPlayMode.FadeOutPrevious:
							StopAllSounds(allowFade: true);
							break;
						case MultiPlayMode.StopPrevious:
							StopAllSounds(allowFade: false);
							break;
					}
				}
			}

			var player = CreateSoundPlayer();
			player.StartSound(Volume);
			_soundPlayers.Add(player);
		}

		protected void StopAllSounds(bool allowFade)
		{
			_soundPlayers.ForEach(x => x.StopSound(allowFade));
		}

		private ISoundComponentSoundPlayer CreateSoundPlayer()
		{
			if (SoundAsset is SoundEffectAsset)
			{
				return new SoundComponentSoundEffectPlayer(
					(SoundEffectAsset)SoundAsset,
					gameObject
				);
			}

			if (SoundAsset is CalloutAsset)
			{
				var request = new CalloutRequest(
					(CalloutAsset)SoundAsset,
					Priority,
					CalloutMaxQueueTime,
					Volume
				);
				return new SoundComponentCalloutPlayer(request, _calloutCoordinator);
			}

			if (SoundAsset is MusicAsset)
			{
				var request = new MusicRequest((MusicAsset)SoundAsset, Priority, Volume);
				return new SoundComponentMusicPlayer(request, _musicCoordinator);
			}

			throw new NotImplementedException(
				$"Unknown type of sound asset '{SoundAsset.GetType()}'"
			);
		}

		public virtual bool SupportsLoopingSoundAssets() => true;

		public virtual Type GetRequiredType() => null;
	}
}
