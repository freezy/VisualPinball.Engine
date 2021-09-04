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

using VisualPinball.Engine.VPT.HitTarget;

namespace VisualPinball.Unity.Editor
{
	public static class TargetExtensions
	{
		internal static IVpxPrefab InstantiatePrefab(this HitTarget hitTarget)
		{
			var prefab = hitTarget.Data.IsDropTarget
				? RenderPipeline.Current.PrefabProvider.CreateDropTarget(hitTarget.Data.TargetType)
				: RenderPipeline.Current.PrefabProvider.CreateHitTarget(hitTarget.Data.TargetType);

			return new VpxPrefab<HitTarget, HitTargetData, TargetAuthoring>(prefab, hitTarget);
		}
	}
}
