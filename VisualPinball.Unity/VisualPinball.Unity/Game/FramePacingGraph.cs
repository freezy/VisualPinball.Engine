using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(10000)]
public class FramePacingGraph : MonoBehaviour
{
	[Header("Visibility & Input")] public bool startVisible = true;
	public InputActionReference toggleAction;

	[Header("Graph Layout (pixels)")] public Vector2 anchorOffset = new Vector2(24f, 24f);
	public Vector2 size = new Vector2(560f, 220f);
	public GraphAnchor anchor = GraphAnchor.TopLeft;

	[Header("Stats Panel")] [Tooltip("Width of the right-side stats panel (pixels).")]
	public float statsPanelWidth = 260f;

	[Tooltip("Padding inside the right-side stats panel (pixels).")]
	public float statsPanelPadding = 6f;

	[Header("Time Window & Scale")] [Range(1f, 60f)]
	public float windowSeconds = 10f;

	[Header("Stats")] [Tooltip("Seconds used for Avg/P95/P99. If <= 0, uses the full graph window.")]
	public float statsSeconds = 3f;

	[Range(60, 480)] public int maxExpectedFps = 240;
	public float yMaxMs = 40f;
	public bool autoY = true;
	public float autoYMargin = 1.2f;

	[Header("CPU Busy (heuristic when wait=0)")] [Range(0.05f, 0.4f)]
	public float nearFullFrameThresholdPercent = 0.20f; // 20% margin

	public float nonZeroWaitEpsilonMs = 0.5f; // consider wait valid if > 0.5 ms
	public bool useDx12HeuristicWhenWaitZero = true;

	[Header("Appearance")] public float lineWidth = 2f;
	public float gridLineWidth = 1f;
	public int gridLinesY = 4;
	public Color backgroundColor = new Color(0f, 0f, 0f, 0.6f);
	public Color borderColor = new Color(1f, 1f, 1f, 0.2f);
	public Color axisTextColor = new Color(1f, 1f, 1f, 0.85f);
	public int fontSize = 12;

	[Header("Built-in Metric Colors")] public Color totalColor = new Color(1f, 1f, 1f, 0.9f);
	public Color cpuColor = new Color(0.25f, 0.8f, 1f, 0.9f);
	public Color gpuColor = new Color(1f, 0.5f, 0.25f, 0.9f);

	[Header("Totals")]
	[Tooltip("When true, uses max(CPU, GPU) for 'Total' when valid; otherwise uses unscaledDeltaTime.")]
	public bool totalFromFrameTimingWhenAvailable = false;

	[Header("Performance")] [Tooltip("Recompute stats (avg/p95/p99) every N frames.")] [Range(1, 60)]
	public int statsEveryNFrames = 12;

	[Tooltip("Cap per-frame draw work by aggregating into pixel columns.")]
	public bool useColumnEnvelope = true;

	[Tooltip("Optionally disable FrameTimingManager reads if heavy on your platform.")]
	public bool enableCpuGpuCollection = true;

	bool initialized;

	// Fixed-width font for stats
	GUIStyle monoStyle;
	static Font sMonoFont; // cached across instances


	// Public API for custom metrics
	public int RegisterCustomMetric(string name, Color color, Func<float> sampler, float scale = 1f,
		bool enabled = true)
	{
		if (string.IsNullOrEmpty(name) || sampler == null) return -1;
		var m = new Metric(name, color, sampler, scale, enabled, capacity);
		customMetrics.Add(m);
		return customMetrics.Count - 1;
	}

	// ----- Internals -----
	public enum GraphAnchor
	{
		TopLeft,
		TopRight,
		BottomLeft,
		BottomRight
	}

	struct Stats
	{
		public float avg, p95, p99, min, max;
		public bool valid;
	}

	class Metric
	{
		public string name;
		public Color color;
		public Func<float> sampler;
		public float scale;
		public bool enabled;
		public float[] values; // ring-buffer aligned
		public Stats stats;
		public string cachedLegend; // updated when stats recompute

		public Metric(string name, Color color, Func<float> sampler, float scale, bool enabled, int capacity)
		{
			this.name = name;
			this.color = color;
			this.sampler = sampler;
			this.scale = scale;
			this.enabled = enabled;
			values = new float[capacity];
			stats = default;
			cachedLegend = name;
		}
	}

	Metric totalMetric, cpuMetric, gpuMetric;
	readonly List<Metric> customMetrics = new();

	float[] timestamps;
	int head = 0, count = 0, capacity;

	FrameTiming[] ftBuf = new FrameTiming[1];
	bool haveFT = false;
	float lastCpuMs = 0f, lastGpuMs = 0f; // raw totals
	float lastCpuBusyMs = 0f, lastGpuBusyMs = 0f; // busy we plot
	float lastCpuWaitMs = 0f; // optional: expose as a line if you want

	static Material lineMat;

	bool visible;
	int frameSinceStats = 0;

	// scratch arrays (no GC)
	float[] scratchValues;

	// FPS smoothing & text
	const float fpsSmoothTau = 0.5f;
	float smoothedFps = 0f;
	float fpsTextTimer = 0f;
	string fpsText = "0.0 FPS";

	// Column envelope caches
	float[] colMin, colMax; // reused per metric
	Vector2[] polyPts; // reused polyline points

	// Cached GUIStyle
	GUIStyle labelStyle;

// 2) Awake(): build a GUIStyle without GUI.skin and set initialized = true at the end
	void Awake()
	{
		visible = startVisible;
		useGUILayout = false;

		capacity = Mathf.Max(8, Mathf.CeilToInt(windowSeconds * maxExpectedFps));
		timestamps = new float[capacity];

		totalMetric = new Metric("Total (ms)", totalColor, SampleTotalMs, 1f, true, capacity);
		cpuMetric = new Metric("CPU (ms)", cpuColor, () => lastCpuBusyMs, 1f, enableCpuGpuCollection, capacity);
		gpuMetric = new Metric("GPU (ms)", gpuColor, () => lastGpuBusyMs, 1f, enableCpuGpuCollection, capacity);

		scratchValues = new float[capacity];
		EnsureLineMaterial();

		if (toggleAction != null && toggleAction.action != null)
		{
			toggleAction.action.performed += OnToggle;
			toggleAction.action.Enable();
		}

		// ✅ Do NOT touch GUI.skin here
		labelStyle = new GUIStyle();
		labelStyle.fontSize = fontSize;
		labelStyle.normal.textColor = axisTextColor;

		// Monospace style for the right panel
		monoStyle = new GUIStyle();
		monoStyle.fontSize = fontSize;
		monoStyle.normal.textColor = axisTextColor;
		monoStyle.richText = false;

		// Try common OS monospace fonts (fallback to default if none found)
		if (sMonoFont == null)
		{
			try
			{
				sMonoFont = Font.CreateDynamicFontFromOSFont(
					new[] { "Consolas", "Courier New", "Lucida Console", "DejaVu Sans Mono", "Menlo", "SF Mono" },
					fontSize);
			}
			catch
			{
				/* platform may not allow OS fonts; safe to ignore */
			}
		}

		if (sMonoFont != null) monoStyle.font = sMonoFont;


		initialized = true; // <-- mark fully constructed
	}


	void OnDestroy()
	{
		if (toggleAction != null && toggleAction.action != null)
			toggleAction.action.performed -= OnToggle;
	}

// 3) OnValidate(): only resize when we're initialized
	void OnValidate()
	{
		if (labelStyle != null)
		{
			labelStyle.fontSize = fontSize;
			labelStyle.normal.textColor = axisTextColor;
		}

		if (monoStyle != null)
		{
			monoStyle.fontSize = fontSize;
			monoStyle.normal.textColor = axisTextColor;
		}


		var needed = Mathf.Max(8, Mathf.CeilToInt(windowSeconds * maxExpectedFps));
		if (Application.isPlaying && initialized && needed != capacity)
		{
			ResizeCapacity(needed);
		}
	}


// 4) ResizeCapacity(): allocate fresh arrays & reset counters
	void ResizeCapacity(int newCap)
	{
		capacity = newCap;

		timestamps = new float[capacity];
		ResizeMetric(totalMetric, capacity);
		ResizeMetric(cpuMetric, capacity);
		ResizeMetric(gpuMetric, capacity);
		for (var i = 0; i < customMetrics.Count; i++)
			ResizeMetric(customMetrics[i], capacity);

		scratchValues = new float[capacity];
		head = 0;
		count = 0;
	}


// 5) ResizeMetric(): null-safe and use the new capacity
	static void ResizeMetric(Metric m, int newCap)
	{
		if (m == null) return;
		m.values = new float[newCap];
		m.stats = default;
		m.cachedLegend = m.name;
	}


	void OnToggle(InputAction.CallbackContext _) => visible = !visible;

	void Update()
	{
		// Get last frame timing
		if (enableCpuGpuCollection)
		{
			var got = FrameTimingManager.GetLatestTimings(1, ftBuf);
			haveFT = got > 0;
			if (haveFT)
			{
				// --- inside Update(), right after GetLatestTimings() succeeds ---
				var ft = ftBuf[0];

				lastCpuMs = Mathf.Max(0f, (float)ft.cpuFrameTime);
				lastGpuMs = Mathf.Max(0f, (float)ft.gpuFrameTime);

				lastCpuBusyMs = EstimateCpuBusyMs(ft);
				lastGpuBusyMs = lastGpuMs;
			}
		}
		else
		{
			haveFT = false;
			lastCpuMs = 0f;
			lastGpuMs = 0f;
		}

		// Sample
		var now = Time.unscaledTime;
		var totalMs = totalMetric.sampler();
		var cpuMs = cpuMetric.enabled ? cpuMetric.sampler() : 0f;
		var gpuMs = gpuMetric.enabled ? gpuMetric.sampler() : 0f;
		WriteSample(now, totalMs, cpuMs, gpuMs);

		// right after WriteSample(now, totalMs, cpuMs, gpuMs);
		var last = (head - 1 + capacity) % capacity;

		// Custom metrics
		var idxPrev = (head - 1 + capacity) % capacity;
		for (var i = 0; i < customMetrics.Count; i++)
		{
			var m = customMetrics[i];
			if (!m.enabled || m.sampler == null)
			{
				m.values[idxPrev] = 0f;
				continue;
			}

			var v = 0f;
			try {
				v = m.sampler() * m.scale;
			} catch { }

			m.values[idxPrev] = v;
		}

		// FPS smoothing & text throttling
		var instFps = Time.unscaledDeltaTime > 0f ? 1f / Time.unscaledDeltaTime : 0f;
		var a = 1f - Mathf.Exp(-Time.unscaledDeltaTime / fpsSmoothTau);
		smoothedFps = Mathf.Lerp(smoothedFps, instFps, a);
		fpsTextTimer += Time.unscaledDeltaTime;
		if (fpsTextTimer >= 0.15f)
		{
			fpsTextTimer = 0f;
			fpsText = $"{smoothedFps:0.0} FPS";
		}

		// Stats occasionally
		if (++frameSinceStats >= statsEveryNFrames)
		{
			frameSinceStats = 0;
			RecomputeStats(totalMetric);
			if (cpuMetric.enabled) RecomputeStats(cpuMetric);
			if (gpuMetric.enabled) RecomputeStats(gpuMetric);
			for (var i = 0; i < customMetrics.Count; i++)
				if (customMetrics[i].enabled)
					RecomputeStats(customMetrics[i]);
		}

		// Capture for next frame
		if (enableCpuGpuCollection) FrameTimingManager.CaptureFrameTimings();
	}

	float EstimateCpuBusyMs(FrameTiming ft)
	{
		var cpuFrame = Mathf.Max(0f, (float)ft.cpuFrameTime);
		var gpuFrame = Mathf.Max(0f, (float)ft.gpuFrameTime);
		var mainFrame = Mathf.Max(0f, (float)ft.cpuMainThreadFrameTime);
		var rendFrame = Mathf.Max(0f, (float)ft.cpuRenderThreadFrameTime);
		var waitMain = Mathf.Max(0f, (float)ft.cpuMainThreadPresentWaitTime);

		// If wait is actually reported, trust it.
		if (waitMain > nonZeroWaitEpsilonMs)
			return Mathf.Max(0f, mainFrame - waitMain);

		if (!useDx12HeuristicWhenWaitZero)
			return mainFrame; // fall back to main (may include idle)

		// --- Heuristic path (DX12 often reports wait=0 even when main is waiting) ---
		var fullWithMargin = (1f - nearFullFrameThresholdPercent) * cpuFrame;
		var gpuNear = gpuFrame > fullWithMargin;
		var mainNear = mainFrame > fullWithMargin;
		var renderNear = rendFrame > fullWithMargin;

		// GPU-bound & main is near frame time => main likely waiting; use render thread work.
		if (gpuNear && mainNear && !renderNear)
			return rendFrame;

		// CPU-bound => take the heavier CPU thread.
		if (!gpuNear && (mainNear || renderNear))
			return Mathf.Max(mainFrame, rendFrame);

		// Balanced/indeterminate: choose the larger "work" but never exceed the frame period.
		return Mathf.Min(cpuFrame, Mathf.Max(rendFrame, mainFrame));
	}


	void WriteSample(float now, float totalMs, float cpuMs, float gpuMs)
	{
		timestamps[head] = now;
		totalMetric.values[head] = totalMs;
		cpuMetric.values[head] = cpuMs;
		gpuMetric.values[head] = gpuMs;
		head = (head + 1) % capacity;
		count = Mathf.Min(count + 1, capacity);
	}

	float SampleTotalMs()
	{
		if (totalFromFrameTimingWhenAvailable && haveFT)
		{
			var candidate = Mathf.Max(lastCpuMs, lastGpuMs);
			if (candidate > 0f) return candidate;
		}

		return Time.unscaledDeltaTime * 1000f;
	}

	void RecomputeStats(Metric m)
	{
		var visible = CollectVisibleValues(m.values, scratchValues, statsSeconds, out var sum);

		if (visible <= 0)
		{
			m.stats = default;
			m.cachedLegend = $"{m.name}: -";
			return;
		}

		Array.Sort(scratchValues, 0, visible);

		var avg = sum / visible;
		var p95 = PercentileSorted(scratchValues, visible, 0.95f);
		var p99 = PercentileSorted(scratchValues, visible, 0.99f);
		var min = scratchValues[0];
		var max = scratchValues[visible - 1];

		m.stats.avg = avg;
		m.stats.p95 = p95;
		m.stats.p99 = p99;
		m.stats.min = min;
		m.stats.max = max;
		m.stats.valid = true;

		// (cachedLegend not used for the right panel anymore, but keep it tidy)
		m.cachedLegend =
			$"{m.name}: avg {avg:0.00} ms  •  p95 {p95:0.00}  •  p99 {p99:0.00}  •  min {min:0.00}  •  max {max:0.00}";
	}

	int CollectVisibleValues(float[] src, float[] dst, float seconds, out float sum)
	{
		sum = 0f;
		if (count == 0) return 0;
		var now = Time.unscaledTime;
		var minTime = now - (seconds > 0f ? seconds : windowSeconds);
		int visible = 0, start = (head - count + capacity) % capacity;

		for (var i = 0; i < count; i++)
		{
			var idx = (start + i) % capacity;
			var t = timestamps[idx];
			if (t < minTime) continue;
			var v = src[idx];
			dst[visible++] = v;
			sum += v;
		}

		return visible;
	}


	static float PercentileSorted(float[] sorted, int n, float p)
	{
		if (n == 0) return 0f;
		if (p <= 0f) return sorted[0];
		if (p >= 1f) return sorted[n - 1];
		var f = (n - 1) * p;
		int i0 = Mathf.FloorToInt(f), i1 = Math.Min(n - 1, i0 + 1);
		var t = f - i0;
		return Mathf.Lerp(sorted[i0], sorted[i1], t);
	}

	// ---------- RENDER ----------
	void OnGUI()
	{
		if (!visible) return;

		// 🧨 Critical fix: only draw on Repaint to avoid extra work during key/mouse events
		var evt = Event.current;
		if (evt == null || evt.type != EventType.Repaint) return;

		var r = ResolveRect();
		if (r.width <= 4f || r.height <= 4f) return;

		// Draw full background (graph + right panel) and grid
		DrawBackgroundGridAndPanel(r);

		var now = Time.unscaledTime;
		var invY = ResolveYScale(out var yMax);

		// Draw metrics (batched, column-envelope)
		DrawMetric(r, now, yMax, invY, totalMetric);
		if (cpuMetric.enabled) DrawMetric(r, now, yMax, invY, cpuMetric);
		if (gpuMetric.enabled) DrawMetric(r, now, yMax, invY, gpuMetric);
		for (var i = 0; i < customMetrics.Count; i++)
			if (customMetrics[i].enabled)
				DrawMetric(r, now, yMax, invY, customMetrics[i]);

		// Labels (legend + axis + fps)
		GUI.Label(new Rect(r.x + 6, r.y - (fontSize + 2), 120, fontSize + 4), $"{yMax:0.#} ms", labelStyle);
		GUI.Label(new Rect(r.x + 6, r.y + r.height * 0.5f - (fontSize + 2), 120, fontSize + 4),
			$"{(yMax * 0.5f):0.#} ms", labelStyle);
		GUI.Label(new Rect(r.x + 6, r.yMax - (fontSize + 2), 120, fontSize + 4), $"0 ms", labelStyle);

		// FPS
		GUI.Label(new Rect(r.x + 8, r.y + 6, 120, fontSize + 6), fpsText, labelStyle);

		// Right-side stats panel
		var statsRect = new Rect(r.xMax, r.y, statsPanelWidth, r.height);
		DrawStatsPanel(statsRect);

		// Time axis labels
		GUI.Label(new Rect(r.xMax - 100, r.yMax + 2, 100, fontSize + 4), "now", labelStyle);
		GUI.Label(new Rect(r.x, r.yMax + 2, 180, fontSize + 4), $"-{windowSeconds:0.#} s", labelStyle);
	}

	Rect ResolveRect()
	{
		float x = anchorOffset.x, y = anchorOffset.y;
		switch (anchor)
		{
			case GraphAnchor.TopRight:
				x = Screen.width - size.x - anchorOffset.x;
				break;
			case GraphAnchor.BottomLeft:
				y = Screen.height - size.y - anchorOffset.y;
				break;
			case GraphAnchor.BottomRight:
				x = Screen.width - size.x - anchorOffset.x;
				y = Screen.height - size.y - anchorOffset.y;
				break;
		}

		return new Rect(x, y, size.x, size.y);
	}

	float ResolveYScale(out float yMaxOut)
	{
		var yMaxLocal = yMaxMs;
		if (autoY)
		{
			var maxSeen = 0f;
			if (totalMetric.stats.valid) maxSeen = Mathf.Max(maxSeen, totalMetric.stats.p99);
			if (cpuMetric.enabled && cpuMetric.stats.valid) maxSeen = Mathf.Max(maxSeen, cpuMetric.stats.p99);
			if (gpuMetric.enabled && gpuMetric.stats.valid) maxSeen = Mathf.Max(maxSeen, gpuMetric.stats.p99);
			for (var i = 0; i < customMetrics.Count; i++)
				if (customMetrics[i].enabled && customMetrics[i].stats.valid)
					maxSeen = Mathf.Max(maxSeen, customMetrics[i].stats.p99);
			if (maxSeen <= 0f) maxSeen = 16f;
			yMaxLocal = Mathf.Min(Mathf.Max(8f, maxSeen * autoYMargin), Mathf.Max(yMaxMs, 8f));
		}

		yMaxOut = yMaxLocal;
		return yMaxLocal > 0f ? 1f / yMaxLocal : 0.1f;
	}

	void DrawMetric(Rect r, float now, float yMax, float invY, Metric m)
	{
		if (count <= 1) return;

		if (useColumnEnvelope)
		{
			DrawMetricColumnEnvelope(r, now, yMax, invY, m);
		}
		else
		{
			// Fallback simple poly (batched)
			BuildPolylinePoints(r, now, invY, m.values, out var pts, out var n);
			DrawPolylineBatched(pts, n, m.color, lineWidth);
		}
	}

	void DrawMetricColumnEnvelope(Rect r, float now, float yMax, float invY, Metric m)
	{
		var w = Mathf.Max(2, Mathf.RoundToInt(r.width));
		EnsureColumnBuffers(w);

		// reset
		for (var i = 0; i < w; i++)
		{
			colMin[i] = float.PositiveInfinity;
			colMax[i] = float.NegativeInfinity;
		}

		// fill per-column min/max
		var minTime = now - windowSeconds;
		var start = (head - count + capacity) % capacity;
		for (var i = 0; i < count; i++)
		{
			var idx = (start + i) % capacity;
			var t = timestamps[idx];
			if (t < minTime) continue;

			var x01 = 1f - Mathf.Clamp01((now - t) / windowSeconds);
			var cx = Mathf.Clamp(Mathf.RoundToInt((w - 1) * x01), 0, w - 1);

			var y01 = Mathf.Clamp01(m.values[idx] * invY);
			var y = Mathf.Lerp(r.yMax, r.y, y01);

			if (y < colMin[cx]) colMin[cx] = y;
			if (y > colMax[cx]) colMax[cx] = y;
		}

		// Build poly on the fly (midpoint of envelope)
		var nPts = 0;
		for (var x = 0; x < w; x++)
		{
			if (!float.IsFinite(colMin[x])) continue;
			var mx = r.x + (x / (float)(w - 1)) * r.width;
			var my = 0.5f * (colMin[x] + colMax[x]);
			if (nPts >= polyPts.Length) Array.Resize(ref polyPts, nPts + 64);
			polyPts[nPts++] = new Vector2(mx, my);
		}

		if (nPts >= 2) DrawPolylineBatched(polyPts, nPts, m.color, lineWidth);
	}

	void BuildPolylinePoints(Rect r, float now, float invY, float[] src, out Vector2[] pts, out int n)
	{
		var start = (head - count + capacity) % capacity;
		var cap = Mathf.Min(count, Mathf.RoundToInt(r.width)); // rough cap
		if (polyPts == null || polyPts.Length < cap) polyPts = new Vector2[Mathf.Max(cap, 128)];
		n = 0;
		var minTime = now - windowSeconds;
		for (var i = 0; i < count; i++)
		{
			var idx = (start + i) % capacity;
			var t = timestamps[idx];
			if (t < minTime) continue;
			var x01 = 1f - Mathf.Clamp01((now - t) / windowSeconds);
			var x = Mathf.Lerp(r.x, r.xMax, x01);
			var y01 = Mathf.Clamp01(src[idx] * invY);
			var y = Mathf.Lerp(r.yMax, r.y, y01);
			polyPts[n++] = new Vector2(x, y);
		}

		pts = polyPts;
	}

	// ------- batched GL drawing -------
	static void EnsureLineMaterial()
	{
		if (lineMat != null) return;
		var s = Shader.Find("Hidden/Internal-Colored");
		lineMat = new Material(s) { hideFlags = HideFlags.HideAndDontSave };
		lineMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
		lineMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
		lineMat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
		lineMat.SetInt("_ZWrite", 0);
	}

	static void DrawPolylineBatched(Vector2[] pts, int n, Color c, float width)
	{
		if (n < 2) return;
		EnsureLineMaterial();
		lineMat.SetPass(0);
		GL.PushMatrix();
		GL.LoadPixelMatrix(0, Screen.width, Screen.height, 0);
		GL.Begin(GL.QUADS);
		GL.Color(c);

		for (var i = 0; i < n - 1; i++)
		{
			Vector2 a = pts[i], b = pts[i + 1];
			var d = b - a;
			var len = d.magnitude;
			if (len <= 0.001f) continue;
			var nrm = new Vector2(-d.y, d.x) / len * (width * 0.5f);
			GL.Vertex(a - nrm);
			GL.Vertex(a + nrm);
			GL.Vertex(b + nrm);
			GL.Vertex(b - nrm);
		}

		GL.End();
		GL.PopMatrix();
	}

	void DrawBackgroundGridAndPanel(Rect graphRect)
	{
		var full = new Rect(graphRect.x, graphRect.y, graphRect.width + statsPanelWidth, graphRect.height);
		EnsureLineMaterial();

		// Full background (graph + panel)
		lineMat.SetPass(0);
		GL.PushMatrix();
		GL.LoadPixelMatrix(0, Screen.width, Screen.height, 0);
		GL.Begin(GL.QUADS);
		GL.Color(backgroundColor);
		GL.Vertex3(full.x, full.y, 0);
		GL.Vertex3(full.xMax, full.y, 0);
		GL.Vertex3(full.xMax, full.yMax, 0);
		GL.Vertex3(full.x, full.yMax, 0);
		GL.End();
		GL.PopMatrix();

		// Border around full
		DrawPolylineBatched(
			new[]
			{
				new Vector2(full.x, full.y), new Vector2(full.xMax, full.y),
				new Vector2(full.xMax, full.yMax), new Vector2(full.x, full.yMax),
				new Vector2(full.x, full.y)
			}, 5, borderColor, gridLineWidth);

		// Vertical separator between graph and panel
		DrawPolylineBatched(new[]
		{
			new Vector2(graphRect.xMax, graphRect.y),
			new Vector2(graphRect.xMax, graphRect.yMax)
		}, 2, borderColor, gridLineWidth);

		// Horizontal grid inside the graph only
		if (gridLinesY > 0)
		{
			for (var i = 1; i < gridLinesY; i++)
			{
				var y = Mathf.Lerp(graphRect.y, graphRect.yMax, i / (float)gridLinesY);
				DrawPolylineBatched(new[] { new Vector2(graphRect.x, y), new Vector2(graphRect.xMax, y) }, 2,
					borderColor, 1f);
			}
		}
	}

	void DrawStatsPanel(Rect statsRect)
	{
		var pad = statsPanelPadding;
		var x = statsRect.x + pad;
		var y = statsRect.y + pad;
		var w = statsRect.width - pad * 2f;

		// Header
		GUI.color = axisTextColor;
		var header = "Metric        avg    min    max    p95    p99";
		GUI.Label(new Rect(x, y, w, fontSize + 6), header, monoStyle);
		y += fontSize + 6;

		// Built-ins
		DrawStatsLine(totalMetric, ref y, x, w);
		if (cpuMetric.enabled) DrawStatsLine(cpuMetric, ref y, x, w);
		if (gpuMetric.enabled) DrawStatsLine(gpuMetric, ref y, x, w);

		// Custom metrics
		for (var i = 0; i < customMetrics.Count; i++)
		{
			var m = customMetrics[i];
			if (m.enabled) DrawStatsLine(m, ref y, x, w);
		}

		GUI.color = Color.white;
	}


	static string TruncPad(string s, int max)
	{
		if (string.IsNullOrEmpty(s)) return new string(' ', max);
		if (s.Length > max) return s.Substring(0, max);
		if (s.Length < max) return s + new string(' ', max - s.Length);
		return s;
	}

	void DrawStatsLine(Metric m, ref float y, float x, float w)
	{
		if (m == null || !m.stats.valid) return;
		GUI.color = m.color;
		var name = TruncPad(m.name, 12);
		var line = string.Format("{0} {1,6:0.00} {2,6:0.00} {3,6:0.00} {4,6:0.00} {5,6:0.00}",
			name, m.stats.avg, m.stats.min, m.stats.max, m.stats.p95, m.stats.p99);
		GUI.Label(new Rect(x, y, w, fontSize + 6), line, monoStyle);
		y += fontSize + 4;
	}


	Rect lastRect;

	void EnsureColumnBuffers(int w)
	{
		if (colMin == null || colMin.Length < w)
		{
			colMin = new float[w];
			colMax = new float[w];
		}

		if (polyPts == null || polyPts.Length < w) polyPts = new Vector2[Mathf.Max(w, 128)];
	}
}
