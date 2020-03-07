using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Unity.Components;
using VisualPinball.Unity.Physics.Flipper;

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
			var tableComponent = gameObject.GetComponent<VisualPinballTable>();
			_table = tableComponent.CreateTable();
			_manager = World.DefaultGameObjectInjectionWorld.EntityManager;

			var tableScripts = GetComponents<VisualPinballScript>();
			foreach (var tableScript in tableScripts) {
				tableScript.OnAwake(TableApi);
			}

			//DebugLog = File.CreateText("flipper.log");
		}

		private void Update()
		{
			// all of this is hacky and only serves as proof of concept.
			// flippers will obviously be handled via script later.
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
