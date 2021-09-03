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
using VisualPinball.Engine.VPT;

namespace VisualPinball.Unity
{
	public class StandardPrefabProvider : IPrefabProvider
	{
		public GameObject CreateBumper()
		{
			return UnityEngine.Resources.Load<GameObject>("Prefabs/Bumper (Builtin)");
		}
		public GameObject CreateGate(int type)
		{
			switch (type) {
				case GateType.GateLongPlate:
					return UnityEngine.Resources.Load<GameObject>("Prefabs/Gate - Long Plate (Builtin)");
				case GateType.GatePlate:
					return UnityEngine.Resources.Load<GameObject>("Prefabs/Gate - Plate (Builtin)");
				case GateType.GateWireRectangle:
					return UnityEngine.Resources.Load<GameObject>("Prefabs/Gate - Wire Rectangle (Builtin)");
				case GateType.GateWireW:
					return UnityEngine.Resources.Load<GameObject>("Prefabs/Gate - Wire W (Builtin)");
				default:
					throw new ArgumentException(nameof(type), $"Unknown gate type {type}.");
			}
		}

		public GameObject CreateKicker(int type)
		{
			switch (type) {
				case KickerType.KickerCup:
					return UnityEngine.Resources.Load<GameObject>("Prefabs/Kicker - Cup (Builtin)");
				case KickerType.KickerCup2:
					return UnityEngine.Resources.Load<GameObject>("Prefabs/Kicker - Cup 2 (Builtin)");
				case KickerType.KickerGottlieb:
					return UnityEngine.Resources.Load<GameObject>("Prefabs/Kicker - Gottlieb (Builtin)");
				case KickerType.KickerHole:
					return UnityEngine.Resources.Load<GameObject>("Prefabs/Kicker - Hole (Builtin)");
				case KickerType.KickerHoleSimple:
					return UnityEngine.Resources.Load<GameObject>("Prefabs/Kicker - Simple Hole (Builtin)");
				case KickerType.KickerWilliams:
					return UnityEngine.Resources.Load<GameObject>("Prefabs/Kicker - Williams (Builtin)");
				case KickerType.KickerInvisible:
					return UnityEngine.Resources.Load<GameObject>("Prefabs/Kicker - Invisible (Builtin)");
				default:
					throw new ArgumentException(nameof(type), $"Unknown kicker type {type}.");
			}
		}
		public GameObject CreateLight()
		{
			return UnityEngine.Resources.Load<GameObject>("Prefabs/Light (Builtin)");
		}

		public GameObject CreateSpinner()
		{
			return UnityEngine.Resources.Load<GameObject>("Prefabs/Spinner (Builtin)");
		}

		public GameObject CreateTarget(int type)
		{
			switch (type) {
				case TargetType.DropTargetBeveled:
					return UnityEngine.Resources.Load<GameObject>("Prefabs/Drop Target - Beveled (Builtin)");
				case TargetType.DropTargetFlatSimple:
					return UnityEngine.Resources.Load<GameObject>("Prefabs/Drop Target - Simple Flat (Builtin)");
				case TargetType.DropTargetSimple:
					return UnityEngine.Resources.Load<GameObject>("Prefabs/Drop Target - Simple (Builtin)");
				case TargetType.HitFatTargetRectangle:
					return UnityEngine.Resources.Load<GameObject>("Prefabs/Hit Target - Rectangle Fat (Builtin)");
				case TargetType.HitFatTargetSlim:
					return UnityEngine.Resources.Load<GameObject>("Prefabs/Hit Target - Rectangle Fat Narrow (Builtin)");
				case TargetType.HitFatTargetSquare:
					return UnityEngine.Resources.Load<GameObject>("Prefabs/Hit Target - Square Fat (Builtin)");
				case TargetType.HitTargetRectangle:
					return UnityEngine.Resources.Load<GameObject>("Prefabs/Hit Target - Rectangle (Builtin)");
				case TargetType.HitTargetRound:
					return UnityEngine.Resources.Load<GameObject>("Prefabs/Hit Target - Round (Builtin)");
				case TargetType.HitTargetSlim:
					return UnityEngine.Resources.Load<GameObject>("Prefabs/Hit Target - Narrow (Builtin)");
				default:
					throw new ArgumentException(nameof(type), $"Unknown target type {type}.");
			}
		}
	}
}
