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
using System.Threading;
using System.Threading.Tasks;
using NLog;
using UnityEngine;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	public enum CalloutRequestStatus
	{
		UnknownId,
		Queued,
		Playing,
		Finished,
	}

	/// <summary>
	/// Manages playback of callouts. Maintains a queue of callout requests. Supports prioritizing
	/// certain callouts over others and enforcing a minimum pause between two callouts.
	/// </summary>
	public class CalloutCoordinator : MonoBehaviour
	{
		[SerializeField]
		[Range(0f, 3f)]
		[Tooltip("How many seconds to pause after a callout before the next one can be started")]
		private float _pauseDuration = 0.5f;

		private readonly List<CalloutRequest> _calloutQ = new();
		private CancellationTokenSource _loopCts;
		private CancellationTokenSource _currentCalloutCts;
		private Task _loopTask;
		private TaskCompletionSource<bool> _waitForNewCalloutTcs;
		private int _requestCounter;
		private int _idOfCurrentlyPlayingRequest = -1;

		private static Logger Logger = LogManager.GetCurrentClassLogger();

		public void EnqueueCallout(CalloutRequest request, out int requestId)
		{
			request.Index = _requestCounter;
			requestId = _requestCounter;
			_requestCounter++;
			var i = _calloutQ.FindIndex(x => x.Priority < request.Priority);
			if (i != -1)
				_calloutQ.Insert(i, request);
			else
				_calloutQ.Add(request);
			_waitForNewCalloutTcs?.TrySetResult(true);
		}

		public void DequeueCallout(int requestId)
		{
			var status = GetRequestStatus(requestId);
			switch (status)
			{
				case CalloutRequestStatus.Queued:
					var index = _calloutQ.FindIndex(x => x.Index == requestId);
					_calloutQ.RemoveAt(index);
					break;
				case CalloutRequestStatus.Playing:
					_currentCalloutCts.Cancel();
					break;
				case CalloutRequestStatus.Finished:
					Logger.Info(
						$"Cannot dequeue callout request with id '{requestId}' because it already "
							+ "finished playing."
					);
					break;
				case CalloutRequestStatus.UnknownId:
					Logger.Error(
						$"Cannot dequeue callout request with id '{requestId}' because no such "
							+ "request was previously made."
					);
					break;
			}
		}

		public CalloutRequestStatus GetRequestStatus(int requestId)
		{
			if (requestId < 0 || requestId >= _requestCounter)
				return CalloutRequestStatus.UnknownId;
			if (_calloutQ.Any(x => x.Index == requestId))
				return CalloutRequestStatus.Queued;
			if (requestId == _idOfCurrentlyPlayingRequest)
				return CalloutRequestStatus.Playing;
			return CalloutRequestStatus.Finished;
		}

		private void OnEnable()
		{
			_loopCts = new();
			_loopTask = CalloutLoop(_loopCts.Token);
		}

		private async void OnDisable()
		{
			_loopCts.Cancel();
			_loopCts.Dispose();
			_waitForNewCalloutTcs?.TrySetCanceled();
			try
			{
				await _loopTask;
			}
			catch (OperationCanceledException) { }
		}

		private async Task CalloutLoop(CancellationToken ct)
		{
			while (true)
			{
				_calloutQ.RemoveAll(x => x.IsExpired());

				if (_calloutQ.Count == 0)
				{
					_waitForNewCalloutTcs = new();
					await _waitForNewCalloutTcs.Task;
				}

				var request = _calloutQ[0];
				_calloutQ.RemoveAt(0);

				_currentCalloutCts = new CancellationTokenSource();

				try
				{
					using var playCts = CancellationTokenSource.CreateLinkedTokenSource(
						ct,
						_currentCalloutCts.Token
					);

					_idOfCurrentlyPlayingRequest = request.Index;
					await Play(request, playCts.Token);
				}
				catch (OperationCanceledException)
				{
					// If only the current callout was canceled, continue, but if it's the whole
					// loop, throw
					ct.ThrowIfCancellationRequested();
				}
				finally
				{
					_idOfCurrentlyPlayingRequest = -1;
					_currentCalloutCts.Dispose();
				}

				await Task.Delay(TimeSpan.FromSeconds(_pauseDuration), ct);
			}
		}

		private async Task Play(CalloutRequest request, CancellationToken ct)
		{
			ct.ThrowIfCancellationRequested();
			var calloutGo = GetCalloutGameObject(request.CalloutAsset.name);
			var audioSource = calloutGo.AddComponent<AudioSource>();

			try
			{
				request.CalloutAsset.ConfigureAudioSource(audioSource);
				audioSource.volume *= request.Volume;
				audioSource.Play();
				await SoundAsset.WaitUntilAudioStops(audioSource, ct);
			}
			finally
			{
				if (audioSource != null)
				{
					if (Application.isPlaying)
						Destroy(audioSource);
					else
						DestroyImmediate(audioSource);
				}
				Destroy(calloutGo);
			}
		}

		private GameObject GetCalloutGameObject(string calloutName)
		{
			var calloutsGoName = $"Callout: {calloutName}";
			var calloutGo = new GameObject(calloutsGoName);
			calloutGo.transform.SetParent(transform, false);
			return calloutGo;
		}
	}
}
