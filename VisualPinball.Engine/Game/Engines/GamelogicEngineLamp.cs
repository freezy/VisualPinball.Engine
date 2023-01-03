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
using VisualPinball.Engine.Math;

namespace VisualPinball.Engine.Game.Engines
{
	[Serializable]
	public class GamelogicEngineLamp : IGamelogicEngineDeviceItem
	{
		/// <summary>
		/// The unique ID of this lamp, as the gamelogic engine addresses it.
		/// </summary>
		public virtual string Id { get => _id; set => _id = value; }

		/// <summary>
		/// An optional description of the lamp.
		/// </summary>
		public virtual string Description { get => _description; set => _description = value; }

		/// <summary>
		/// Which channel this lamp corresponds to.
		/// </summary>
		public ColorChannel Channel = ColorChannel.Alpha;

		/// <summary>
		/// If it's a fading light, this is the value at maximal intensity.
		/// </summary>
		public int FadingSteps;

		/// <summary>
		/// How the gamelogic engine triggers the lamp. Either through GI or through the normal lamp API.
		/// </summary>
		///
		/// <remarks>
		/// Note that lamps connected to coils will appear under coils.
		/// </remarks>
		public LampSource Source = LampSource.Lamp;

		/// <summary>
		/// Which type this lamp is.
		/// </summary>
		public LampType Type = LampType.SingleOnOff;

		/// <summary>
		/// A regular expression to match the component on the playfield.
		/// </summary>
		public string DeviceHint { get => _deviceHint; set => _deviceHint = value; }

		/// <summary>
		/// A regular expression to match the lamp component within the component.
		/// </summary>
		public string DeviceItemHint { get => _deviceItemHint; set => _deviceItemHint = value; }

		public int NumMatches { get => _numMatches; set => _numMatches = value; }

		private string _description;
		private string _id;
		private string _deviceHint;
		private string _deviceItemHint;
		private int _numMatches = 1;

		public GamelogicEngineLamp(string id)
		{
			Id = id;
		}

		public GamelogicEngineLamp(int id)
		{
			Id = id.ToString();
		}
	}

	public enum LampSource
	{
		Lamp = 0,
		GI = 1,
	}

	public enum LampType
	{
		SingleOnOff = 0,
		SingleFading = 1,
		RgbMulti = 2,
		Rgb = 3,
	}

	public enum LampStatus
	{
		Off = 0,
		On = 1,
		Blinking = 2,
	}

	public class LampState
	{
		public LampStatus Status {
			get => _status;
			set {
				if (value != LampStatus.Off) {
					_lastOnStatus = value;
				}
				_status = value;
			}
		}

		public bool IsOn {
			get => _status is LampStatus.On or LampStatus.Blinking;
			set => _status = value ? _lastOnStatus : LampStatus.Off;
		}

		public float Intensity {
			get => Color.A;
			set => Color = Color.WithAlpha(value);
		}

		public Color Color;

		private LampStatus _status;
		private LampStatus _lastOnStatus = LampStatus.On;

		public LampState(LampStatus status, Color color)
		{
			Status = status;
			Color = color;
		}

		public LampState(float intensity)
		{
			if (intensity == 0f) {
				_status = LampStatus.Off;
				Color = new Color(255, 255, 255, 255);

			} else {
				_status = LampStatus.On;
				Color = new Color(255, 255, 255, (int)(intensity * 255));
			}
		}

		public LampState(Color color)
		{
			_status = color.A > 0 ? LampStatus.On : LampStatus.Off;
			Color = color;
		}

		public void SetChannel(ColorChannel channel, float value)
		{
			switch (channel) {

				case ColorChannel.Red:
					Color.R = value;
					break;
				case ColorChannel.Green:
					Color.G = value;
					break;
				case ColorChannel.Blue:
					Color.B = value;
					break;
				case ColorChannel.Alpha:
					Color.A = value;
					break;

				default:
					throw new ArgumentOutOfRangeException(nameof(channel), channel, null);
			}
		}

		public override string ToString()
		{
			return $"[{_status}] {Color}";
		}

		public static LampState Default => new LampState(LampStatus.Off, Colors.White);
	}
}
