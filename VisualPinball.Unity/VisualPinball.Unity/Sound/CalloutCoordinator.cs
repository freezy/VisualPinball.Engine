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
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace VisualPinball.Unity
{
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
		private Task _loopTask;
		private TaskCompletionSource<bool> _waitForNewCalloutTcs;

		public void EnqueueCallout(CalloutRequest callout)
		{
			var i = _calloutQ.FindIndex(x => x.Priority < callout.Priority);
			if (i != -1)
				_calloutQ.Insert(i, callout);
			else
				_calloutQ.Add(callout);
			_waitForNewCalloutTcs?.TrySetResult(true);
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

				var callout = _calloutQ[0];
				_calloutQ.RemoveAt(0);
				await callout.Play(gameObject, ct);
				await Task.Delay(TimeSpan.FromSeconds(_pauseDuration), ct);
			}
		}
	}
}
