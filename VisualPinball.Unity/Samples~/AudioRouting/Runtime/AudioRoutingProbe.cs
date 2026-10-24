// Visual Pinball Engine. Copyright (C) 2026 freezy and VPE Team.
// SPDX-License-Identifier: MIT
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Audio;

namespace VisualPinball.Unity
{
	/// <summary>Standalone diagnostic sample. Never installs itself in a table or production player.</summary>
	public sealed class AudioRoutingProbe : MonoBehaviour
	{
		public const string SpatializerName = "VPE Cabinet Panner";
		private const string Library = "AudioPluginVpeRouting";
		private const float Amplitude = .02f;
		[DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
		private static extern int VpeRoutingGetMetrics(int slot, [Out] float[] values, int count);
		[DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
		private static extern int VpeRoutingClearMetrics(int slot);
		[DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
		private static extern int VpeRoutingSetGenerator(int channel, int frequency, float amplitude);

		[Serializable] public sealed class Observation
		{
			public string name;
			public int clipChannels, sourceInputs, sourceOutputs, mixerInputs, mixerOutputs;
			public int sourceHostApi, sourceMaxFrames, mixerMaxFrames, toneHz;
			public float sourceCallbacks, mixerCallbacks;
			public bool spatialized, parametersAccepted, virtualized, unsupportedLayout, passed;
			public float[] inputPeaks, sourcePeaks, mixerPeaks;
			public string result;
		}
		[Serializable] public sealed class Report
		{
			public int schemaVersion = 1;
			public string unityVersion, platform, requestedLayout, effectiveLayout, driverLayout, spatializer, backend, backendDecision, error;
			public int sampleRate, dspBufferSize;
			public bool resetAccepted, nativeLoaded, stereoRoutingPassed, softwareRoutingPassed, physicalOutputsVerified;
			public string surroundGate;
			public List<Observation> observations = new List<Observation>();
		}

		private Report _report;
		private AudioSource _source;
		private AudioClip _clip;
		private Transform _listener;
		private string _reportPath, _status = "Preparing audio";
		private bool _running, _finished, _generatorAvailable;

		private void Start()
		{
			Application.runInBackground = true;
			_reportPath = Argument("--report", Path.Combine(Application.persistentDataPath, "audio-routing-probe.json"));
			_report = new Report { unityVersion = Application.unityVersion, platform = Application.platform.ToString(), requestedLayout = Argument("--layout", "7.1"), backend = Argument("--backend", "spatializer") };
			try {
				if (_report.backend != "spatializer" && _report.backend != "generator") throw new ArgumentException("Use --backend spatializer or generator.");
				if (_report.requestedLayout != "stereo" && _report.requestedLayout != "7.1") throw new ArgumentException("Use --layout stereo or 7.1.");
				var config = AudioSettings.GetConfiguration();
				config.speakerMode = _report.requestedLayout == "stereo" ? AudioSpeakerMode.Stereo : AudioSpeakerMode.Mode7point1;
				_report.resetAccepted = AudioSettings.Reset(config);
				config = AudioSettings.GetConfiguration();
				_report.effectiveLayout = config.speakerMode.ToString();
				_report.driverLayout = AudioSettings.driverCapabilities.ToString();
				_report.sampleRate = config.sampleRate; _report.dspBufferSize = config.dspBufferSize;
				_report.spatializer = AudioSettings.GetSpatializerPluginName();
				_report.nativeLoaded = VpeRoutingGetMetrics(0, new float[24], 24) == 1;
				_generatorAvailable = VpeRoutingSetGenerator(0, 480, 0) == 1;
				if (_report.spatializer != SpatializerName) throw new InvalidOperationException("The VPE Cabinet Panner must be selected at build time.");
				var listenerObject = new GameObject("Probe listener");
				_listener = listenerObject.transform; listenerObject.AddComponent<AudioListener>();
				_source = new GameObject("Probe source").AddComponent<AudioSource>();
				_source.outputAudioMixerGroup = Resources.Load<AudioMixer>(_report.backend == "generator" ? "GeneratorProbeMixer" : "RoutingProbeMixer").FindMatchingGroups("Master")[0];
				_source.playOnAwake = false; _source.loop = true; _source.priority = 0;
				_source.dopplerLevel = 0; _source.spread = 0;
				_source.rolloffMode = AudioRolloffMode.Custom;
				_source.SetCustomCurve(AudioSourceCurveType.CustomRolloff, AnimationCurve.Linear(0, 1, 1, 1));
				_running = true;
				StartCoroutine(RunSafely(_report.backend == "generator" ? RunGenerator() : Run()));
			} catch (Exception error) { Fail(error); }
		}

		private IEnumerator RunSafely(IEnumerator routine)
		{
			var stack = new Stack<IEnumerator>();
			stack.Push(routine);
			while (stack.Count > 0 && !_finished) {
				var current = stack.Peek();
				if (!MoveNext(current)) { stack.Pop(); continue; }
				if (current.Current is IEnumerator nested) stack.Push(nested);
				else yield return current.Current;
			}
		}
		private bool MoveNext(IEnumerator routine)
		{
			try { return routine.MoveNext(); }
			catch (Exception error) { Fail(error); return false; }
		}
		private IEnumerator Run()
		{
			// An isolated source per case is unnecessary: settle silent buffers and reset metrics
			// after configuring the existing source, then measure one steady loop at a time.
			yield return Observe("raw-mono", 1, -1, 0, .5f, .5f, false);
			yield return Observe("raw-stereo-left", 2, 0, 0, .5f, .5f, false);
			yield return Observe("raw-stereo-right", 2, 1, 0, .5f, .5f, false);
			yield return Observe("program-2d-left", 2, 0, 0, .5f, .5f, true);
			yield return Observe("program-2d-right", 2, 1, 0, .5f, .5f, true);
			yield return Observe("cabinet-front-left", 1, -1, 1, 0, 0, false);
			yield return Observe("cabinet-front-right", 1, -1, 1, 1, 0, false);
			yield return Observe("cabinet-back-left", 1, -1, 1, 0, 1, false);
			yield return Observe("cabinet-center", 1, -1, 1, .5f, .5f, false);
			yield return Observe("cabinet-back-right", 1, -1, 1, 1, 1, false);
			yield return Observe("cabinet-far-listener", 1, -1, 1, .5f, .5f, false, true);
			yield return Observe("backbox-left", 2, 0, 2, 0, 0, false);
			yield return Observe("backbox-right", 2, 1, 2, 0, 0, false);
			_report.stereoRoutingPassed = _report.observations.Take(5).All(o => o.passed);
			_report.softwareRoutingPassed = _report.observations.All(o => o.passed);
			_report.surroundGate = ClassifySurroundGate();
			_report.backendDecision = "Unity's documented spatializer input is stereo; use the central generator/PCM renderer as the production candidate. Physical SSF remains unverified.";
			Finish();
		}

		private IEnumerator RunGenerator()
		{
			_report.stereoRoutingPassed = true;
			int[] channels = { 0, 1, 4, 5, 6, 7 };
			string[] names = { "backbox-left", "backbox-right", "cabinet-front-left", "cabinet-front-right", "cabinet-back-left", "cabinet-back-right" };
			for (int index = 0; index < channels.Length; ++index) {
				_status = "generator-" + names[index];
				VpeRoutingSetGenerator(0, 480, 0);
				yield return new WaitForSecondsRealtime(.2f);
				VpeRoutingClearMetrics(0); VpeRoutingClearMetrics(2);
				bool accepted = VpeRoutingSetGenerator(channels[index], 320 + index*80, Amplitude) == 1;
				yield return new WaitForSecondsRealtime(.45f);
				var source = new float[24]; var mixer = new float[24];
				VpeRoutingGetMetrics(2, source, 24); VpeRoutingGetMetrics(0, mixer, 24);
				var expected = new float[8]; expected[channels[index]] = Amplitude;
				var result = new Observation {
					name = _status, parametersAccepted = accepted, toneHz = 320 + index*80,
					sourceInputs = (int)source[0], sourceOutputs = (int)source[1], sourceCallbacks = source[2],
					mixerInputs = (int)mixer[0], mixerOutputs = (int)mixer[1], mixerCallbacks = mixer[2],
					sourceHostApi = (int)source[5], sourceMaxFrames = (int)source[6], mixerMaxFrames = (int)mixer[6],
					unsupportedLayout = source[3] != 0, inputPeaks = source.Skip(8).Take(8).ToArray(),
					sourcePeaks = source.Skip(16).Take(8).ToArray(), mixerPeaks = mixer.Skip(16).Take(8).ToArray()
				};
				bool eightChannels = result.sourceOutputs == 8 && result.mixerInputs == 8 && result.mixerOutputs == 8;
				bool callbacksActive = result.sourceCallbacks > 0 && result.mixerCallbacks > 0;
				bool signalPreserved = accepted && callbacksActive && Same(expected, result.sourcePeaks) && Same(expected, result.mixerPeaks);
				if (index < 2) _report.stereoRoutingPassed &= signalPreserved;
				bool channelAvailable = result.sourceOutputs > channels[index] && result.mixerOutputs > channels[index];
				result.passed = eightChannels && signalPreserved;
				result.result = result.passed ? "passed" : !accepted ? "generator-controls-rejected" : !callbacksActive ? "generator-callbacks-unavailable" :
					!eightChannels && (!channelAvailable || signalPreserved) ? "required-eight-channel-path-unavailable" : "inspect-generator-output-peaks";
				_report.observations.Add(result);
				VpeRoutingSetGenerator(0, 480, 0);
			}
			_report.softwareRoutingPassed = _report.observations.All(o => o.passed);
			_report.surroundGate = ClassifySurroundGate();
			_report.backendDecision = _report.softwareRoutingPassed ? "Generator passed eight-channel software transport; physical outputs and production PCM voice rendering remain pending." : "Generator transport not established; inspect callback counts and output peaks. Physical SSF remains pending.";
			Finish();
		}

		private string ClassifySurroundGate()
		{
			if (_report.softwareRoutingPassed) return "passed";
			// Missing discrete output on mono/stereo/quad/5.1 is inconclusive. A known
			// gain/isolation failure must never be hidden by the driver's capabilities.
			bool unavailableOnly = _report.observations.All(o => o.passed || o.result == "required-eight-channel-path-unavailable");
			return unavailableOnly && _report.driverLayout != AudioSpeakerMode.Mode7point1.ToString() ? "inconclusive" : "failed";
		}

		private IEnumerator Observe(string name, int channels, int activeChannel, int mode, float x, float y, bool twoDimensional, bool far = false)
		{
			_status = name;
			_source.Stop(); _source.volume = 0;
			if (_clip != null) Destroy(_clip);
			int frames = _report.sampleRate / 4;
			int toneHz = mode == 1 ? (x == 0 && y == 0 ? 320 : x == 1 && y == 0 ? 480 : x == 0 && y == 1 ? 640 : x == 1 && y == 1 ? 800 : 560) : activeChannel == 1 ? 720 : 480;
			var data = new float[frames * channels];
			for (int f = 0; f < frames; ++f) for (int c = 0; c < channels; ++c)
				data[f * channels + c] = channels == 1 || c == activeChannel ? Amplitude * Mathf.Sin(2 * Mathf.PI * toneHz * f / _report.sampleRate) : 0;
			_clip = AudioClip.Create(name, frames, channels, _report.sampleRate, false);
			_clip.SetData(data, 0); _source.clip = _clip;
			_listener.position = far ? new Vector3(10000, 10000, 10000) : Vector3.zero;
			_source.spatialBlend = twoDimensional ? 0 : 1;
			_source.spatialize = !twoDimensional;
			_source.Play();
			yield return new WaitForSecondsRealtime(.2f);
			bool accepted = twoDimensional || (_source.SetSpatializerFloat(0, x) && _source.SetSpatializerFloat(1, y) && _source.SetSpatializerFloat(2, mode) && _source.SetSpatializerFloat(3, 1));
			yield return new WaitForSecondsRealtime(.15f);
			VpeRoutingClearMetrics(0); VpeRoutingClearMetrics(1);
			_source.volume = 1;
			yield return new WaitForSecondsRealtime(.45f);
			// Peak metrics hold values: do not mistake the next silent DSP block for lost audio.
			var source = new float[24]; var mixer = new float[24];
			VpeRoutingGetMetrics(1, source, 24); VpeRoutingGetMetrics(0, mixer, 24);
			var result = new Observation {
				name = name, clipChannels = channels, spatialized = !twoDimensional, parametersAccepted = accepted,
				sourceHostApi = (int)source[5], sourceMaxFrames = (int)source[6], mixerMaxFrames = (int)mixer[6], toneHz = toneHz,
				sourceInputs = (int)source[0], sourceOutputs = (int)source[1], sourceCallbacks = source[2],
				mixerInputs = (int)mixer[0], mixerOutputs = (int)mixer[1], mixerCallbacks = mixer[2],
				unsupportedLayout = source[3] != 0, virtualized = _source.isVirtual,
				inputPeaks = source.Skip(8).Take(8).ToArray(), sourcePeaks = source.Skip(16).Take(8).ToArray(), mixerPeaks = mixer.Skip(16).Take(8).ToArray()
			};
			bool audible = (twoDimensional ? result.mixerPeaks : result.inputPeaks).Any(p => p > Amplitude * .1f);
			bool eightChannels = result.sourceInputs == 8 && result.sourceOutputs == 8 && result.mixerInputs == 8 && result.mixerOutputs == 8;
			if (mode == 0) {
				var expected = new float[8];
				if (channels == 1) expected[0] = expected[1] = Amplitude / Mathf.Sqrt(2);
				else expected[activeChannel] = Amplitude;
				result.passed = accepted && audible && !result.virtualized && result.mixerCallbacks > 0 &&
					Same(expected, result.mixerPeaks) && (twoDimensional || result.sourceCallbacks > 0 &&
					Same(expected, result.inputPeaks) && Same(expected, result.sourcePeaks));
			}
			else {
				var expected = new float[8];
				if (mode == 1) {
					expected[4] = Amplitude * Mathf.Sqrt((1-x)*(1-y)); expected[5] = Amplitude * Mathf.Sqrt(x*(1-y));
					expected[6] = Amplitude * Mathf.Sqrt((1-x)*y); expected[7] = Amplitude * Mathf.Sqrt(x*y);
				} else expected[activeChannel] = Amplitude;
				result.passed = accepted && audible && eightChannels && !result.virtualized && Same(expected, result.sourcePeaks) && Same(expected, result.mixerPeaks);
			}
			result.result = result.passed ? "passed" : mode != 0 && !eightChannels ? "required-eight-channel-path-unavailable" : "inspect-input-output-peaks";
			_report.observations.Add(result);
			_source.volume = 0; _source.Stop();
			Debug.Log("Audio routing probe: " + name + " => " + result.result);
			yield return new WaitForSecondsRealtime(.15f);
		}

		private static bool Same(float[] a, float[] b)
		{
			for (int i = 0; i < a.Length; ++i) if (float.IsNaN(b[i]) || float.IsInfinity(b[i]) || Mathf.Abs(a[i] - b[i]) > .001f) return false;
			return true;
		}
		private static string Argument(string name, string fallback)
		{
			var args = Environment.GetCommandLineArgs();
			for (int i = 0; i < args.Length - 1; ++i) if (args[i] == name) return args[i + 1];
			return fallback;
		}
		private void Fail(Exception error) { _report.error = error.ToString(); _status = error.Message; Finish(); }
		private void Finish()
		{
			if (_finished) return;
			_finished = true; _running = false;
			if (_generatorAvailable) VpeRoutingSetGenerator(0, 480, 0);
			if (_source != null) _source.Stop();
			try {
				var fullPath = Path.GetFullPath(_reportPath);
				Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
				File.WriteAllText(fullPath, JsonUtility.ToJson(_report, true));
				Debug.Log("Audio routing report: " + fullPath);
			} catch (Exception error) {
				_report.error = "Could not write routing report: " + error;
				Debug.LogError(_report.error);
			}
			_status = _report.error ?? _report.backendDecision;
			if (Environment.GetCommandLineArgs().Contains("--quit-on-complete"))
				Application.Quit(_report.error != null ? 2 : _report.surroundGate == "failed" ? 3 : 0);
		}
		private void OnGUI()
		{
			GUILayout.BeginArea(new Rect(25, 25, Screen.width - 50, Screen.height - 50));
			GUILayout.Label("VPE — Audio routing prototype");
			GUILayout.Label(_status);
			if (_report != null) GUILayout.Label("Requested: " + _report.requestedLayout + " · Effective: " + _report.effectiveLayout + " · Driver: " + _report.driverLayout);
			GUILayout.Label("Low-level test signals only. Physical channel verification is a separate manual step.");
			if (_running && GUILayout.Button("Stop tests")) Fail(new OperationCanceledException("Tests stopped by the user."));
			if (!_running) GUILayout.Label("Report: " + _reportPath);
			GUILayout.EndArea();
		}
		private void OnDestroy()
		{
			if (_generatorAvailable) VpeRoutingSetGenerator(0, 480, 0);
			if (_source != null) Destroy(_source.gameObject);
			if (_clip != null) Destroy(_clip);
			if (_listener != null) Destroy(_listener.gameObject);
		}
	}
}
