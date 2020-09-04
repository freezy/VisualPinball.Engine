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

using System;
using Unity.Entities;
using VisualPinball.Engine.VPT.Kicker;

namespace VisualPinball.Unity
{
	public class KickerApi : ItemApi<Kicker, KickerData>, IApiInitializable, IApiHittable
	{

		/// <summary>
		/// Event emitted when the table is started.
		/// </summary>
		public event EventHandler Init;

		/// <summary>
		/// Event emitted when the ball moves into the kicker.
		/// </summary>
		public event EventHandler Hit;

		/// <summary>
		/// Event emitted when the ball leaves the kicker.
		/// </summary>
		public event EventHandler UnHit;

		public KickerApi(Kicker item, Entity entity, Player player) : base(item, entity, player)
		{
		}

		public BallApi CreateBall()
		{
			return Player.CreateBall(Item);
		}

		public BallApi CreateSizedBallWithMass(float radius, float mass)
		{
			return Player.CreateBall(Item, radius, mass);
		}

		public BallApi CreateSizedBall(float radius)
		{
			return Player.CreateBall(Item, radius);
		}

		#region Events

		void IApiInitializable.OnInit()
		{
			Init?.Invoke(this, EventArgs.Empty);
		}

		void IApiHittable.OnHit(bool isUnHit)
		{
			if (isUnHit) {
				UnHit?.Invoke(this, EventArgs.Empty);

			} else {
				Hit?.Invoke(this, EventArgs.Empty);
			}
		}

		#endregion
	}
}
