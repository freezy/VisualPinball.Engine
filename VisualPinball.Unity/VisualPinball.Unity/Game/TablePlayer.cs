using System;
using System.Collections.Generic;
using System.IO;
using Unity.Entities;
using UnityEngine;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Unity.Components;
using VisualPinball.Unity.Physics.Flipper;

namespace VisualPinball.Unity.Game
{
	public class TablePlayer : MonoBehaviour
	{
		public readonly Dictionary<string, Entity> FlipperEntities = new Dictionary<string, Entity>();
		//public static StreamWriter DebugLog;

		private Table _table;
		private Player _player;

		private EntityManager _manager;


		private void Awake()
		{
			var tableComponent = gameObject.GetComponent<VisualPinballTable>();
			_table = tableComponent.CreateTable();
			_player = new Player(_table).Init();

			_manager = World.DefaultGameObjectInjectionWorld.EntityManager;
			//DebugLog = File.CreateText("flipper.log");
		}

		private void Update()
		{
			// all of this is hacky and only serves as proof of concept.
			// flippers will obviously be handled via script later.
			if (Input.GetKeyDown("left shift") && FlipperEntities.ContainsKey("LeftFlipper")) {
				_manager.SetComponentData(FlipperEntities["LeftFlipper"], new SolenoidStateData { Value = true });
			}
			if (Input.GetKeyUp("left shift") && FlipperEntities.ContainsKey("LeftFlipper")) {
				_manager.SetComponentData(FlipperEntities["LeftFlipper"], new SolenoidStateData { Value = false });
			}
			if (Input.GetKeyDown("right shift") && FlipperEntities.ContainsKey("RightFlipper")) {
				_manager.SetComponentData(FlipperEntities["RightFlipper"], new SolenoidStateData { Value = true });
			}
			if (Input.GetKeyUp("right shift") && FlipperEntities.ContainsKey("RightFlipper")) {
				_manager.SetComponentData(FlipperEntities["RightFlipper"], new SolenoidStateData { Value = false });
			}
		}

		private void OnDestroy()
		{
			//DebugLog.Dispose();
		}
	}
}
