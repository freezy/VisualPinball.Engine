using System;
using UnityEngine;
using UnityEngine.UI;

namespace VisualPinball.Unity
{
	/// <summary>
	/// <para>Pushes the Framerate value to a Text component.</para>
	/// </summary>
	[AddComponentMenu("Visual Pinball/FPS Counter")]
	public class FpsCounter : MonoBehaviour
	{
		[Header("// Sample Groups of Data ")]
		[Tooltip("Instead of reporting a specific framerate, this will sample many frames and report the average.")]
		public bool GroupSampling;

		[Tooltip("If Group Sampling is on, how many frames would you want to sample to report an average on?")]
		[Range(0, 20)]
		public int SampleSize = 10;

		[Header("// Config ")]
		[Tooltip("The Text Component you want the result pushed to. If you're using TMP then you need to change this in the code to a TMP_Text component instead.")]
		// use 'TMP_Text' instead of 'Text' if you want Text Mesh Pro support.
		public Text TargetText;

		[Tooltip("How often (in frames) do you want to update the Text component?")]
		[Range(1, 20)]
		public int UpdateTextEvery = 10;

		[Tooltip("This will smooth out the results so they blend together between updates and are easier to read.")]
		public bool Smoothed;

		[Tooltip("This sets how many numbers are buffered into memory as strings in order to obtain zero allocations at runtime.\n\nAlthough this is trivial in memory usage, realistically, there's no reason to be over 1000.")]
		[Range(0, 1000)]
		public int NumberBufferSize = 500;

		[Header("// System FPS (updates once/sec)")]
		[Tooltip("Would you like to read the System Tick instead of calculating it in this script?\n\nTests show that differences are negligible, but the option remains available to you.")]
		public bool UseSystemTick;

		[Header("// Color Config ")]
		[Tooltip("Optionally change the color of the TargetText based on FPS performance.")]
		public bool UseColors = true;
		[Tooltip("If the framerate is above 'OkayBelow' it will be the 'Good' color.")]
		public Color Good = Color.green;
		[Tooltip("If the framerate is below 'OkayBelow' it will be the 'Okay' color.")]
		public Color Okay = Color.yellow;
		[Tooltip("If the framerate is below 'BadBelow' it will be the 'Bad' color.")]
		public Color Bad = Color.red;
		[Tooltip("Threshold for defining an 'okay' framerate. Below this value is considered okay, but not high enough to be good, and not low enough to be bad.")]
		public int OkayBelow = 60;
		[Tooltip("Threshold for defining an 'bad' framerate. Below this value is considered bad.")]
		public int BadBelow = 30;

		public int Framerate { get; private set; }

		protected int[] FpsSamples;
		protected int SampleIndex;
		protected int TextUpdateIndex;

		private int m_sysLastSysTick;
		private int m_sysLastFrameRate;
		private int m_sysFrameRate;
		private string m_localfps;

		private static string[] m_numbers;

		protected virtual void Reset()
		{
			GroupSampling = true;
			SampleSize = 20;
			UpdateTextEvery = 1;
			Smoothed = true;
			UseColors = true;

			Good = Color.green;
			Okay = Color.yellow;
			Bad = Color.red;

			OkayBelow = 60;
			BadBelow = 30;

			UseSystemTick = false;
			NumberBufferSize = 1000;
		}

		protected virtual void Start()
		{
			m_numbers = new string[NumberBufferSize];
			for (var i = 0; i < NumberBufferSize; i++) m_numbers[i] = i.ToString();

			FpsSamples = new int[SampleSize];
			for (var i = 0; i < FpsSamples.Length; i++) FpsSamples[i] = 1;
			if (!TargetText) enabled = false;
		}

		protected virtual void Update()
		{
			if (GroupSampling) Group();
			else SingleFrame();

			m_localfps = m_numbers[Framerate];

			SampleIndex = SampleIndex < SampleSize - 1 ? SampleIndex + 1 : 0;
			TextUpdateIndex = TextUpdateIndex > UpdateTextEvery ? 0 : TextUpdateIndex + 1;
			if (TextUpdateIndex == UpdateTextEvery) TargetText.text = m_localfps;

			if (!UseColors) return;
			if (Framerate < BadBelow) {
				TargetText.color = Bad;
				return;
			}
			TargetText.color = Framerate < OkayBelow ? Okay : Good;
		}

		protected virtual void SingleFrame()
		{
			Framerate = Mathf.Clamp(UseSystemTick
					? GetSystemFramerate()
					: (int)(Smoothed
						? 1 / Time.smoothDeltaTime
						: 1 / Time.deltaTime),
				0,
				m_numbers.Length - 1);
		}

		protected virtual void Group()
		{
			FpsSamples[SampleIndex] = Mathf.Clamp(UseSystemTick
					? GetSystemFramerate()
					: (int)(Smoothed
						? 1 / Time.smoothDeltaTime
						: 1 / Time.deltaTime),
				0,
				m_numbers.Length - 1);

			Framerate = 0;
			var loop = true;
			var i = 0;
			while (loop) {
				if (i == SampleSize - 1) loop = false;
				Framerate += FpsSamples[i];
				i++;
			}
			Framerate /= FpsSamples.Length;
		}

		protected virtual int GetSystemFramerate()
		{
			if (Environment.TickCount - m_sysLastSysTick >= 1000) {
				m_sysLastFrameRate = m_sysFrameRate;
				m_sysFrameRate = 0;
				m_sysLastSysTick = Environment.TickCount;
			}
			m_sysFrameRate++;
			return m_sysLastFrameRate;
		}
	}
}
