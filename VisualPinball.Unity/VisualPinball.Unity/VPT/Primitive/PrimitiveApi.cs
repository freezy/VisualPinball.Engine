﻿using System;
using Unity.Entities;

namespace VisualPinball.Unity
{
	public class PrimitiveApi : ItemApi<Engine.VPT.Primitive.Primitive, Engine.VPT.Primitive.PrimitiveData>,
		IApiInitializable, IApiHittable
	{
		/// <summary>
		/// Event emitted when the table is started.
		/// </summary>
		public event EventHandler Init;

		/// <summary>
		/// Event emitted when the ball glides on the primitive.
		/// </summary>
		public event EventHandler Hit;

		internal PrimitiveApi(Engine.VPT.Primitive.Primitive item, Entity entity, Player player) : base(item, entity, player)
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
