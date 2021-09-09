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

using UnityEngine;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Components implementing this interface can be referenced by <see cref="IOnSurfaceComponent"/>
	/// </summary>
	public interface ISurfaceComponent : IIdentifiableItemComponent
	{
		float Height(Vector2 pos);
	}

	/// <summary>
	/// Components implementing this interface can be placed on <see cref="ISurfaceComponent"/>.
	/// </summary>
	public interface IOnSurfaceComponent
	{
		ISurfaceComponent Surface { get; }

		void OnSurfaceUpdated();
	}

	public interface IOnPlayfieldComponent
	{
		void OnPlayfieldHeightUpdated();
	}
}
