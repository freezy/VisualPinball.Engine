using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using UnityEngine;
using static Unity.Mathematics.math;

namespace VisualPinball.Unity.Collections
{
	/// <summary>
	/// Burst friendly curve implementation used to efficiently work with animation curves in the job system.
	/// </summary>
	public struct NativeCurve : IDisposable
	{
		/// <summary>
		/// Informs if the native data structure has an allocated memory buffer.
		/// </summary>
		public bool isCreated => m_Values.IsCreated;

		NativeArray<float> m_Values;
		WrapMode m_PreWrapMode;
		WrapMode m_PostWrapMode;

		void InitializeValues(int count, Allocator allocator = Allocator.Persistent)
		{
			if (m_Values.IsCreated)
				m_Values.Dispose();

			m_Values = new NativeArray<float>(count, allocator, NativeArrayOptions.UninitializedMemory);
		}

		/// <summary>
		/// Re-initialize native curve data with new Animation curve.
		/// </summary>
		/// <param name="curve">Curve ground truth to initialize from.</param>
		/// <param name="resolution">Number of samples to use when converting from animation curve to native curve.</param>
		public void Update(AnimationCurve curve, int resolution)
		{
			if (curve == null)
				return;

			m_PreWrapMode = curve.preWrapMode;
			m_PostWrapMode = curve.postWrapMode;

			if (!m_Values.IsCreated || m_Values.Length != resolution)
				InitializeValues(resolution);

			for (int i = 0; i < resolution; i++)
				m_Values[i] = curve.Evaluate((float)i / (float)resolution);
		}

		/// <summary>
		/// Evaluate value along the underlying native curve.
		/// </summary>
		/// <param name="t">Location along curve to evaluate.</param>
		/// <returns>Value along curve at given location t.</returns>
		public float Evaluate(float t)
		{
			var count = m_Values.Length;

			if (count == 1)
				return m_Values[0];

			if (t < 0f)
			{
				switch (m_PreWrapMode)
				{
					default:
						return m_Values[0];
					case WrapMode.Loop:
						t = 1f - (abs(t) % 1f);
						break;
					case WrapMode.PingPong:
						t = PingPong(t, 1f);
						break;
				}
			}
			else if (t > 1f)
			{
				switch (m_PostWrapMode)
				{
					default:
						return m_Values[count - 1];
					case WrapMode.Loop:
						t %= 1f;
						break;
					case WrapMode.PingPong:
						t = PingPong(t, 1f);
						break;
				}
			}

			var it = t * (count - 1);

			var lower = (int)it;
			var upper = lower + 1;
			if (upper >= count)
				upper = count - 1;

			return lerp(m_Values[lower], m_Values[upper], it - lower);
		}

		/// <summary>
		/// Dispose native collection.
		/// </summary>
		public void Dispose()
		{
			if (m_Values.IsCreated)
				m_Values.Dispose();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		float Repeat(float t, float length)
		{
			return clamp(t - floor(t / length) * length, 0, length);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		float PingPong(float t, float length)
		{
			t = Repeat(t, length * 2f);
			return length - abs(t - length);
		}
	}
}
