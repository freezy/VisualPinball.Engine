// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.

// ReSharper disable InconsistentNaming

using System;
using System.Collections.Generic;
using UnityEngine;
using VisualPinball.Engine.Game.Engines;
using NLog;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	[PackAs("ScoreReelDisplay")]
	[AddComponentMenu("Visual Pinball/Display/Score Reel Display")]
	[HelpURL("https://docs.visualpinball.org/creators-guide/manual/mechanisms/score-reels.html")]
	public class ScoreReelDisplayComponent : DisplayComponent, IPackable
	{
		#region Data

		[SerializeField]
		public string _id = "display0";

		public override string Id { get => _id; set => _id = value; }

		[Unit("positions/s")]
		[Tooltip("Positions per second.")]
		public float Speed = 15;

		[Unit("ms")]
		[Tooltip("Wait between positions in milliseconds.")]
		public float Wait = 30;

		[Tooltip("The reel components, from left to right.")]
		public ScoreReelComponent[] ReelObjects;

		[Tooltip("The score motor component to simulate EM reel timing.")]
		public ScoreMotorComponent ScoreMotorComponent;

		#endregion

		#region Packaging

		public byte[] Pack() => ScoreReelDisplayPackable.Pack(this);

		public byte[] PackReferences(Transform root, PackagedRefs refs, PackagedFiles files) => ScoreReelDisplayReferencesPackable.Pack(this, refs);

		public void Unpack(byte[] bytes) => ScoreReelDisplayPackable.Unpack(bytes, this);

		public void UnpackReferences(byte[] data, Transform root, PackagedRefs refs, PackagedFiles files) => ScoreReelDisplayReferencesPackable.Unpack(data, this, refs);

		#endregion

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		private float _score;

		private void Start()
		{
			foreach (var reelObject in ReelObjects) {
				reelObject.Speed = Speed;
				reelObject.Wait = Wait;
			}

			_score = 0;
		}

		public override void Clear()
		{
			if (ScoreMotorComponent) {
				// Truncate score to the amount of reels
				var value = (float)(_score % System.Math.Pow(10, ReelObjects.Length));

				ScoreMotorComponent.ResetScore(Id, value, (score) => {
					_score = score;
					UpdateFrame();
				});
			}
			else {
				_score = 0;
				UpdateFrame();
			}
		}

		public override void UpdateFrame(DisplayFrameFormat format, byte[] data)
		{
			var value = BitConverter.ToSingle(data);

			if (ScoreMotorComponent) {
				ScoreMotorComponent.AddPoints(Id, value, (points) => {
					_score += points;
					UpdateFrame();
				});
			}
			else {
				_score = value;
				UpdateFrame();
			}
		}

		private void UpdateFrame()
		{
			var score = _score;
			var tmp = score;

			for (var i = ReelObjects.Length - 1; i >= 0; i--) {
				ReelObjects[i].AnimateTo((int)tmp % 10);
				tmp /= 10;
			}

			OnDisplayChanged?.Invoke(this, new DisplayFrameData(Id, DisplayFrameFormat.Numeric, BitConverter.GetBytes(score)));
		}

		#region Unused

		protected override Material CreateMaterial()
		{
			throw new NotImplementedException();
		}

		public override void UpdateDimensions(int width, int height, bool flipX = false)
		{
			Debug.Log($"Reel of {width} requested.");
		}

		public override Color LitColor { get; set; }
		public override Color UnlitColor { get; set; }
		protected override float MeshWidth { get; }
		public override float MeshHeight { get; }
		protected override float MeshDepth { get; }
		public override float AspectRatio { get; set; }

		#endregion
	}
}
