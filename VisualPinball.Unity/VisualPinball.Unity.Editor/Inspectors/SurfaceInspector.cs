
using UnityEditor;
using VisualPinball.Unity.VPT.Surface;

namespace VisualPinball.Unity.Editor.Inspectors
{
	[CustomEditor(typeof(SurfaceBehavior))]
	public class SurfaceInspector : ItemInspector
	{
		private SurfaceBehavior _surface;

		protected virtual void OnEnable()
		{
			_surface = (SurfaceBehavior)target;
		}

		public override void OnInspectorGUI()
		{
			base.OnInspectorGUI();
		}
	}
}
