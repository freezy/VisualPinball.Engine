// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
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

namespace VisualPinball.Unity
{
	/// <summary>
	/// The file isn't a .vpe package this build can load: it can't be opened as one, it has no
	/// manifest, it's from a newer version of VPE, or its scene is missing.
	/// </summary>
	///
	/// <remarks>
	/// That's a problem with the file, not with VPE, so callers tell the user and log a warning
	/// instead of an error. Failures further into loading stay ordinary exceptions, since they
	/// can as well be VPE's.
	/// </remarks>
	public sealed class InvalidPackageException : Exception
	{
		public InvalidPackageException(string message, Exception innerException = null) : base(message, innerException)
		{
		}
	}
}
