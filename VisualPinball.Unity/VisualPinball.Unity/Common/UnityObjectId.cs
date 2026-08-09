// Visual Pinball Engine
//
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version.
//
// This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without
// even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
// General Public License for more details.
//
// You should have received a copy of the GNU General Public License along with this program. If not,
// see <https://www.gnu.org/licenses/>.

namespace VisualPinball.Unity
{
	/// <summary>
	/// A stable per-object id that callers can serialize and compare later to tell whether they are
	/// still looking at the same object - used to detect duplicated components.
	/// </summary>
	///
	/// <remarks>
	/// The id must be derived from the object itself, never handed out from a counter. Static state is
	/// wiped on every domain reload, so a counter reassigns ids after each script compile and every
	/// caller then concludes its object was duplicated.
	/// </remarks>
	public static class UnityObjectId
	{
		public static int Get(UnityEngine.Object obj)
		{
#if UNITY_6000_5_OR_NEWER
			// EntityId supersedes the deprecated instance id and, like it, stays stable for the
			// lifetime of the object, domain reloads included. Fold it into an int deterministically.
			var raw = UnityEngine.EntityId.ToULong(obj.GetEntityId());
			return (int)(raw ^ (raw >> 32));
#else
			return obj.GetInstanceID();
#endif
		}
	}
}
