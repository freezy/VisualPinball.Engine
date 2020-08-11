﻿using System;
using Unity.Entities;
using VisualPinball.Engine.VPT.Kicker;
using VisualPinball.Unity.Game;
using VisualPinball.Unity.VPT.Ball;

namespace VisualPinball.Unity.VPT.Kicker
{
	public class KickerApi : ItemApi<Engine.VPT.Kicker.Kicker, KickerData>, IApiInitializable, IApiHittable
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

		public KickerApi(Engine.VPT.Kicker.Kicker item, Entity entity, Player player) : base(item, entity, player)
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
