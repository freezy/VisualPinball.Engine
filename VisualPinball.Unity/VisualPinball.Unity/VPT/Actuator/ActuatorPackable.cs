// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
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

using UnityEngine;

namespace VisualPinball.Unity
{
	public struct ActuatorPackable
	{
		public ActuatorCoilMode CoilMode;
		public float InitialPosition;
		public float ActivationDuration;
		public float ReleaseDuration;
		public AnimationCurve ActivationCurve;
		public AnimationCurve ReleaseCurve;
		public float ReleaseDelay;
		public float ActivationThreshold;
		public float OneShotHoldDuration;

		public static byte[] Pack(ActuatorComponent comp)
		{
			return PackageApi.Packer.Pack(new ActuatorPackable {
				CoilMode = comp.CoilMode,
				InitialPosition = comp.InitialPosition,
				ActivationDuration = comp.ActivationDuration,
				ReleaseDuration = comp.ReleaseDuration,
				ActivationCurve = comp.ActivationCurve,
				ReleaseCurve = comp.ReleaseCurve,
				ReleaseDelay = comp.ReleaseDelay,
				ActivationThreshold = comp.ActivationThreshold,
				OneShotHoldDuration = comp.OneShotHoldDuration,
			});
		}

		public static void Unpack(byte[] bytes, ActuatorComponent comp)
		{
			var data = PackageApi.Packer.Unpack<ActuatorPackable>(bytes);
			comp.CoilMode = data.CoilMode;
			comp.InitialPosition = data.InitialPosition;
			comp.ActivationDuration = data.ActivationDuration;
			comp.ReleaseDuration = data.ReleaseDuration;
			comp.ActivationCurve = data.ActivationCurve;
			comp.ReleaseCurve = data.ReleaseCurve;
			comp.ReleaseDelay = data.ReleaseDelay;
			comp.ActivationThreshold = data.ActivationThreshold;
			comp.OneShotHoldDuration = data.OneShotHoldDuration;
		}
	}

	public struct ActuatorTransformPackable
	{
		public bool AnimatePosition;
		public PackableFloat3 PositionOffset;
		public ActuatorTranslationSpace TranslationSpace;
		public bool AnimateRotation;
		public PackableFloat3 RotationOffset;
		public AnimationCurve ResponseCurve;
		public bool Reverse;

		public static byte[] Pack(ActuatorTransformComponent comp)
		{
			return PackageApi.Packer.Pack(new ActuatorTransformPackable {
				AnimatePosition = comp.AnimatePosition,
				PositionOffset = comp.PositionOffset,
				TranslationSpace = comp.TranslationSpace,
				AnimateRotation = comp.AnimateRotation,
				RotationOffset = comp.RotationOffset,
				ResponseCurve = comp.ResponseCurve,
				Reverse = comp.Reverse,
			});
		}

		public static void Unpack(byte[] bytes, ActuatorTransformComponent comp)
		{
			var data = PackageApi.Packer.Unpack<ActuatorTransformPackable>(bytes);
			comp.AnimatePosition = data.AnimatePosition;
			comp.PositionOffset = data.PositionOffset;
			comp.TranslationSpace = data.TranslationSpace;
			comp.AnimateRotation = data.AnimateRotation;
			comp.RotationOffset = data.RotationOffset;
			comp.ResponseCurve = data.ResponseCurve;
			comp.Reverse = data.Reverse;
		}
	}

	public struct ActuatorTransformReferencesPackable
	{
		public ReferencePackable EmitterRef;

		public static byte[] Pack(ActuatorTransformComponent comp, PackagedRefs refs)
		{
			var emitter = comp._emitter;
			var emitterRef = new ReferencePackable(null, null);
			if (emitter != null) {
				if (refs.HasType(emitter.GetType())) {
					emitterRef = refs.PackReference(emitter);
				} else {
					Debug.LogWarning($"Cannot package animation emitter {emitter.GetType().FullName} on '{comp.name}' because it has no PackAs attribute; writing a null reference.", comp);
				}
			}
			return PackageApi.Packer.Pack(new ActuatorTransformReferencesPackable { EmitterRef = emitterRef });
		}

		public static void Unpack(byte[] bytes, ActuatorTransformComponent comp, PackagedRefs refs)
		{
			var data = PackageApi.Packer.Unpack<ActuatorTransformReferencesPackable>(bytes);
			comp._emitter = refs.Resolve<MonoBehaviour, IAnimationValueEmitter<float>>(data.EmitterRef);
		}
	}
}
