// Visual Pinball Engine
// Copyright (C) 2021 freezy and VPE Team
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

using System;
using UnityEngine;
using VisualPinball.Engine.VPT.Decal;
using VisualPinball.Engine.VPT.DispReel;
using VisualPinball.Engine.VPT.Flasher;
using VisualPinball.Engine.VPT.LightSeq;
using VisualPinball.Engine.VPT.TextBox;
using VisualPinball.Engine.VPT.Timer;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Legacy in VPE is data from Visual Pinball 10 that isn't used in VPE,
	/// but still available to export.
	/// </summary>
	[Serializable]
	public class LegacyContainer
	{
		[HideInInspector] public DecalData[] decals;
		[HideInInspector] public DispReelData[] dispReels;
		[HideInInspector] public FlasherData[] flashers;
		[HideInInspector] public LightSeqData[] lightSeqs;
		[HideInInspector] public TextBoxData[] textBoxes;
		[HideInInspector] public TimerData[] timers;
	}
}
