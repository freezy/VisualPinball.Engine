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
		internal const float DefaultDeflectionClearance = 60f;
		internal const float MinimumDeflectionClearance = MinimumResponseLength;

		/// <summary>
		/// Diagnostic carried by a component that was never baked, and by every
		/// payload written before collider package version 3.
		/// </summary>
		public const string NotBakedReason = "The compliant rebound has not been set up yet.";

		/// <summary>Length of a canonical deformation bake hash, a SHA-256 in lowercase hex.</summary>
		internal const int DeformationBakeHashLength = 64;

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

		[SerializeField]
		[Tooltip("Let the straight free spans of this Physical rubber rebound off a massless-string model, so the rebound direction and speed depend on where and how hard the ball hits. The collision geometry stays static.")]
		private bool _deformationEnabled;
		[SerializeField]
		[Min(MinimumDeflectionClearance)]
		[Tooltip("Largest deflection in VPX units a free span may reach before it stops, measured from its rest curve. Keep it below the clearance to whatever sits behind the rubber.")]
		private float _deflectionClearance = DefaultDeflectionClearance;

		[SerializeField, HideInInspector]
		private RubberCompiledDeformationMaterial _compiledDeformationMaterial;
		[SerializeField, HideInInspector]
		private RubberSpanDescriptor[] _spanDescriptors = Array.Empty<RubberSpanDescriptor>();
		[SerializeField, HideInInspector]
		private RubberBackedResponseDescriptor[] _backedResponseDescriptors
			= Array.Empty<RubberBackedResponseDescriptor>();
		[SerializeField, HideInInspector]
		private RubberDeformationBakeOutcome _bakeOutcome = RubberDeformationBakeOutcome.Invalid;
		[SerializeField, HideInInspector]
		private string _bakeReason = NotBakedReason;
		[SerializeField, HideInInspector]
		private int _deformationBakeVersion;
		[SerializeField, HideInInspector]
		private string _deformationBakeHash = string.Empty;

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

		#region Deformation

		/// <summary>
		/// Opt-in for the compliant free-span rebound of this Physical rubber. It only
		/// takes effect with a current <see cref="RubberDeformationBakeOutcome.Deformable"/>
		/// bake, and only on the spans the bake marked rebound-eligible.
		/// </summary>
		public bool DeformationEnabled {
			get => _deformationEnabled;
			set => _deformationEnabled = value;
		}

		/// <summary>Deflection cap in VPX units, before the stretch limit is applied.</summary>
		public float DeflectionClearance {
			get => SanitizeLength(_deflectionClearance, DefaultDeflectionClearance);
			set => _deflectionClearance = SanitizeLength(value, DefaultDeflectionClearance);
		}

		public RubberCompiledDeformationMaterial CompiledDeformationMaterial {
			get => _compiledDeformationMaterial;
			internal set => _compiledDeformationMaterial = value;
		}

		public IReadOnlyList<RubberSpanDescriptor> SpanDescriptors
			=> _spanDescriptors ?? Array.Empty<RubberSpanDescriptor>();

		public IReadOnlyList<RubberBackedResponseDescriptor> BackedResponseDescriptors
			=> _backedResponseDescriptors ?? Array.Empty<RubberBackedResponseDescriptor>();

		public RubberDeformationBakeOutcome BakeOutcome => _bakeOutcome;

		public string BakeReason => string.IsNullOrEmpty(_bakeReason) ? NotBakedReason : _bakeReason;

		public int DeformationBakeVersion => _deformationBakeVersion;

		/// <summary>
		/// Canonical lowercase hex digest over every input that decides the bake
		/// outcome, or an empty string when nothing has been baked.
		/// </summary>
		public string DeformationBakeHash => SanitizeHash(_deformationBakeHash);

		internal void RestoreDeformationSettings(bool enabled, float deflectionClearance,
			RubberCompiledDeformationMaterial compiledMaterial,
			RubberSpanDescriptor[] spanDescriptors,
			RubberBackedResponseDescriptor[] backedResponseDescriptors,
			RubberDeformationBakeOutcome bakeOutcome, string bakeReason,
			int bakeVersion, string bakeHash)
		{
			_deformationEnabled = enabled;
			DeflectionClearance = deflectionClearance;
			_compiledDeformationMaterial = compiledMaterial;
			_spanDescriptors = spanDescriptors ?? Array.Empty<RubberSpanDescriptor>();
			_backedResponseDescriptors = backedResponseDescriptors
				?? Array.Empty<RubberBackedResponseDescriptor>();
			_bakeOutcome = Enum.IsDefined(typeof(RubberDeformationBakeOutcome), bakeOutcome)
				? bakeOutcome
				: RubberDeformationBakeOutcome.Invalid;
			_bakeReason = string.IsNullOrEmpty(bakeReason) ? NotBakedReason : bakeReason;
			_deformationBakeVersion = math.max(0, bakeVersion);
			_deformationBakeHash = SanitizeHash(bakeHash);
			if (_deformationBakeHash.Length == 0) {
				_deformationBakeVersion = 0;
			}
		}

		/// <summary>
		/// Clears the bake and every descriptor, leaving deformation switched off.
		/// This is what a payload written before collider package version 3 restores.
		/// </summary>
		internal void ResetDeformationSettings()
			=> RestoreDeformationSettings(false, DefaultDeflectionClearance,
				default, Array.Empty<RubberSpanDescriptor>(),
				Array.Empty<RubberBackedResponseDescriptor>(),
				RubberDeformationBakeOutcome.Invalid, NotBakedReason, 0, string.Empty);

		/// <summary>Writes a bake result, keeping the authored settings untouched.</summary>
		[NonSerialized] private string _lastEnsureWarning;

		/// <summary>
		/// Brings the bakes this collider depends on up to date: the guided path when a
		/// guide moved, and the compliant rebound when any of its inputs changed. Both
		/// are caches of the authored state, so nothing has to be baked by hand: this
		/// runs when the game starts and whenever the editor reads them. Returns true
		/// when the rubber has a valid guided path and, with the compliant rebound on, a
		/// bake whose outcome is Deformable.
		/// </summary>
		public bool EnsureBakesCurrent()
		{
			var rubber = MainComponent;
			if (Mode != RubberColliderMode.Physical || !rubber) {
				return false;
			}
			if (rubber.PathSource == RubberPathSource.Guides
				&& !RubberAutofit.EnsureCurrent(rubber, out var fitError)) {
				WarnOnce($"Rubber '{rubber.name}' could not refit its guided path: {fitError}");
				return false;
			}
			if (!_deformationEnabled) {
				return true;
			}
			if (!RubberDeformationBaker.EnsureCurrent(this)) {
				WarnOnce($"Rubber '{rubber.name}' could not set up its compliant rebound: {BakeReason}");
				return false;
			}
			return BakeOutcome == RubberDeformationBakeOutcome.Deformable;
		}

		private void WarnOnce(string message)
		{
			if (message == _lastEnsureWarning) {
				return;
			}
			_lastEnsureWarning = message;
			Debug.LogWarning(message, this);
		}

		private void Awake()
		{
			if (Application.isPlaying) {
				EnsureBakesCurrent();
			}
		}

		internal void ApplyDeformationBake(RubberCompiledDeformationMaterial compiledMaterial,
			RubberSpanDescriptor[] spanDescriptors,
			RubberBackedResponseDescriptor[] backedResponseDescriptors,
			RubberDeformationBakeOutcome bakeOutcome, string bakeReason,
			int bakeVersion, string bakeHash)
			=> RestoreDeformationSettings(_deformationEnabled, _deflectionClearance,
				compiledMaterial, spanDescriptors, backedResponseDescriptors,
				bakeOutcome, bakeReason, bakeVersion, bakeHash);

		private static string SanitizeHash(string value)
		{
			if (string.IsNullOrEmpty(value) || value.Length != DeformationBakeHashLength) {
				return string.Empty;
			}
			foreach (var character in value) {
				var isHex = character >= '0' && character <= '9'
					|| character >= 'a' && character <= 'f';
				if (!isHex) {
					return string.Empty;
				}
			}
			return value;
		}

		#endregion

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
			var deflectionClearance = DeflectionClearance;
			if (_deflectionClearance != deflectionClearance) {
				_deflectionClearance = deflectionClearance;
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
