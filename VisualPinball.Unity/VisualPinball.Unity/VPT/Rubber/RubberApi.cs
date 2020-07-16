﻿using System;
using Unity.Entities;
using VisualPinball.Unity.Game;

namespace VisualPinball.Unity.VPT.Rubber
{
	public class RubberApi : ItemApi<Engine.VPT.Rubber.Rubber, Engine.VPT.Rubber.RubberData>, IApiInitializable, IApiHittable
	{
		/// <summary>
		/// Event triggered when the table is started.
		/// </summary>
		public event EventHandler Init;

		/// <summary>
		/// Event triggered when the ball hits the rubber.
		/// </summary>
		public event EventHandler Hit;

		internal RubberApi(Engine.VPT.Rubber.Rubber item, Entity entity, Player player) : base(item, entity, player)
		{
		}

		#region Events

		void IApiInitializable.OnInit()
		{
			Init?.Invoke(this, EventArgs.Empty);
		}

		void IApiHittable.OnHit(bool _)
		{
			Hit?.Invoke(this, EventArgs.Empty);
		}

		#endregion
	}
}
