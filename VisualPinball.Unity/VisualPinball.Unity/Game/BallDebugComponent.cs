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
			_physicsEngine = GetComponentInParent<PhysicsEngine>();
			_playfield = GetComponentInParent<PlayfieldComponent>();
			_player = GetComponentInParent<Player>();

			_ltw = Physics.VpxToWorld;
			_wtl = Physics.WorldToVpx;

			var p1 = _ltw.MultiplyPoint(new Vector3(-100f, 100f, 0));
			var p2 = _ltw.MultiplyPoint(new Vector3(100f, 100f, 0));
			var p3 = _ltw.MultiplyPoint(new Vector3(100f, -100f, 0));
			_playfieldPlane.Set3Points(p1, p2, p3);
		}


		protected bool GetCursorPositionOnPlayfield(out float3 vpxPos, out float3 worldPos)
		{
			vpxPos = float3.zero;
			worldPos = float3.zero;
			if (!Camera.main) {
				return false;
			}

			var mouseOnScreenPos = Mouse.current.position.ReadValue();
			var ray = Camera.main.ScreenPointToRay(mouseOnScreenPos);

			if (_playfieldPlane.Raycast(ray, out var enter)) {
				worldPos = _playfield.transform.localToWorldMatrix.inverse.MultiplyPoint(ray.GetPoint(enter));
				vpxPos = _wtl.MultiplyPoint(worldPos);

				// todo check playfield bounds
				return true;
			}
			return false;
		}
	}
}
