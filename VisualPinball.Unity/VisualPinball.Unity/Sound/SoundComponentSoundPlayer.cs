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
using UnityEngine;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Single use object to manage the playback of a music, callout, or sound effect asset
	/// </summary>
	public abstract class SoundCommponentSoundPlayer : MonoBehaviour
	{
		public abstract void StartSound(float volume = 10f);
		public abstract void StopSound(bool allowFade);
		public abstract bool IsPlayingOrRequestingSound();
	}

	public class SoundComponentSoundEffectPlayer : SoundCommponentSoundPlayer
	{
		private SoundEffectAsset _soundEffectAsset;
		private CancellationTokenSource _allowFadeCts;
		private CancellationTokenSource _instantFadeCts;
		private Task _playTask;

		public void Init(SoundEffectAsset soundEffectAsset)
		{
			_soundEffectAsset = soundEffectAsset;
			_allowFadeCts = new CancellationTokenSource();
			_instantFadeCts = new CancellationTokenSource();
		}

		public override async void StartSound(float volume)
		{
			_playTask = _soundEffectAsset.Play(
				gameObject,
				_allowFadeCts.Token,
				_instantFadeCts.Token,
				volume
			);

			try
			{
				await _playTask;
			}
			catch (OperationCanceledException) { }
		}

		public override void StopSound(bool allowFade)
		{
			var ctsToCancel = allowFade ? _allowFadeCts : _instantFadeCts;
			ctsToCancel.Cancel();
		}

		public override bool IsPlayingOrRequestingSound()
		{
			return _playTask != null && !_playTask.IsCompleted;
		}

		private void OnDestroy()
		{
			_allowFadeCts.Dispose();
			_instantFadeCts.Dispose();
		}
	}

	public class SoundComponentMusicPlayer : SoundCommponentSoundPlayer
	{
		private MusicRequest _request;
		private MusicCoordinator _coordinator;
		private int _requestId = -1;

		public void Init(MusicRequest request, MusicCoordinator coordinator)
		{
			_request = request;
			_coordinator = coordinator;
		}

		public override void StartSound(float volume)
		{
			_coordinator.AddRequest(_request, out _requestId);
		}

		public override bool IsPlayingOrRequestingSound()
		{
			return _coordinator.GetRequestStatus(_requestId)
				is MusicRequestStatus.Waiting
					or MusicRequestStatus.Playing;
		}

		public override void StopSound(bool allowFade)
		{
			_coordinator.RemoveRequest(_requestId);
		}

		private void OnDestroy()
		{
			if (_coordinator != null && IsPlayingOrRequestingSound())
				_coordinator.RemoveRequest(_requestId);
		}
	}

	public class SoundComponentCalloutPlayer : SoundCommponentSoundPlayer
	{
		private CalloutRequest _request;
		private CalloutCoordinator _coordinator;
		private int _requestId = -1;

		public void Init(CalloutRequest request, CalloutCoordinator coordinator)
		{
			_request = request;
			_coordinator = coordinator;
		}

		public override void StartSound(float volume = 10)
		{
			_coordinator.EnqueueCallout(_request, out _requestId);
		}

		public override void StopSound(bool allowFade)
		{
			_coordinator.DequeueCallout(_requestId);
		}

		public override bool IsPlayingOrRequestingSound()
		{
			return _coordinator.GetRequestStatus(_requestId)
				is CalloutRequestStatus.Queued
					or CalloutRequestStatus.Playing;
		}

		private void OnDestroy()
		{
			if (_coordinator != null && IsPlayingOrRequestingSound())
				_coordinator.DequeueCallout(_requestId);
		}
	}
}
