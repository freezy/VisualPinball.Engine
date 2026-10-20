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
		private readonly SoundPlaybackContext _playbackContext = new();

		protected static readonly Logger Logger = LogManager.GetCurrentClassLogger();
		private static readonly LogOncePerKey<int> CannotPlayLog = new();

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
			_musicCoordinator = GetComponentInParent<MusicCoordinator>();
			// only sounds that need a coordinator care whether there is one
			if (SoundAsset is CalloutAsset && _calloutCoordinator == null) {
				Logger.Warn("Sound {Name} plays a callout, but there is no callout coordinator above it. It won't play.", name);
			}
			if (SoundAsset is MusicAsset && _musicCoordinator == null) {
				Logger.Warn("Sound {Name} plays music, but there is no music coordinator above it. It won't play.", name);
			}
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

		protected void StartSound() => StartSound(SoundAsset, 1f);

		protected virtual void StartSound(SoundAsset asset, float gain = 1f)
		{
			if (!isActiveAndEnabled)
			{
				if (CannotPlayLog.ShouldLog(UnityObjectId.Get(this))) {
					Logger.Warn("Cannot play sound component {Name}, it is disabled.", name);
				}
				return;
			}

			if (asset == null)
			{
				if (CannotPlayLog.ShouldLog(UnityObjectId.Get(this))) {
					Logger.Warn("Cannot play sound component {Name}, it has no sound asset. Assign it in the inspector.", name);
				}
				return;
			}

			if (IsPlayingOrRequestingSound())
			{
				if (asset is MusicAsset)
				{
					// We never want to have multiple active music requests. Makes no sense.
					StopAllSounds(allowFade: true);
				}
				else
				{
					switch (EffectiveMultiPlayMode)
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

			var player = CreateSoundPlayer(asset, gain);
			player.StartSound(Volume * gain);
			_soundPlayers.Add(player);
		}

		protected void StopAllSounds(bool allowFade)
		{
			_soundPlayers.ForEach(x => x.StopSound(allowFade));
		}

		protected virtual MultiPlayMode EffectiveMultiPlayMode => MultiPlayMode;

		private ISoundComponentSoundPlayer CreateSoundPlayer(SoundAsset asset, float gain)
		{
			if (asset is SoundEffectAsset)
			{
				return new SoundComponentSoundEffectPlayer(
					(SoundEffectAsset)asset,
					gameObject,
					_playbackContext
				);
			}

			if (asset is CalloutAsset)
			{
				var request = new CalloutRequest(
					(CalloutAsset)asset,
					Priority,
					CalloutMaxQueueTime,
					Volume * gain
				);
				return new SoundComponentCalloutPlayer(request, _calloutCoordinator);
			}

			if (asset is MusicAsset)
			{
				var request = new MusicRequest((MusicAsset)asset, Priority, Volume * gain);
				return new SoundComponentMusicPlayer(request, _musicCoordinator);
			}

			throw new NotImplementedException(
				$"Unknown type of sound asset '{asset.GetType()}'"
			);
		}

		public virtual bool SupportsLoopingSoundAssets() => true;

		public virtual Type GetRequiredType() => null;
	}
}
