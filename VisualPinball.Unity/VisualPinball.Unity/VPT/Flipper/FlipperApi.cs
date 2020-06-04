// ReSharper disable EventNeverSubscribedTo.Global
#pragma warning disable 67

using System;
using Unity.Entities;
using VisualPinball.Engine.Common;
using VisualPinball.Engine.VPT.Flipper;
using VisualPinball.Unity.Game;
using VisualPinball.Unity.Physics.Engine;

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
			EngineProvider<IPhysicsEngine>.Get().FlipperRotateToEnd(Entity);
		}

		public void RotateToStart()
		{
			EngineProvider<IPhysicsEngine>.Get().FlipperRotateToStart(Entity);
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
