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
using VisualPinball.Engine.VPT.Spinner;

namespace VisualPinball.Unity
{
	internal class SpinnerPlateAuthoring : ItemAuthoring<Spinner, SpinnerData>, IConvertGameObjectToEntity
	{
		public override string DefaultDescription => "Spinner Plate";

		protected override string[] Children => new string[0];

		public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
		{
			Convert(entity, dstManager);

			dstManager.AddComponentData(entity, new SpinnerStaticData {
				AngleMax = math.radians(data.AngleMax),
				AngleMin = math.radians(data.AngleMin),
				Damping = math.pow(data.Damping, (float)PhysicsConstants.PhysFactor),
				Elasticity = data.Elasticity,
				Height = data.Height
			});

			dstManager.AddComponentData(entity, new SpinnerMovementData {
				Angle = math.radians(math.clamp(0.0f, data.AngleMin, data.AngleMax)),
				AngleSpeed = 0f
			});

			// register
			var spinner = transform.parent.gameObject.GetComponent<SpinnerAuthoring>().Item;
			transform.GetComponentInParent<Player>().RegisterSpinner(spinner, entity, gameObject);
		}

		protected override Spinner GetItem()
		{
			return transform.parent.gameObject.GetComponent<SpinnerAuthoring>().Item;
		}
	}
}
