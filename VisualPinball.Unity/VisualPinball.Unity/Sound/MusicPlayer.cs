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
using UnityEngine;

namespace VisualPinball.Unity
{
    public enum MusicTransitionType
    {
        CrossFade,
        FadeOut,
    }

    public class MusicPlayer : MonoBehaviour
    {
        [SerializeField] private MusicTransitionType _transitionType;

        private readonly List<StackItem> _musicStack = new();
        private int _stackCounter = 0;

        public void InsertIntoStack(MusicAsset music, out int stackId)
        {
            stackId = _stackCounter;
            _stackCounter++;
            var stackItem = new StackItem(music, stackId);
            var index = _musicStack.FindIndex(x => x.MusicAsset.Priority <= stackItem.MusicAsset.Priority);

            if (index == -1)
                _musicStack.Insert(index, stackItem);
            else
                _musicStack.Add(stackItem);


        }

        public void RemoveFromStack(int stackId)
        {
            var index = _musicStack.FindIndex(x => x.StackId == stackId);
            if (index >= 0)
                _musicStack.RemoveAt(index);
        }

        private readonly struct StackItem
        {
            public readonly MusicAsset MusicAsset;
            public readonly int StackId;

            public StackItem(MusicAsset musicAsset, int stackid)
            {
                MusicAsset = musicAsset;
                StackId = stackid;
            }
        }
    }
}