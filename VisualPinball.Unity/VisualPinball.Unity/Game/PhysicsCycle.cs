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

using System;
using NativeTrees;
using Unity.Collections;

namespace VisualPinball.Unity
{
	public struct PhysicsCycle : IDisposable
	{
		private NativeList<ContactBufferElement> _contacts;

		public PhysicsCycle(Allocator a)
		{
			_contacts = new NativeList<ContactBufferElement>(a);
		}

		internal void Simulate(float dTime, ref PhysicsState state, ref NativeOctree<PlaneCollider> octree, ref NativeList<BallData> balls)
		{
			while (dTime > 0) {
				
				var hitTime = dTime;       // begin time search from now ...  until delta ends
				
				// todo apply flipper time
				
				// clear contacts
				_contacts.Clear();

				// todo dynamic broad phase
				
				// todo static broad phase
				
				// todo static narrow phase
				
				// todo dynamic narrow phase

				// todo apply static time

				// todo displacement
				
				// todo dynamic collision
				
				// todo static collision
				
				// todo handle contacts

				// clear contacts
				_contacts.Clear();

				// todo ball spin hack
				
				dTime -= hitTime;  
			}
		}

		public void Dispose()
		{
			_contacts.Dispose();
		}
	}
}
