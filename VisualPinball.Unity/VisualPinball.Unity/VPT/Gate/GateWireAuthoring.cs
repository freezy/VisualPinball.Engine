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
using Unity.Mathematics;
using VisualPinball.Engine.Common;
using VisualPinball.Engine.VPT.Gate;

namespace VisualPinball.Unity
{
	public class GateWireAuthoring : ItemMainAuthoring<Gate, GateData>, IConvertGameObjectToEntity
	{
		public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
		{
			Convert(entity, dstManager);

			dstManager.AddComponentData(entity, new GateStaticData {
				AngleMin = Data.AngleMin,
				AngleMax = Data.AngleMax,
				Height = Data.Height,
				Damping = math.pow(Data.Damping, (float)PhysicsConstants.PhysFactor),
				GravityFactor = Data.GravityFactor,
				TwoWay = Data.TwoWay
			});
			dstManager.AddComponentData(entity, new GateMovementData {
				Angle = Data.AngleMin,
				AngleSpeed = 0,
				ForcedMove = false,
				IsOpen = false
			});

			// register
			var gate = transform.parent.gameObject.GetComponent<GateAuthoring>().Item;
			transform.GetComponentInParent<Player>().RegisterGate(gate, entity, gameObject);
		}

		protected override Gate InstantiateItem(GateData data)
		{
			return transform.parent.gameObject.GetComponent<GateAuthoring>().Item;
		}
	}
}
