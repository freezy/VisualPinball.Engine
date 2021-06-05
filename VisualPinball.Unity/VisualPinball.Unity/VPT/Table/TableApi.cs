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
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Unity
{
	public class TableApi : IApiInitializable, IApiColliderGenerator
	{
		private readonly Player _player;
		public TableData Data;

		internal readonly Dictionary<string, BumperApi> Bumpers = new Dictionary<string, BumperApi>();
		internal readonly Dictionary<string, FlipperApi> Flippers = new Dictionary<string, FlipperApi>();
		internal readonly Dictionary<string, GateApi> Gates = new Dictionary<string, GateApi>();
		internal readonly Dictionary<string, HitTargetApi> HitTargets = new Dictionary<string, HitTargetApi>();
		internal readonly Dictionary<string, KickerApi> Kickers = new Dictionary<string, KickerApi>();
		internal readonly Dictionary<string, LightApi> Lights = new Dictionary<string, LightApi>();
		internal readonly Dictionary<string, PlungerApi> Plungers = new Dictionary<string, PlungerApi>();
		internal readonly Dictionary<string, RampApi> Ramps = new Dictionary<string, RampApi>();
		internal readonly Dictionary<string, RubberApi> Rubbers = new Dictionary<string, RubberApi>();
		internal readonly Dictionary<string, SpinnerApi> Spinners = new Dictionary<string, SpinnerApi>();
		internal readonly Dictionary<string, SurfaceApi> Surfaces = new Dictionary<string, SurfaceApi>();
		internal readonly Dictionary<string, TriggerApi> Triggers = new Dictionary<string, TriggerApi>();
		internal readonly Dictionary<string, TroughApi> Troughs = new Dictionary<string, TroughApi>();
		internal readonly Dictionary<string, PrimitiveApi> Primitives = new Dictionary<string, PrimitiveApi>();

		public TableApi(Player player)
		{
			_player = player;
		}

		internal IApiSwitch Switch(string name) => _player.Switch(name);

		/// <summary>
		/// Event emitted before the game starts.
		/// </summary>
		public event EventHandler Init;

		#region Items

		/// <summary>
		/// Returns a bumper by name.
		/// </summary>
		/// <param name="name">Name of the bumper</param>
		/// <returns>Bumper or `null` if no bumper with that name exists.</returns>
		public BumperApi Bumper(string name) => Bumpers.ContainsKey(name) ? Bumpers[name] : null;

		/// <summary>
		/// Returns a flipper by name.
		/// </summary>
		/// <param name="name">Name of the flipper</param>
		/// <returns>Flipper or `null` if no flipper with that name exists.</returns>
		public FlipperApi Flipper(string name) => Flippers.ContainsKey(name) ? Flippers[name] : null;

		/// <summary>
		/// Returns a gate by name.
		/// </summary>
		/// <param name="name">Name of the gate</param>
		/// <returns>Gate or `null` if no gate with that name exists.</returns>
		public GateApi Gate(string name) => Gates.ContainsKey(name) ? Gates[name] : null;

		/// <summary>
		/// Returns a hit target / drop target by name.
		/// </summary>
		/// <param name="name">Name of the target</param>
		/// <returns>Hit/drop target or `null` if no target with that name exists.</returns>
		public HitTargetApi HitTarget(string name) => HitTargets.ContainsKey(name) ? HitTargets[name] : null;

		/// <summary>
		/// Returns a kicker by name.
		/// </summary>
		/// <param name="name">Name of the kicker</param>
		/// <returns>Kicker or `null` if no kicker with that name exists.</returns>
		public KickerApi Kicker(string name) => Kickers.ContainsKey(name) ? Kickers[name] : null;

		/// <summary>
		/// Returns a light by name.
		/// </summary>
		/// <param name="name">Name of the light</param>
		/// <returns>Light or `null` if no light with that name exists.</returns>
		public LightApi Light(string name) => Lights.ContainsKey(name) ? Lights[name] : null;

		/// <summary>
		/// Returns a plunger by name.
		/// </summary>
		/// <param name="name">Name of the plunger</param>
		/// <returns>Plunger or `null` if no plunger with that name exists.</returns>
		public PlungerApi Plunger(string name) => Plungers.ContainsKey(name) ? Plungers[name] : null;

		/// <summary>
		/// Returns a primitive by name.
		/// </summary>
		/// <param name="name">Name of the primitive</param>
		/// <returns>Primitive or `null` if no primitive with that name exists.</returns>
		public PrimitiveApi Primitive(string name) => Primitives.ContainsKey(name) ? Primitives[name] : null;

		/// <summary>
		/// Returns a ramp by name.
		/// </summary>
		/// <param name="name">Name of the ramp</param>
		/// <returns>Ramp or `null` if no ramp with that name exists.</returns>
		public RampApi Ramp(string name) => Ramps.ContainsKey(name) ? Ramps[name] : null;

		/// <summary>
		/// Returns a rubber by name.
		/// </summary>
		/// <param name="name">Name of the rubber</param>
		/// <returns>Rubber or `null` if no rubber with that name exists.</returns>
		public RubberApi Rubber(string name) => Rubbers.ContainsKey(name) ? Rubbers[name] : null;

		/// <summary>
		/// Returns a spinner by name.
		/// </summary>
		/// <param name="name">Name of the spinner</param>
		/// <returns>Spinner or `null` if no spinner with that name exists.</returns>
		public SpinnerApi Spinner(string name) => Spinners.ContainsKey(name) ? Spinners[name] : null;

		/// <summary>
		/// Returns a surface (wall) by name.
		/// </summary>
		/// <param name="name">Name of the surface</param>
		/// <returns>Surface or `null` if no surface with that name exists.</returns>
		public SurfaceApi Surface(string name) => Surfaces.ContainsKey(name) ? Surfaces[name] : null;

		/// <summary>
		/// Returns a trigger by name.
		/// </summary>
		/// <param name="name">Name of the trigger</param>
		/// <returns>Trigger or `null` if no trigger with that name exists.</returns>
		public TriggerApi Trigger(string name) => Triggers.ContainsKey(name) ? Triggers[name] : null;

		/// <summary>
		/// Returns a trough by name.
		/// </summary>
		/// <param name="name">Name of the trough</param>
		/// <returns>Trigger or `null` if no trough with that name exists.</returns>
		public TroughApi Trough(string name) => Troughs.ContainsKey(name) ? Troughs[name] : null;

		#endregion

		#region Events

		void IApiInitializable.OnInit(BallManager ballManager)
		{
			Init?.Invoke(this, EventArgs.Empty);
		}

		#endregion

		Entity IApiColliderGenerator.ColliderEntity { get; } = Player.TableEntity;
		bool IApiColliderGenerator.IsColliderEnabled { get; } = true;

		internal (PlaneCollider, PlaneCollider) CreateColliders(Table table)
		{
			var info = new ColliderInfo {
				ItemType = ItemType.Table,
				Entity = new Entity { Index = table.Index, Version = table.Version },
				FireEvents = false,
				IsEnabled = true,
				Material = new PhysicsMaterialData {
					Elasticity = table.Data.Elasticity,
					ElasticityFalloff = table.Data.ElasticityFalloff,
					Friction = table.Data.Friction,
					ScatterAngleRad = table.Data.Scatter
				},
				HitThreshold = 0
			};

			return (
				new PlaneCollider(new float3(0, 0, 1), table.TableHeight, info),
				new PlaneCollider(new float3(0, 0, -1), table.GlassHeight, info)
			);
		}
		void IApiColliderGenerator.CreateColliders(Table table, List<ICollider> colliders)
		{
			var info = ((IApiColliderGenerator)this).GetColliderInfo();

			// simple outer borders:
			colliders.Add(new LineCollider(
				new float2(table.Data.Right, table.Data.Top),
				new float2(table.Data.Right, table.Data.Bottom),
				table.Data.TableHeight,
				table.Data.GlassHeight,
				info
			));

			colliders.Add(new LineCollider(
				new float2(table.Data.Left, table.Data.Bottom),
				new float2(table.Data.Left, table.Data.Top),
				table.Data.TableHeight,
				table.Data.GlassHeight,
				info
			));

			colliders.Add(new LineCollider(
				new float2(table.Data.Right, table.Data.Bottom),
				new float2(table.Data.Left, table.Data.Bottom),
				table.Data.TableHeight,
				table.Data.GlassHeight,
				info
			));

			colliders.Add(new LineCollider(
				new float2(table.Data.Left, table.Data.Top),
				new float2(table.Data.Right, table.Data.Top),
				table.Data.TableHeight,
				table.Data.GlassHeight,
				info
			));

			// glass:
			var rgv3D = new[] {
				new float3(Data.Left, Data.Top, table.Data.GlassHeight),
				new float3(Data.Right, Data.Top, table.Data.GlassHeight),
				new float3(Data.Right, Data.Bottom, table.Data.GlassHeight),
				new float3(Data.Left, Data.Bottom, table.Data.GlassHeight)
			};
			ColliderUtils.Generate3DPolyColliders(rgv3D, table, info, colliders);
		}

		ColliderInfo IApiColliderGenerator.GetColliderInfo()
		{
			return new ColliderInfo {
				Id = -1,
				ItemType = ItemType.Table,
				Entity = Player.TableEntity,
				ParentEntity = Entity.Null,
				FireEvents = false,
				IsEnabled = true,
				Material = default,
				HitThreshold = 0,
			};
		}
	}
}
