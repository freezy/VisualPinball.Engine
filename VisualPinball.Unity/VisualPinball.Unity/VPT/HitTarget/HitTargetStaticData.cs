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

using Unity.Entities;

namespace VisualPinball.Unity
{
	internal struct HitTargetStaticData : IComponentData
	{
		public int TargetType;
		public float DropSpeed;
		public float RaiseDelay;
		public float RotZ;
		public bool UseHitEvent;

		// table data
		public float TableScaleZ;

		public bool IsDropTarget => TargetType == Engine.VPT.TargetType.DropTargetBeveled
				|| TargetType == Engine.VPT.TargetType.DropTargetFlatSimple
				|| TargetType == Engine.VPT.TargetType.DropTargetSimple;
	}
}
