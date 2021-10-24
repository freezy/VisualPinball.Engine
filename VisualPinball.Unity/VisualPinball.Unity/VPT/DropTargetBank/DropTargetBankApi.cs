using System;
using Logger = NLog.Logger;
using NLog;
using UnityEngine;
using System.Collections.Generic;

namespace VisualPinball.Unity
{
	public class DropTargetBankApi : IApi, IApiCoilDevice
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		private readonly DropTargetBankComponent _dropTargetBankComponent;
		private Player _player;

		public DeviceCoil ResetCoil;

		public List<DropTargetApi> _dropTargetApis = new List<DropTargetApi>();

		public event EventHandler Init;

		internal DropTargetBankApi(GameObject go, Player player)
		{
			_dropTargetBankComponent = go.GetComponentInChildren<DropTargetBankComponent>();
			_player = player;
		}

		IApiCoil IApiCoilDevice.Coil(string deviceItem)
		{
			return deviceItem == _dropTargetBankComponent.name ? ResetCoil : null;
		}

		void IApi.OnInit(BallManager ballManager)
		{
			ResetCoil = new DeviceCoil(OnResetCoilEnabled);
			
			for (var index = 0; index < _dropTargetBankComponent.BankSize; index++)
			{
				_dropTargetApis.Add(_player.TableApi.DropTarget(_dropTargetBankComponent.DropTargets[index]));
			}

			Init?.Invoke(this, EventArgs.Empty);
		}

		private void OnResetCoilEnabled()
		{
			Logger.Info("OnResetCoilEnabled");

			foreach (var dropTargetApi in _dropTargetApis)
			{
				dropTargetApi.IsDropped = false;
			}
		}

		void IApi.OnDestroy()
		{
			Logger.Info("Destroying drop target bank!");
		}
	}
}

