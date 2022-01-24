﻿// Visual Pinball Engine
// Copyright (C) 2022 freezy and VPE Team
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
using VisualPinball.Engine.VPT;

namespace VisualPinball.Unity
{
	public class StandardPrefabProvider : IPrefabProvider
	{
		public GameObject CreateBumper()
		{
			return Resources.Load<GameObject>("Prefabs/Bumper (Builtin)");
		}
		public GameObject CreateGate(int type)
		{
			switch (type) {
				case GateType.GateLongPlate:
					return Resources.Load<GameObject>("Prefabs/Gate - Long Plate (Builtin)");
				case GateType.GatePlate:
					return Resources.Load<GameObject>("Prefabs/Gate - Plate (Builtin)");
				case GateType.GateWireRectangle:
					return Resources.Load<GameObject>("Prefabs/Gate - Wire Rectangle (Builtin)");
				case GateType.GateWireW:
					return Resources.Load<GameObject>("Prefabs/Gate - Wire W (Builtin)");
				default:
					throw new ArgumentException(nameof(type), $"Unknown gate type {type}.");
			}
		}

		public GameObject CreateKicker(int type)
		{
			switch (type) {
				case KickerType.KickerCup:
					return Resources.Load<GameObject>("Prefabs/Kicker - Cup (Builtin)");
				case KickerType.KickerCup2:
					return Resources.Load<GameObject>("Prefabs/Kicker - Cup 2 (Builtin)");
				case KickerType.KickerGottlieb:
					return Resources.Load<GameObject>("Prefabs/Kicker - Gottlieb (Builtin)");
				case KickerType.KickerHole:
					return Resources.Load<GameObject>("Prefabs/Kicker - Hole (Builtin)");
				case KickerType.KickerHoleSimple:
					return Resources.Load<GameObject>("Prefabs/Kicker - Simple Hole (Builtin)");
				case KickerType.KickerWilliams:
					return Resources.Load<GameObject>("Prefabs/Kicker - Williams (Builtin)");
				case KickerType.KickerInvisible:
					return Resources.Load<GameObject>("Prefabs/Kicker - Invisible (Builtin)");
				default:
					throw new ArgumentException(nameof(type), $"Unknown kicker type {type}.");
			}
		}

		public GameObject CreateLight()
		{
			return Resources.Load<GameObject>("Prefabs/Light (Builtin)");
		}

		public GameObject CreateInsertLight()
		{
			return Resources.Load<GameObject>("Prefabs/Light - Insert (Builtin)");
		}

		public GameObject CreateSpinner()
		{
			return Resources.Load<GameObject>("Prefabs/Spinner (Builtin)");
		}

		public GameObject CreateHitTarget(int type)
		{
			switch (type) {
				case TargetType.HitFatTargetRectangle:
					return Resources.Load<GameObject>("Prefabs/Hit Target - Rectangle Fat (Builtin)");
				case TargetType.HitFatTargetSlim:
					return Resources.Load<GameObject>("Prefabs/Hit Target - Rectangle Fat Narrow (Builtin)");
				case TargetType.HitFatTargetSquare:
					return Resources.Load<GameObject>("Prefabs/Hit Target - Square Fat (Builtin)");
				case TargetType.HitTargetRectangle:
					return Resources.Load<GameObject>("Prefabs/Hit Target - Rectangle (Builtin)");
				case TargetType.HitTargetRound:
					return Resources.Load<GameObject>("Prefabs/Hit Target - Round (Builtin)");
				case TargetType.HitTargetSlim:
					return Resources.Load<GameObject>("Prefabs/Hit Target - Narrow (Builtin)");
				default:
					throw new ArgumentException(nameof(type), $"Unknown hit target type {type}.");
			}
		}

		public GameObject CreateDropTarget(int type)
		{
			switch (type) {
				case TargetType.DropTargetBeveled:
					return Resources.Load<GameObject>("Prefabs/Drop Target - Beveled (Builtin)");
				case TargetType.DropTargetFlatSimple:
					return Resources.Load<GameObject>("Prefabs/Drop Target - Simple Flat (Builtin)");
				case TargetType.DropTargetSimple:
					return Resources.Load<GameObject>("Prefabs/Drop Target - Simple (Builtin)");
				default:
					throw new ArgumentException(nameof(type), $"Unknown drop target type {type}.");
			}
		}
	}
}
