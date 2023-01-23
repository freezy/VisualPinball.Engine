using Unity.Entities;
using UnityEngine;
using VisualPinball.Unity;

namespace VisualPinballUnity
{
	[UpdateInGroup(typeof(InitializationSystemGroup))]
	internal partial class InitPlayerSystem : SystemBase
	{
		protected override void OnCreate()
		{
			base.OnCreate();
			Debug.Log($"[INIT] Created.");
			
			Debug.Log($"[INIT] Initializing GameObjects...");
			Entities.WithoutBurst().ForEach((Entity entity, in FlipperBaker.GameObjectContainer goc) =>
			{
				
				//var tf = EntityManager.GetComponentObject<FlipperStaticData>(entity);
				Debug.Log($"[INIT] Initializing GameObject {goc.GameObject.name}...");

			}).Run();
		}

		protected override void OnUpdate()
		{

		}
	}
}
