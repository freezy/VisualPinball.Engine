// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace VisualPinball.Unity
{
	public enum CuePriority
	{
		Base = 0,
		Status = 1,
		Award = 2,
		Mode = 3,
		Critical = 4,
		System = 5,
	}

	public enum CueInterruptPolicy
	{
		Replace,
		Queue,
		NonInterruptible,
	}

	public enum CueReturnPolicy
	{
		Resume,
		Restart,
		Discard,
	}

	public enum DmdDirection
	{
		Left,
		Right,
		Up,
		Down,
	}

	public enum DmdTransitionType
	{
		Cut,
		Push,
		Cover,
		Uncover,
		WipeOn,
		SplitIn,
		SplitOut,
		Dissolve,
		FadeThroughBlack,
		ScrollOff,
	}

	[Serializable]
	public struct DmdTransitionSpec
	{
		public DmdTransitionType Type;
		public DmdDirection Direction;
		public int DurationFrames;
	}

	[Serializable]
	public struct DmdCueParameter
	{
		public string Name;
		public DmdParamType Type;
		public long IntValue;
		public double FloatValue;
		public string StringValue;
		public bool BoolValue;

		public DmdParamValue DefaultValue => new DmdParamValue {
			Name = Name,
			Type = Type,
			IntValue = IntValue,
			FloatValue = FloatValue,
			StringValue = StringValue,
			BoolValue = BoolValue
		};
	}

	[CreateAssetMenu(fileName = "DmdCue", menuName = "Pinball/DMD/Cue", order = 311)]
	public class DmdCueAsset : ScriptableObject
	{
		public string CueId;
		public CuePriority Priority = CuePriority.Award;
		public CueInterruptPolicy Interrupt = CueInterruptPolicy.Replace;
		public CueReturnPolicy Return = CueReturnPolicy.Resume;
		public string CoalesceKey;
		public int DurationFrames;
		public bool Loop;
		public DmdTransitionSpec EnterTransition;
		public DmdTransitionSpec ExitTransition;

		[JsonIgnore]
		[SerializeReference]
		public List<DmdLayer> Layers = new List<DmdLayer>();

		public List<DmdCueParameter> Parameters = new List<DmdCueParameter>();

		public string EffectiveId => string.IsNullOrWhiteSpace(CueId) ? name : CueId;

		public DmdValidationResult Validate() => DmdValidation.Validate(this);
	}
}
