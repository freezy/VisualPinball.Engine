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

using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Used to request from <c>CalloutCoordinator</c> to play a callout.
	/// </summary>
	public struct CalloutRequest
	{
		public SoundPriority Priority { get; private set; }

		/// <summary>
		/// Set by <c>CalloutCoordinator</c> to uniquely identify this request so it can be removed
		/// later.
		/// </summary>
		public int Index { get; set; }

		public readonly CalloutAsset CalloutAsset;
		private readonly float _deadline;

		/// <summary>
		/// Create a callout
		/// </summary>
		/// <param name="calloutAsset">The callout asset to play</param>
		/// <param name="priority">Higher priority callouts will play before lower priority ones</param>
		/// <param name="maxQueueTime">How many seconds to wait in the queue before discarding the request. -1 = no limit</param>
		public CalloutRequest(
			CalloutAsset calloutAsset,
			SoundPriority priority,
			float maxQueueTime = -1f
		)
		{
			CalloutAsset = calloutAsset;
			Priority = priority;
			if (maxQueueTime != -1f)
				_deadline = Time.time + maxQueueTime;
			else
				_deadline = -1f;
			Index = -1;
		}

		public readonly bool IsExpired()
		{
			if (_deadline == -1f)
				return false;
			return Time.time > _deadline;
		}
	}
}
