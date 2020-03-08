using Unity.Entities;
using UnityEngine;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Unity.VPT.Flipper;
using VisualPinball.Unity.VPT.Table;

namespace VisualPinball.Unity.Game
{
	public class TablePlayer : MonoBehaviour
	{
		public readonly TableApi TableApi = new TableApi();

		//public static StreamWriter DebugLog;

		private Table _table;
		private EntityManager _manager;

		private void Awake()
		{
			var tableComponent = gameObject.GetComponent<TableBehavior>();
			_table = tableComponent.CreateTable();
			_manager = World.DefaultGameObjectInjectionWorld.EntityManager;

			//DebugLog = File.CreateText("flipper.log");
		}

		private void Start()
		{
			// bootstrap table script(s)
			var tableScripts = GetComponents<VisualPinballScript>();
			foreach (var tableScript in tableScripts) {
				tableScript.OnAwake(TableApi);
			}

			// trigger init events now
			foreach (var i in TableApi.Initializables) {
				i.Init();
			}

			// link events from systems
			World.DefaultGameObjectInjectionWorld.GetOrCreateSystem<FlipperVelocitySystem>().OnRotated +=
				(sender, e) => TableApi.Flipper(e.EntityIndex)?.HandleEvent(e);
		}

		private void Update()
		{
			// flippers will be handled via script later, but until scripting works, do it here.
			if (Input.GetKeyDown("left shift")) {
				TableApi.Flipper("LeftFlipper")?.RotateToEnd();
			}
			if (Input.GetKeyUp("left shift")) {
				TableApi.Flipper("LeftFlipper")?.RotateToStart();
			}
			if (Input.GetKeyDown("right shift")) {
				TableApi.Flipper("RightFlipper")?.RotateToEnd();
			}
			if (Input.GetKeyUp("right shift")) {
				TableApi.Flipper("RightFlipper")?.RotateToStart();
			}
		}

		private void OnDestroy()
		{
			//DebugLog.Dispose();
		}
	}
}
