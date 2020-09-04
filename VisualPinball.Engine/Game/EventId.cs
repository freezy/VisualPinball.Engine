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

namespace VisualPinball.Engine.Game
{
	public enum EventId
	{
		// Surface
		SurfaceEventsSlingshot = 1101, // DISPID_SurfaceEvents_Slingshot

		// Flipper
		FlipperEventsCollide = 1200, // DISPID_FlipperEvents_Collide

		// Spinner
		SpinnerEventsSpin = 1301, // DISPID_SpinnerEvents_Spin

		// HitTarget
		TargetEventsDropped = 1302, // DISPID_TargetEvents_Dropped
		TargetEventsRaised = 1303,  // DISPID_TargetEvents_Raised

		// Generic
		HitEventsHit = 1400, // DISPID_HitEvents_Hit
		HitEventsUnhit = 1401, // DISPID_HitEvents_Unhit
		LimitEventsEos = 1402, // DISPID_LimitEvents_EOS
		LimitEventsBos = 1403, // DISPID_LimitEvents_BOS
	}
}
