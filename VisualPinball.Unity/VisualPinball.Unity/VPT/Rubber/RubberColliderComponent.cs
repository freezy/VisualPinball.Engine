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

using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;
using VisualPinball.Engine.VPT.Rubber;

namespace VisualPinball.Unity
{
	[PackAs("RubberCollider")]
	[AddComponentMenu("Pinball/Collision/Rubber Collider")]
	public class RubberColliderComponent : ColliderComponent<RubberData, RubberComponent>, IPackable
	{
		internal const float DefaultGuideElasticityScale = 1f;
		internal const float DefaultGuideInfluenceLength = 50f;
		internal const float DefaultMaximumResponseSegmentLength = 20f;
		internal const float MinimumResponseLength = 0.1f;

		#region Data

		[Tooltip("If set, a hit event is triggered.")]
		public bool HitEvent;

		[FormerlySerializedAs("HitZOffset")] [Tooltip("Z-Offset between the mesh and the collider.")]
		public float ZOffset = 0;

		[Tooltip("Ignore the assigned physics material above and use the value below.")]
		public bool OverwritePhysics;

		[Min(0f)]
		[Tooltip("Bounciness, also known as coefficient of restitution. Higher is more bouncy.")]
		public float Elasticity;

		[Min(0f)]
		[Tooltip("How much to decrease elasticity for fast impacts.")]
		public float ElasticityFalloff;

		[Min(0)]
		[Tooltip("Friction of the material.")]
		public float Friction;

		[Range(-90f, 90f)]
		[Tooltip("When hit, add a random angle between 0 and this value to the trajectory.")]
		public float Scatter;

		[SerializeField] private RubberColliderMode _mode = RubberColliderMode.Legacy;
		[SerializeField] private RubberPhysicsMaterialAsset _rubberPhysicsMaterial;
		[SerializeField]
		[Tooltip("Vary Physical collider elasticity by distance along each free span from its supporting guides.")]
		private bool _guideDistanceElasticityEnabled;
		[SerializeField]
		[Range(0f, 1f)]
		[FormerlySerializedAs("_freeSpanElasticityScale")]
		[Tooltip("Multiplier for ball-contact elasticity where the rubber is backed by a supporting guide. Free spans transition toward the full contact-material response.")]
		private float _guideElasticityScale = DefaultGuideElasticityScale;
		[SerializeField]
		[Min(MinimumResponseLength)]
		[Tooltip("Distance in VPX units from a supporting guide over which free-span elasticity transitions.")]
		private float _guideInfluenceLength = DefaultGuideInfluenceLength;
		[SerializeField]
		[Min(MinimumResponseLength)]
		[Tooltip("Upper bound for Physical collider length in VPX units while the distance response is active. The effective limit can be lower to sample the guide transition at least eight times.")]
		private float _maximumResponseSegmentLength = DefaultMaximumResponseSegmentLength;

		public RubberColliderMode Mode {
			get => _mode;
			set => _mode = value;
		}

		public RubberPhysicsMaterialAsset RubberPhysicsMaterial {
			get => _rubberPhysicsMaterial;
			set => _rubberPhysicsMaterial = value;
		}

		public bool GuideDistanceElasticityEnabled {
			get => _guideDistanceElasticityEnabled;
			set => _guideDistanceElasticityEnabled = value;
		}

		public float GuideElasticityScale {
			get => SanitizeScale(_guideElasticityScale);
			set => _guideElasticityScale = SanitizeScale(value);
		}

		public float GuideInfluenceLength {
			get => SanitizeLength(_guideInfluenceLength, DefaultGuideInfluenceLength);
			set => _guideInfluenceLength = SanitizeLength(value, DefaultGuideInfluenceLength);
		}

		public float MaximumResponseSegmentLength {
			get => SanitizeLength(_maximumResponseSegmentLength, DefaultMaximumResponseSegmentLength);
			set => _maximumResponseSegmentLength = SanitizeLength(value, DefaultMaximumResponseSegmentLength);
		}

		internal RubberGuideDistanceElasticitySettings GuideDistanceElasticitySettings
			=> new(GuideDistanceElasticityEnabled, GuideElasticityScale,
				GuideInfluenceLength, MaximumResponseSegmentLength);

		public bool CanUsePhysical => MainComponent && MainComponent.HasUsableGuidedPath;

		internal void RestoreGuideDistanceElasticitySettings(bool enabled, float guideScale,
			float guideInfluenceLength, float maximumResponseSegmentLength)
		{
			GuideDistanceElasticityEnabled = enabled;
			GuideElasticityScale = guideScale;
			GuideInfluenceLength = guideInfluenceLength;
			MaximumResponseSegmentLength = maximumResponseSegmentLength;
		}

		internal void ResetGuideDistanceElasticitySettings()
			=> RestoreGuideDistanceElasticitySettings(false, DefaultGuideElasticityScale,
				DefaultGuideInfluenceLength, DefaultMaximumResponseSegmentLength);

		private static float SanitizeScale(float value)
			=> float.IsFinite(value) ? math.saturate(value) : DefaultGuideElasticityScale;

		private static float SanitizeLength(float value, float defaultValue)
		{
			if (!float.IsFinite(value) || value <= 0f) {
				return defaultValue;
			}
			return math.max(MinimumResponseLength, value);
		}

		protected virtual void OnValidate()
		{
			var guideElasticityScale = GuideElasticityScale;
			var guideInfluenceLength = GuideInfluenceLength;
			var maximumResponseSegmentLength = MaximumResponseSegmentLength;
			if (_guideElasticityScale != guideElasticityScale) {
				_guideElasticityScale = guideElasticityScale;
			}
			if (_guideInfluenceLength != guideInfluenceLength) {
				_guideInfluenceLength = guideInfluenceLength;
			}
			if (_maximumResponseSegmentLength != maximumResponseSegmentLength) {
				_maximumResponseSegmentLength = maximumResponseSegmentLength;
			}
		}

		#endregion

		#region Packaging

		public byte[] Pack() => RubberColliderPackable.Pack(this);

		public byte[] PackReferences(Transform root, PackagedRefs refs, PackagedFiles files)
			=> RubberColliderReferencesPackable.Pack(this, files);

		public void Unpack(byte[] bytes) => RubberColliderPackable.Unpack(bytes, this);

		public void UnpackReferences(byte[] data, Transform root, PackagedRefs refs, PackagedFiles files)
			=> RubberColliderReferencesPackable.Unpack(data, this, files);

		#endregion

		#region Physics Material

		public override float PhysicsElasticity {
			get => Elasticity;
			set => Elasticity = value;
		}

		public override float PhysicsElasticityFalloff {
			get => ElasticityFalloff;
			set => ElasticityFalloff = value;
		}

		public override float PhysicsFriction {
			get => Friction;
			set => Friction = value;
		}

		public override float PhysicsScatter {
			get => Scatter;
			set => Scatter = value;
		}

		public override bool PhysicsOverwrite {
			get => OverwritePhysics;
			set => OverwritePhysics = value;
		}

		#endregion

		protected override IApiColliderGenerator InstantiateColliderApi(Player player, PhysicsEngine physicsEngine)
			=> MainComponent.RubberApi ?? new RubberApi(gameObject, player, physicsEngine);
	}

	internal readonly struct RubberGuideDistanceElasticitySettings
	{
		internal static RubberGuideDistanceElasticitySettings Disabled => new(false,
			RubberColliderComponent.DefaultGuideElasticityScale,
			RubberColliderComponent.DefaultGuideInfluenceLength,
			RubberColliderComponent.DefaultMaximumResponseSegmentLength);

		internal readonly bool Enabled;
		internal readonly float GuideElasticityScale;
		internal readonly float GuideInfluenceLength;
		internal readonly float MaximumResponseSegmentLength;

		internal bool UsesResponseProfile => Enabled && GuideElasticityScale != 1f;
		internal float EffectiveMaximumSegmentLength
			=> math.min(MaximumResponseSegmentLength, GuideInfluenceLength / 8f);

		internal RubberGuideDistanceElasticitySettings(bool enabled, float guideElasticityScale,
			float guideInfluenceLength, float maximumResponseSegmentLength)
		{
			Enabled = enabled;
			GuideElasticityScale = guideElasticityScale;
			GuideInfluenceLength = guideInfluenceLength;
			MaximumResponseSegmentLength = maximumResponseSegmentLength;
		}
	}
}
