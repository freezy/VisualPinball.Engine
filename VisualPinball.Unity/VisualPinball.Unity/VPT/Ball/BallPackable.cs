// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
// https://github.com/freezy/VisualPinball.Engine
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace VisualPinball.Unity
{
	public struct BallPackable
	{
		public float Radius;
		public float Mass;

		public static byte[] Pack(BallComponent comp)
		{
			return PackageApi.Packer.Pack(new BallPackable {
				Radius = comp.Radius,
				Mass = comp.Mass,
			});
		}

		public static void Unpack(byte[] bytes, BallComponent comp)
		{
			var data = PackageApi.Packer.Unpack<BallPackable>(bytes);
			comp.Radius = data.Radius;
			comp.Mass = data.Mass;
		}
	}
}
