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

using System.Collections.Generic;
using System.Linq;
using NLog;
using UnityEngine;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	public enum MusicRequestStatus
	{
		UnknownId,
		Waiting,
		Playing,
		Finished,
	}

	/// <summary>
	/// Manages music playback using a stack. Other scripts can add requests to the stack.
	/// The stack is sorted based on priority and age of the requests. The topmost request is
	/// played. Music fades out when stopped. New music fades in if other music is playing.
	/// </summary>
	[PackAs("MusicCoordinator")]
	public class MusicCoordinator : MonoBehaviour, IPackable
	{
		#region Data

		[Tooltip("How many seconds should transitions between songs take?")]
		[Range(0f, 10f)]
		public float FadeDuration = 3f;

		#endregion

		#region Packaging

		public byte[] Pack() => MusicCoordinatorPackable.Pack(this);

		public byte[] PackReferences(Transform root, PackagedRefs refs, PackagedFiles files) => null;

		public void Unpack(byte[] bytes) => MusicCoordinatorPackable.Unpack(bytes, this);

		public void UnpackReferences(byte[] data, Transform root, PackagedRefs refs, PackagedFiles files) { }

		#endregion

		private readonly List<MusicPlayer> _players = new();
		private readonly List<MusicRequest> _requestStack = new();
		private int _requestCounter;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		/// <summary>
		/// Insert a request into the request stack
		/// </summary>
		/// <param name="request"></param>
		/// <param name="requestId">A unique identifier that can be used to remove the request later</param>
		public void AddRequest(MusicRequest request, out int requestId)
		{
			request.Index = _requestCounter;
			requestId = request.Index;
			_requestCounter++;
			_requestStack.Add(request);

			EvaluateRequestStack();
		}

		/// <summary>
		/// Remove a request that was previously added using <c>AddRequest</c>
		/// </summary>
		/// <param name="requestId"></param>
		public void RemoveRequest(int requestId)
		{
			var i = _requestStack.FindIndex(x => x.Index == requestId);
			if (i != -1)
				_requestStack.RemoveAt(i);
			else
				Logger.Error(
					$"Can't remove music request with id '{requestId}' because there is no such "
						+ "request in the stack."
				);

			EvaluateRequestStack();
		}

		public MusicRequestStatus GetRequestStatus(int requestId)
		{
			if (requestId < 0 || requestId >= _requestCounter)
				return MusicRequestStatus.UnknownId;
			if (_requestStack.Any(x => x.Index == requestId))
				return MusicRequestStatus.Waiting;
			if (_requestStack.Count > 0 && requestId == _requestStack[0].Index)
				return MusicRequestStatus.Playing;
			return MusicRequestStatus.Finished;
		}

		private void EvaluateRequestStack()
		{
			_players.RemoveAll(x => x == null);
			if (_requestStack.Count > 0)
			{
				_requestStack.Sort();
				var requestToPlay = _requestStack[0];
				var musicToPlay = requestToPlay.MusicAsset;
				var playerToPlay = _players.FirstOrDefault(x => x.MusicAsset == musicToPlay);
				if (playerToPlay == default)
				{
					var musicGo = GetMusicGameObject(musicToPlay.name);
					playerToPlay = musicGo.AddComponent<MusicPlayer>();
					playerToPlay.Init(
						musicToPlay,
						FadeDuration,
						MusicPlayer.AfterStopAction.DeleteGameObject
					);
					_players.Add(playerToPlay);
				}

				// No need to fade in if nothing else is playing
				playerToPlay.StartAtFullVolume = !_players.Any(x => x.IsPlaying);
				playerToPlay.RequestVolume = requestToPlay.Volume;
				_players.ForEach(x => x.ShouldPlay = x == playerToPlay);
			}
			else
				_players.ForEach(x => x.ShouldPlay = false);
		}

		private GameObject GetMusicGameObject(string musicName)
		{
			const string musicGoName = "Music";
			var musicParent = transform.Find(musicGoName);
			if (musicParent == null)
			{
				musicParent = new GameObject(musicGoName).transform;
				musicParent.SetParent(transform, false);
			}

			var musicGo = new GameObject(musicName);
			musicGo.transform.SetParent(musicParent, false);
			return musicGo;
		}
	}
}
