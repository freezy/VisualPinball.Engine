// ReSharper disable EventNeverSubscribedTo.Global
#pragma warning disable 67

using System;
using Unity.Entities;
using VisualPinball.Engine.VPT.Flipper;
using VisualPinball.Unity.Game;

namespace VisualPinball.Unity.VPT.Flipper
{
	public class FlipperApi : ItemApi<Engine.VPT.Flipper.Flipper, FlipperData>, IApiInitializable
	{
		public event EventHandler Collide;
		public event EventHandler Hit;
		public event EventHandler Init;
		public event EventHandler<RotationEventArgs> LimitBos;
		public event EventHandler<RotationEventArgs> LimitEos;
		public event EventHandler Timer;

		public FlipperApi(Engine.VPT.Flipper.Flipper flipper, Entity entity, Player player) : base(flipper, entity, player)
		{
		}

		public void RotateToEnd()
		{
			var mData = EntityManager.GetComponentData<FlipperMovementData>(Entity);
			mData.EnableRotateEvent = 1;
			EntityManager.SetComponentData(Entity, mData);
			EntityManager.SetComponentData(Entity, new SolenoidStateData { Value = true });
			DPProxy.OnRotateToEnd(Entity);
		}

		public void RotateToStart()
		{
			var mData = EntityManager.GetComponentData<FlipperMovementData>(Entity);
			mData.EnableRotateEvent = -1;
			EntityManager.SetComponentData(Entity, mData);
			EntityManager.SetComponentData(Entity, new SolenoidStateData { Value = false });
			DPProxy.OnRotateToStart(Entity);
		}

		internal void HandleEvent(FlipperRotatedEvent rotatedEvent)
		{
			if (rotatedEvent.Direction) {
				LimitBos?.Invoke(this, new RotationEventArgs { AngleSpeed = rotatedEvent.AngleSpeed });
			} else {
				LimitEos?.Invoke(this, new RotationEventArgs { AngleSpeed = rotatedEvent.AngleSpeed });
			}
		}

		void IApiInitializable.Init()
		{
			Init?.Invoke(this, EventArgs.Empty);
		}
	}

	public class RotationEventArgs
	{
		public float AngleSpeed;
	}
}
