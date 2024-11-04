using System;

namespace VisualPinball.Unity
{
	public class LazyInit<T>
	{
		private bool isInitialized = false;
		private T value;
		public ref T Ref
		{
			get
			{
				if (!isInitialized) {
					isInitialized = true;
					value = constructor();
				}
				return ref value;
			}
		}

		private readonly Func<T> constructor;

		public LazyInit(Func<T> constructor)
		{
			this.constructor = constructor;
		}
	}
}
