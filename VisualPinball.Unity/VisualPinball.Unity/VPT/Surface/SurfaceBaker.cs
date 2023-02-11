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

using VisualPinball.Engine.VPT.Surface;

namespace VisualPinball.Unity
{
	public class SurfaceBaker : ItemBaker<SurfaceComponent, SurfaceData>
	{
		public override void Bake(SurfaceComponent authoring)
		{
			base.Bake(authoring);
			
			// physics collision data
			var collComponent = GetComponentInChildren<SurfaceColliderComponent>();
			if (collComponent) {
				AddComponent(new LineSlingshotData {
					IsDisabled = false,
					Threshold = collComponent.SlingshotThreshold,
				});
			}

			GetComponentInParent<Player>().RegisterSurface(authoring);
		}
	}
}
