using Unity.Entities;
using Unity.Entities.CodeGeneratedJobForEach;

namespace VisualPinball.Unity.VisualPinball.Unity.Game
{
	public abstract class SystemBaseStub
	{
		protected abstract void OnUpdate();
		protected virtual void OnCreate() { }
		protected virtual void OnStartRunning() { }
		protected virtual void OnDestroy() { }
		
		public void Update() { }

		public ComponentLookup<T> GetComponentLookup<T>(bool isReadOnly = false) where T : unmanaged, IComponentData => default;

		public T GetComponentData<T>(Entity entity) where T : unmanaged, IComponentData => default;

		public EntityManager EntityManager => default;

		public World World => default;
		
		protected internal LambdaSingleJobDescription Job => new LambdaSingleJobDescription();
		
		protected internal T GetComponent<T>(Entity entity) where T : unmanaged, IComponentData => EntityManager.GetComponentData<T>(entity);

		protected internal void SetComponent<T>(Entity entity, T component) where T : unmanaged, IComponentData => EntityManager.SetComponentData(entity, component);

		protected internal bool HasComponent<T>(Entity entity) where T : unmanaged, IComponentData => false;

		protected internal EntityQuery GetEntityQuery(params ComponentType[] componentTypes) => default;
		
		protected internal ForEachLambdaJobDescription Entities => new ForEachLambdaJobDescription();
		
		public new BufferLookup<T> GetBufferLookup<T>(bool isReadOnly = false) where T : unmanaged, IBufferElementData => default;
	}
}
