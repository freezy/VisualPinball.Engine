// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using VisualPinball.Engine.Game.Engines;

namespace VisualPinball.Unity.Samples.DmdStudio
{
	/// <summary>
	/// Minimal custom gamelogic engine showing the complete DMD Studio runtime lifecycle.
	/// </summary>
	[DisallowMultipleComponent]
	[AddComponentMenu("Pinball/Gamelogic Engine/DMD Studio Sample")]
	public sealed class DmdStudioSampleGamelogicEngine : MonoBehaviour, IGamelogicEngine,
		IDisplayFrameFormatPreference
	{
		[SerializeField] private DmdProjectAsset _dmdProject;
		[SerializeField] private string _baseCueId = "score";

		private DmdCuePlayer _dmd;

		public string Name => "DMD Studio Sample";
		public GamelogicEngineSwitch[] RequestedSwitches { get; } = Array.Empty<GamelogicEngineSwitch>();
		public GamelogicEngineLamp[] RequestedLamps { get; } = Array.Empty<GamelogicEngineLamp>();
		public GamelogicEngineCoil[] RequestedCoils { get; } = Array.Empty<GamelogicEngineCoil>();
		public GamelogicEngineWire[] AvailableWires { get; } = Array.Empty<GamelogicEngineWire>();

#pragma warning disable CS0067
		public event EventHandler<CoilEventArgs> OnCoilChanged;
		public event EventHandler<LampEventArgs> OnLampChanged;
		public event EventHandler<LampsEventArgs> OnLampsChanged;
		public event EventHandler<SwitchEventArgs2> OnSwitchChanged;
#pragma warning restore CS0067
		public event EventHandler<RequestedDisplays> OnDisplaysRequested;
		public event EventHandler<string> OnDisplayClear;
		public event EventHandler<DisplayFrameData> OnDisplayUpdateFrame;
		public event EventHandler<EventArgs> OnStarted;

		public Task OnInit(Player player, TableApi tableApi, BallManager ballManager, CancellationToken ct)
		{
			ct.ThrowIfCancellationRequested();
			if (_dmdProject == null) {
				Debug.LogError("DMD Studio Sample requires a DmdProjectAsset.", this);
				return Task.CompletedTask;
			}

			DisposePlayer();
			_dmd = new DmdCuePlayer(_dmdProject, new GleDisplayEmitter(
				displays => OnDisplaysRequested?.Invoke(this, displays),
				frame => OnDisplayUpdateFrame?.Invoke(this, frame),
				id => OnDisplayClear?.Invoke(this, id)));
			if (!string.IsNullOrWhiteSpace(_baseCueId)) {
				_dmd.SetBase(_baseCueId);
			}
			_dmd.Start();
			OnStarted?.Invoke(this, EventArgs.Empty);
			return Task.CompletedTask;
		}

		private void Update()
		{
			_dmd?.Tick(Time.timeAsDouble);
		}

		private void OnDestroy()
		{
			DisposePlayer();
		}

		public CueHandle PlayCue(string cueId, DmdParams parameters = null)
		{
			return _dmd != null ? _dmd.Play(cueId, parameters) : default;
		}

		public bool UpdateCue(string cueIdOrKey, DmdParams parameters)
		{
			return _dmd != null && _dmd.UpdateCue(cueIdOrKey, parameters);
		}

		public void SetBaseCue(string cueId, DmdParams parameters = null)
		{
			_dmd?.SetBase(cueId, parameters);
		}

		public void RequestDisplayFrameFormat(string displayId, DisplayFrameFormat format)
		{
			var projectDisplayId = string.IsNullOrWhiteSpace(_dmdProject?.DisplayId)
				? "dmd0"
				: _dmdProject.DisplayId;
			if (_dmd != null && string.Equals(displayId, projectDisplayId,
				    StringComparison.OrdinalIgnoreCase)) {
				_dmd.RequestFormat(format);
			}
		}

		private void DisposePlayer()
		{
			_dmd?.Dispose();
			_dmd = null;
		}

		public void DisplayChanged(DisplayFrameData displayFrameData) { }
		public void Switch(string id, bool isClosed) { }
		public void SetCoil(string id, bool isEnabled) { }
		public void SetLamp(string id, float value, bool isCoil = false, LampSource source = LampSource.Lamp) { }
		public bool GetSwitch(string id) => false;
		public bool GetCoil(string id) => false;
		public LampState GetLamp(string id) => default;
	}
}
