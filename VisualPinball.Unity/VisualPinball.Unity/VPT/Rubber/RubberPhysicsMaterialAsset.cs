// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace VisualPinball.Unity
{
	[PackAs("RubberPhysicsMaterial")]
	[PackWith(typeof(RubberPhysicsMaterialPacker))]
	[CreateAssetMenu(fileName = "Rubber Physics Material", menuName = "Pinball/Rubber Physics Material")]
	public sealed class RubberPhysicsMaterialAsset : ScriptableObject
	{
		[Min(0f)] public float DensityKgPerCubicMeter = 1100f;
		[JsonIgnore] public AnimationCurve NominalStressMpaByStretchRatio = new(
			new Keyframe(1f, 0f), new Keyframe(2f, 2f));
		[Range(0f, 1f)] public float ModalDampingRatio = 0.08f;
		[Min(0f)] public float BallSurfaceFriction = 0.3f;
		[Min(1f)] public float MaximumStretchRatio = 2f;

		public float[] BakeStressLut(int sampleCount)
		{
			if (sampleCount < 2) {
				throw new ArgumentOutOfRangeException(nameof(sampleCount));
			}
			var curve = NominalStressMpaByStretchRatio ?? new AnimationCurve();
			var lut = new float[sampleCount];
			for (var i = 0; i < sampleCount; i++) {
				var stretch = Mathf.Lerp(1f, Mathf.Max(1f, MaximumStretchRatio), i / (sampleCount - 1f));
				lut[i] = curve.Evaluate(stretch);
			}
			return lut;
		}
	}

	public sealed class RubberPhysicsMaterialPacker : IPacker<RubberPhysicsMaterialAsset>, IPacker<ScriptableObject>
	{
		public MetaPackable Pack(int instanceId, RubberPhysicsMaterialAsset material, PackagedFiles files)
		{
			return RubberPhysicsMaterialMetaPackable.From(instanceId, material);
		}

		public MetaPackable Unpack(byte[] bytes, RubberPhysicsMaterialAsset material, PackagedFiles files)
		{
			var data = PackageApi.Packer.Unpack<RubberPhysicsMaterialMetaPackable>(bytes);
			data.Apply(material);
			return data;
		}

		MetaPackable IPacker<ScriptableObject>.Pack(int instanceId, ScriptableObject obj, PackagedFiles files)
			=> Pack(instanceId, (RubberPhysicsMaterialAsset)obj, files);

		MetaPackable IPacker<ScriptableObject>.Unpack(byte[] bytes, ScriptableObject obj, PackagedFiles files)
			=> Unpack(bytes, (RubberPhysicsMaterialAsset)obj, files);
	}

	public sealed class RubberPhysicsMaterialMetaPackable : MetaPackable
	{
		public CurveKeyframePackable[] Keyframes;
		public int PreWrapMode;
		public int PostWrapMode;

		public static RubberPhysicsMaterialMetaPackable From(int instanceId, RubberPhysicsMaterialAsset material)
		{
			var curve = material.NominalStressMpaByStretchRatio ?? new AnimationCurve();
			return new RubberPhysicsMaterialMetaPackable {
				InstanceId = instanceId,
				Keyframes = curve.keys.Select(CurveKeyframePackable.From).ToArray(),
				PreWrapMode = (int)curve.preWrapMode,
				PostWrapMode = (int)curve.postWrapMode,
			};
		}

		public void Apply(RubberPhysicsMaterialAsset material)
		{
			var curve = new AnimationCurve(Keyframes?.Select(key => key.ToKeyframe()).ToArray()
				?? Array.Empty<Keyframe>()) {
				preWrapMode = (WrapMode)PreWrapMode,
				postWrapMode = (WrapMode)PostWrapMode,
			};
			material.NominalStressMpaByStretchRatio = curve;
		}
	}

	public struct CurveKeyframePackable
	{
		public float Time;
		public float Value;
		public float InTangent;
		public float OutTangent;
		public float InWeight;
		public float OutWeight;
		public int WeightedMode;

		public static CurveKeyframePackable From(Keyframe keyframe)
		{
			return new CurveKeyframePackable {
				Time = keyframe.time,
				Value = keyframe.value,
				InTangent = keyframe.inTangent,
				OutTangent = keyframe.outTangent,
				InWeight = keyframe.inWeight,
				OutWeight = keyframe.outWeight,
				WeightedMode = (int)keyframe.weightedMode,
			};
		}

		public Keyframe ToKeyframe()
		{
			return new Keyframe {
				time = Time,
				value = Value,
				inTangent = InTangent,
				outTangent = OutTangent,
				inWeight = InWeight,
				outWeight = OutWeight,
				weightedMode = (WeightedMode)WeightedMode,
			};
		}
	}
}
