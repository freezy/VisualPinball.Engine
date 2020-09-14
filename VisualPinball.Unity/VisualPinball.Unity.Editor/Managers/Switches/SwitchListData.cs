// Visual Pinball Engine
// Copyright (C) 2020 freezy and VPE Team
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

namespace VisualPinball.Unity.Editor
{
	public enum SwitchSource
	{
		InputSystem = 0,
		Playfield = 1,
		Constant = 2
	}

	public enum SwitchConstant
	{
		NC = 0,
		NO = 1
	}

	public enum SwitchType
	{
		OnOff = 0,
		Pulse = 1
	}

	public enum SwitchEvent
	{
		None = 0,
		KeyDown = 1,
		KeyUp = 2,
		Hit = 3,
		UnHit = 4
	}

	public class SwitchListData : IManagerListData
	{
		[ManagerListColumn(Order = 0, HeaderName = "ID", Width = 120)]
		public string Name => ID;

		[ManagerListColumn(Order = 1, HeaderName = "Description", Width = 120)]
		public string Description;

		[ManagerListColumn(Order = 2, HeaderName = "Source", Width = 120)]
		public SwitchSource Source = SwitchSource.Constant;

		[ManagerListColumn(Order = 3, HeaderName = "Element", Width = 150)]
		public string Element;

		[ManagerListColumn(Order = 4, HeaderName = "Type", Width = 100)]
		public SwitchType Type = SwitchType.OnOff;

		[ManagerListColumn(Order = 5, HeaderName = "Trigger", Width = 100)]
		public SwitchEvent Trigger;

		[ManagerListColumn(Order = 6, HeaderName = "Off", Width = 100)]
		public string Off = "";

		public string ID;
		public SwitchConstant Constant = SwitchConstant.NC;
		public int Pulse = 10;

		public SwitchListData()
		{
			ID = "";
		}

		public SwitchListData(string id, ISwitchableAuthoring item)
		{
			ID = id;
			Source = SwitchSource.Playfield;
			Element = item.Name;
			
			if (item is BumperAuthoring)
			{
				Description = "Bumper";
			}
			else if (item is FlipperAuthoring)
			{
				Description = "Flipper";
			}
			else if (item is GateAuthoring)
			{
				Description = "Gate";
			}
			else if (item is HitTargetAuthoring)
			{
				Description = "Target";
			}
			else if (item is KickerAuthoring)
			{
				Description = "Kicker";
			}
			else if (item is PrimitiveAuthoring)
			{
				Description = "Primitive";
			}
			else if (item is RubberAuthoring)
			{
				Description = "Rubber";
			}
			else if (item is SurfaceAuthoring)
			{
				Description = "Surface";
			}
			else if (item is TriggerAuthoring)
			{
				Description = "Trigger";
			}
			else if (item is SpinnerAuthoring)
			{
				Description = "Spinner";
			}

			if (item is KickerAuthoring || item is TriggerAuthoring)
			{
				Type = SwitchType.OnOff;
			}
			else
			{
				Type = SwitchType.Pulse;
			}
		}
	}
}
