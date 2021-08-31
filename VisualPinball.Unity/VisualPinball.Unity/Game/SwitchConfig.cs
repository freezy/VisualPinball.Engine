// Visual Pinball Engine
// Copyright (C) 2021 freezy and VPE Team
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

namespace VisualPinball.Unity
{
	public struct SwitchConfig
	{
		public readonly string SwitchId;
		public readonly int PulseDelay;
		public bool IsPulseSwitch;
		public bool IsNormallyClosed;

		public SwitchConfig(SwitchMapping switchMapping)
		{
			SwitchId = switchMapping.Id;
			IsPulseSwitch = false;
			IsNormallyClosed = switchMapping.IsNormallyClosed;
			PulseDelay = switchMapping.PulseDelay;
		}

		public SwitchConfig WithPulse(bool isPulseSwitch)
		{
			IsPulseSwitch = isPulseSwitch;
			return this;
		}

		public SwitchConfig WithDefault(SwitchDefault switchDefault)
		{
			if (switchDefault == SwitchDefault.Configurable) {
				return this;
			}
			IsNormallyClosed = switchDefault == SwitchDefault.NormallyClosed;
			return this;
		}
	}
}
