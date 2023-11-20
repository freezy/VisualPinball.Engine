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

using VisualPinball.Engine.VPT;

namespace VisualPinball.Unity
{
	public abstract class AnimationComponent<TData, TMainComponent> : SubComponent<TData, TMainComponent>,
		IAnimationComponent
		where TData : ItemData
		where TMainComponent : MainRenderableComponent<TData>
	{
		public void UpdateTransforms() => MainComponent.UpdateTransforms();

		private void Awake()
		{
			// todo remove when all animation components are translated through their main component
			RegisterPhysics();
		}
	}
}
