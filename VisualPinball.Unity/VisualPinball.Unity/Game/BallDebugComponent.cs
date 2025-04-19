using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace VisualPinball.Unity
{
	public abstract class BallDebugComponent : MonoBehaviour
	{
		protected PhysicsEngine _physicsEngine;
		protected PlayfieldComponent _playfield;
		protected Player _player;
		protected Matrix4x4 _ltw;
		protected Matrix4x4 _wtl;

		protected Plane _playfieldPlane;

		protected int _ballId = 0;

		private void Awake()
		{
			_playfield = GetComponentInChildren<PlayfieldComponent>();
			_player = GetComponentInChildren<Player>();

			_ltw = Physics.VpxToWorld;
			_wtl = Physics.WorldToVpx;

			var p1 = _ltw.MultiplyPoint(new Vector3(-100f, 100f, 0));
			var p2 = _ltw.MultiplyPoint(new Vector3(100f, 100f, 0));
			var p3 = _ltw.MultiplyPoint(new Vector3(100f, -100f, 0));
			_playfieldPlane.Set3Points(p1, p2, p3);
			_physicsEngine = GetComponentInChildren<PhysicsEngine>();
		}


		protected bool GetCursorPositionOnPlayfield(out float2 position)
		{
			if (!Camera.main) {
				position = float2.zero;
				return false;
			}

			var mouseOnScreenPos = Mouse.current.position.ReadValue();
			var ray = Camera.main.ScreenPointToRay(mouseOnScreenPos);

			if (_playfieldPlane.Raycast(ray, out var enter)) {
				var playfieldPosWorld = _playfield.transform.localToWorldMatrix.inverse.MultiplyPoint(ray.GetPoint(enter));
				var playfieldPosLocal = _wtl.MultiplyPoint(playfieldPosWorld);

				position = new float2(playfieldPosLocal.x, playfieldPosLocal.y);

				// todo check playfield bounds
				return true;
			}
			position = float2.zero;
			return false;
		}
	}
}
