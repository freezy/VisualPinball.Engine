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
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
    public class MusicCoordinator : MonoBehaviour
    {
        [Tooltip("How many seconds should transitions between songs take?")]
        [SerializeField][Range(0f, 10f)] private float _fadeDuration = 3f;

        private readonly List<MusicPlayer> _players = new();
        private readonly List<MusicRequest> _requestStack = new();
        private int _requestCounter;

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Insert a request into the request stack
        /// </summary>
        /// <param name="musicAsset">The requested music</param>
        /// <param name="requestId">A unique identifier that can be used to remove the request later</param>
        /// <param name="priority">The priority of the request</param>
        public void AddRequest(MusicAsset musicAsset, out int requestId, SoundPriority priority = SoundPriority.Medium)
        {
            var request = new MusicRequest(musicAsset, _requestCounter, priority);
            requestId = request.Id;
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
            var i = _requestStack.FindIndex(x => x.Id == requestId);
            if (i != -1)
                _requestStack.RemoveAt(i);
            else
                Logger.Error(
                    $"Can't remove music request with id '{requestId}' because there is no such "
                    + "request in the stack.");
            EvaluateRequestStack();
        }

        private void EvaluateRequestStack()
        {
            _players.RemoveAll(x => x == null);
            if (_requestStack.Count > 0)
            {
                _requestStack.Sort();
                var musicToPlay = _requestStack[0].MusicAsset;
                var playerToPlay = _players.FirstOrDefault(x => x.MusicAsset == musicToPlay);
                if (playerToPlay == default)
                {
                    playerToPlay = gameObject.AddComponent<MusicPlayer>();
                    playerToPlay.Init(musicToPlay, _fadeDuration);
                    _players.Add(playerToPlay);
                }

                // No need to fade in if nothing else is playing
                playerToPlay.StartAtFullVolume = !_players.Any(x => x.IsPlaying);
                _players.ForEach(x => x.ShouldPlay = x == playerToPlay);
            }
            else
                _players.ForEach(x => x.ShouldPlay = false);
        }

        private readonly struct MusicRequest : IComparable<MusicRequest>
        {
            public MusicRequest(MusicAsset musicAsset, int id, SoundPriority priority = SoundPriority.Medium)
            {
                MusicAsset = musicAsset;
                Priority = priority;
                Time = DateTime.Now;
                Id = id;
            }

            public readonly MusicAsset MusicAsset;
            public readonly SoundPriority Priority;
            public readonly DateTime Time;
            public readonly int Id;

            // Used to sort the request stack to determine which request to play
            public int CompareTo(MusicRequest other)
            {
                if (Priority != other.Priority)
                    return other.Priority.CompareTo(Priority);
                // If priority is the same, favor newer requests
                return other.Time.CompareTo(Time);
            }
        }
    }
}