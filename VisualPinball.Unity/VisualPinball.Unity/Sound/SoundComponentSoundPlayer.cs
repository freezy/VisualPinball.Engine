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
using System.Threading;
using System.Threading.Tasks;
using NLog;
using UnityEngine;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Single use object to manage the playback of a music, callout, or sound effect asset in the
	/// context of a <c>SoundComponent</c>
	/// </summary>
	public interface ISoundComponentSoundPlayer : IDisposable
	{
		public void StartSound(float volume = 10f);
		public void StopSound(bool allowFade);
		public bool IsPlayingOrRequestingSound();
	}

	/// <summary>
	/// Single use object to manage the playback of a sound effect asset in the context of a
	/// <c>SoundComponent</c>
	/// </summary>
	public class SoundComponentSoundEffectPlayer : ISoundComponentSoundPlayer
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
		private static readonly LogOncePerKey<string> InvalidAssetLog = new();

		private SoundEffectAsset _soundEffectAsset;
		private GameObject _audioSourceGo;
		private CancellationTokenSource _allowFadeCts;
		private CancellationTokenSource _instantFadeCts;
		private Task _playTask;
		private readonly SoundPlaybackContext _context;

		public SoundComponentSoundEffectPlayer(
			SoundEffectAsset soundEffectAsset,
			GameObject audioSourceGo,
			SoundPlaybackContext context = null
		)
		{
			_soundEffectAsset = soundEffectAsset;
			_audioSourceGo = audioSourceGo;
			_context = context;
			_allowFadeCts = new CancellationTokenSource();
			_instantFadeCts = new CancellationTokenSource();
		}

		public async void StartSound(float volume)
		{
			// a sound asset without clips is the table's problem, not a bug: say so once, then skip it
			if (_soundEffectAsset != null && !_soundEffectAsset.IsValid()) {
				if (InvalidAssetLog.ShouldLog(_soundEffectAsset.name)) {
					Logger.Warn("Sound effect {Name} has no audio clips and won't play.", _soundEffectAsset.name);
				}
				return;
			}
			try
			{
				_playTask = _soundEffectAsset.Play(
					_audioSourceGo,
					_allowFadeCts.Token,
					_instantFadeCts.Token,
					volume,
					_context
				);
				await _playTask;
			}
			catch (OperationCanceledException) { }
			catch (Exception e)
			{
				Logger.Error(e, "Cannot play sound effect {Name}.", _soundEffectAsset ? _soundEffectAsset.name : "(none)");
			}
		}

		public void StopSound(bool allowFade)
		{
			var ctsToCancel = allowFade ? _allowFadeCts : _instantFadeCts;
			ctsToCancel.Cancel();
		}

		public bool IsPlayingOrRequestingSound()
		{
			return _playTask != null && !_playTask.IsCompleted;
		}

		public void Dispose()
		{
			_allowFadeCts.Dispose();
			_instantFadeCts.Dispose();
		}
	}

	/// <summary>
	/// Single use object to manage the playback of a music asset in the context of a
	/// <c>SoundComponent</c>
	/// </summary>
	public class SoundComponentMusicPlayer : ISoundComponentSoundPlayer
	{
		private MusicRequest _request;
		private MusicCoordinator _coordinator;
		private int _requestId = -1;

		public SoundComponentMusicPlayer(MusicRequest request, MusicCoordinator coordinator)
		{
			_request = request;
			_coordinator = coordinator;
		}

		public void StartSound(float volume)
		{
			_coordinator.AddRequest(_request, out _requestId);
		}

		public bool IsPlayingOrRequestingSound()
		{
			return _requestId != -1
				&& _coordinator.GetRequestStatus(_requestId)
					is MusicRequestStatus.Waiting
						or MusicRequestStatus.Playing;
		}

		public void StopSound(bool allowFade)
		{
			if (_coordinator != null && IsPlayingOrRequestingSound())
				_coordinator.RemoveRequest(_requestId);
		}

		public void Dispose() { }
	}

	/// <summary>
	/// Single use object to manage the playback of a callout asset in the context of a
	/// <c>SoundComponent</c>
	/// </summary>
	public class SoundComponentCalloutPlayer : ISoundComponentSoundPlayer
	{
		private CalloutRequest _request;
		private CalloutCoordinator _coordinator;
		private int _requestId = -1;

		public SoundComponentCalloutPlayer(CalloutRequest request, CalloutCoordinator coordinator)
		{
			_request = request;
			_coordinator = coordinator;
		}

		public void StartSound(float volume = 10)
		{
			_coordinator.EnqueueCallout(_request, out _requestId);
		}

		public void StopSound(bool allowFade)
		{
			if (_coordinator != null && IsPlayingOrRequestingSound())
				_coordinator.DequeueCallout(_requestId);
		}

		public bool IsPlayingOrRequestingSound()
		{
			return _requestId != -1
				&& _coordinator.GetRequestStatus(_requestId)
					is CalloutRequestStatus.Queued
						or CalloutRequestStatus.Playing;
		}

		public void Dispose() { }
	}
}
