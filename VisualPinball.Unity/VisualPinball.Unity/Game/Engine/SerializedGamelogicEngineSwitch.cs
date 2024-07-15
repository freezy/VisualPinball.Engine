// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
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

// ReSharper disable InconsistentNaming

using System;
using UnityEngine;
using VisualPinball.Engine.Game.Engines;

namespace VisualPinball.Unity
{
    [Serializable]
    public class SerializedGamelogicEngineSwitch : GamelogicEngineSwitch, ISerializationCallbackReceiver
    {
        public SerializedGamelogicEngineSwitch(string id) : base(id) { }
        public SerializedGamelogicEngineSwitch(int id) : base(id) { }

        [SerializeField] private string _serializedDescription;
        [SerializeField] private string _serializedId;
        [SerializeField] private string _serializedDeviceHint;
        [SerializeField] private string _serializedDeviceItemHint;
        [SerializeField] private int _serializedNumMatches = 1;

        public void OnBeforeSerialize()
        {
            _serializedDescription = _description;
            _serializedId = _id;
            _serializedDeviceHint = _deviceHint;
            _serializedDeviceItemHint = _deviceItemHint;
            _serializedNumMatches = _numMatches;
        }

        public void OnAfterDeserialize()
        {
            _description = _serializedDescription;
            _id = _serializedId;
            _deviceHint = _serializedDeviceHint;
            _deviceItemHint = _serializedDeviceItemHint;
            _numMatches = _serializedNumMatches;
        }
    }
}
