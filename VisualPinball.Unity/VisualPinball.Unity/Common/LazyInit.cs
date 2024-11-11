using System;

namespace VisualPinball.Unity
{
	public class LazyInit<T>
	{
		private readonly Func<T> _constructor;
		private bool _isInitialized;
		private T _value;

		public ref T Ref {
			get {
				if (_isInitialized) {
					return ref _value;
				}
				_isInitialized = true;
				_value = _constructor();
				return ref _value;
			}
		}

		public LazyInit(Func<T> constructor)
		{
			_constructor = constructor;
		}
	}
}
