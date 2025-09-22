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

	[Header("Time Window & Scale")] [Range(1f, 60f)]
	public float windowSeconds = 10f;

	[Range(60, 480)] public int maxExpectedFps = 240;
	public float yMaxMs = 40f;
	public bool autoY = true;
	public float autoYMargin = 1.2f;

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

	bool initialized = false;
	int lastFTCount;

	float lastCpuBusyMs = 0f;

	// Public API for custom metrics
	public int RegisterCustomMetric(string name, Color color, Func<float> sampler, float scale = 1f,
		bool enabled = true)
	{
		if (string.IsNullOrEmpty(name) || sampler == null) return -1;
		var m = new Metric(name, color, sampler, scale, enabled, capacity);
		customMetrics.Add(m);
		return customMetrics.Count - 1;
	}

	public void SetCustomMetricEnabled(int index, bool enabled)
	{
		if (index >= 0 && index < customMetrics.Count) customMetrics[index].enabled = enabled;
	}

	public void ClearCustomMetrics() => customMetrics.Clear();

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
		public float avg, p95, p99;
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
	float lastCpuMs = 0f, lastGpuMs = 0f;

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
		cpuMetric   = new Metric("CPU (ms)",   cpuColor,   () => lastCpuMs, 1f, enableCpuGpuCollection, capacity);
		gpuMetric   = new Metric("GPU (ms)",   gpuColor,   () => lastGpuMs, 1f, enableCpuGpuCollection, capacity);

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

		int needed = Mathf.Max(8, Mathf.CeilToInt(windowSeconds * maxExpectedFps));
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
		ResizeMetric(cpuMetric,  capacity);
		ResizeMetric(gpuMetric,  capacity);
		for (int i = 0; i < customMetrics.Count; i++)
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
			uint got = enableCpuGpuCollection ? FrameTimingManager.GetLatestTimings(1, ftBuf) : 0;
			lastFTCount = (int)got;
			haveFT = got > 0;
			if (haveFT)
			{
				var ft = ftBuf[0];
				lastCpuMs = Mathf.Max(0f, (float)ft.cpuFrameTime);
				lastGpuMs = Mathf.Max(0f, (float)ft.gpuFrameTime);
			}
		}
		else
		{
			haveFT = false;
			lastCpuMs = 0f;
			lastGpuMs = 0f;
		}

		// Sample
		float now = Time.unscaledTime;
		float totalMs = totalMetric.sampler();
		float cpuMs = cpuMetric.enabled ? cpuMetric.sampler() : 0f;
		float gpuMs = gpuMetric.enabled ? gpuMetric.sampler() : 0f;
		WriteSample(now, totalMs, cpuMs, gpuMs);

		// Custom metrics
		int idxPrev = (head - 1 + capacity) % capacity;
		for (int i = 0; i < customMetrics.Count; i++)
		{
			var m = customMetrics[i];
			if (!m.enabled || m.sampler == null)
			{
				m.values[idxPrev] = 0f;
				continue;
			}

			float v = 0f;
			try
			{
				v = m.sampler() * m.scale;
			}
			catch
			{
			}

			m.values[idxPrev] = v;
		}

		// FPS smoothing & text throttling
		float instFps = Time.unscaledDeltaTime > 0f ? 1f / Time.unscaledDeltaTime : 0f;
		float a = 1f - Mathf.Exp(-Time.unscaledDeltaTime / fpsSmoothTau);
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
			for (int i = 0; i < customMetrics.Count; i++)
				if (customMetrics[i].enabled)
					RecomputeStats(customMetrics[i]);
		}

		// Capture for next frame
		if (enableCpuGpuCollection) FrameTimingManager.CaptureFrameTimings();
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
			float candidate = Mathf.Max(lastCpuMs, lastGpuMs);
			if (candidate > 0f) return candidate;
		}

		return Time.unscaledDeltaTime * 1000f;
	}

	void RecomputeStats(Metric m)
	{
		int visible = CollectVisibleValues(m.values, scratchValues, out float sum);
		if (visible <= 0)
		{
			m.stats = default;
			m.cachedLegend = $"{m.name}: -";
			return;
		}

		Array.Sort(scratchValues, 0, visible);
		float avg = sum / visible;
		float p95 = PercentileSorted(scratchValues, visible, 0.95f);
		float p99 = PercentileSorted(scratchValues, visible, 0.99f);

		m.stats.avg = avg;
		m.stats.p95 = p95;
		m.stats.p99 = p99;
		m.stats.valid = true;
		m.cachedLegend = $"{m.name}: avg {avg:0.0} ms  •  p95 {p95:0.0}  •  p99 {p99:0.0}";
	}

	int CollectVisibleValues(float[] src, float[] dst, out float sum)
	{
		sum = 0f;
		if (count == 0) return 0;
		float now = Time.unscaledTime, minTime = now - windowSeconds;
		int visible = 0, start = (head - count + capacity) % capacity;
		for (int i = 0; i < count; i++)
		{
			int idx = (start + i) % capacity;
			float t = timestamps[idx];
			if (t >= minTime)
			{
				float v = src[idx];
				dst[visible++] = v;
				sum += v;
			}
		}

		return visible;
	}

	static float PercentileSorted(float[] sorted, int n, float p)
	{
		if (n == 0) return 0f;
		if (p <= 0f) return sorted[0];
		if (p >= 1f) return sorted[n - 1];
		float f = (n - 1) * p;
		int i0 = Mathf.FloorToInt(f), i1 = Math.Min(n - 1, i0 + 1);
		float t = f - i0;
		return Mathf.Lerp(sorted[i0], sorted[i1], t);
	}

	// ---------- RENDER ----------
	void OnGUI()
	{
		if (!visible) return;

		// 🧨 Critical fix: only draw on Repaint to avoid extra work during key/mouse events
		var evt = Event.current;
		if (evt == null || evt.type != EventType.Repaint) return;

		Rect r = ResolveRect();
		if (r.width <= 4f || r.height <= 4f) return;

		DrawBackgroundAndGrid(r);

		float now = Time.unscaledTime;
		float invY = ResolveYScale(out float yMax);

		// Draw metrics (batched, column-envelope)
		DrawMetric(r, now, yMax, invY, totalMetric);
		if (cpuMetric.enabled) DrawMetric(r, now, yMax, invY, cpuMetric);
		if (gpuMetric.enabled) DrawMetric(r, now, yMax, invY, gpuMetric);
		for (int i = 0; i < customMetrics.Count; i++)
			if (customMetrics[i].enabled)
				DrawMetric(r, now, yMax, invY, customMetrics[i]);

		// Labels (legend + axis + fps)
		GUI.Label(new Rect(r.x + 6, r.y - (fontSize + 2), 120, fontSize + 4), $"{yMax:0.#} ms", labelStyle);
		GUI.Label(new Rect(r.x + 6, r.y + r.height * 0.5f - (fontSize + 2), 120, fontSize + 4),
			$"{(yMax * 0.5f):0.#} ms", labelStyle);
		GUI.Label(new Rect(r.x + 6, r.yMax - (fontSize + 2), 120, fontSize + 4), $"0 ms", labelStyle);

		// FPS
		GUI.Label(new Rect(r.x + 8, r.y + 6, 120, fontSize + 6), fpsText, labelStyle);
		GUI.Label(new Rect(r.x + 125, r.y + 6, 180, fontSize + 6),$"FT:{lastFTCount} {(haveFT ? "OK" : "OFF")}", labelStyle);

		// Legends (cached strings)
		float lx = r.x + 8, ly = Mathf.Max(r.y + 24, r.yMax - 18 - (fontSize + 4) * (3 + customMetrics.Count));
		GUI.color = totalMetric.color;
		GUI.Label(new Rect(lx, ly, 1000, fontSize + 6), totalMetric.cachedLegend, labelStyle);
		ly += fontSize + 4;
		if (cpuMetric.enabled)
		{
			GUI.color = cpuMetric.color;
			GUI.Label(new Rect(lx, ly, 1000, fontSize + 6), cpuMetric.cachedLegend, labelStyle);
			ly += fontSize + 4;
		}

		if (gpuMetric.enabled)
		{
			GUI.color = gpuMetric.color;
			GUI.Label(new Rect(lx, ly, 1000, fontSize + 6), gpuMetric.cachedLegend, labelStyle);
			ly += fontSize + 4;
		}

		for (int i = 0; i < customMetrics.Count; i++)
		{
			var m = customMetrics[i];
			if (!m.enabled || !m.stats.valid) continue;
			GUI.color = m.color;
			GUI.Label(new Rect(lx, ly, 1400, fontSize + 6), m.cachedLegend, labelStyle);
			ly += fontSize + 4;
		}

		GUI.color = Color.white;

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
		float yMaxLocal = yMaxMs;
		if (autoY)
		{
			float maxSeen = 0f;
			if (totalMetric.stats.valid) maxSeen = Mathf.Max(maxSeen, totalMetric.stats.p99);
			if (cpuMetric.enabled && cpuMetric.stats.valid) maxSeen = Mathf.Max(maxSeen, cpuMetric.stats.p99);
			if (gpuMetric.enabled && gpuMetric.stats.valid) maxSeen = Mathf.Max(maxSeen, gpuMetric.stats.p99);
			for (int i = 0; i < customMetrics.Count; i++)
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
			BuildPolylinePoints(r, now, invY, m.values, out var pts, out int n);
			DrawPolylineBatched(pts, n, m.color, lineWidth);
		}
	}

	void DrawMetricColumnEnvelope(Rect r, float now, float yMax, float invY, Metric m)
	{
		int w = Mathf.Max(2, Mathf.RoundToInt(r.width));
		EnsureColumnBuffers(w);

		// reset
		for (int i = 0; i < w; i++)
		{
			colMin[i] = float.PositiveInfinity;
			colMax[i] = float.NegativeInfinity;
		}

		// fill per-column min/max
		float minTime = now - windowSeconds;
		int start = (head - count + capacity) % capacity;
		for (int i = 0; i < count; i++)
		{
			int idx = (start + i) % capacity;
			float t = timestamps[idx];
			if (t < minTime) continue;

			float x01 = 1f - Mathf.Clamp01((now - t) / windowSeconds);
			int cx = Mathf.Clamp(Mathf.RoundToInt((w - 1) * x01), 0, w - 1);

			float y01 = Mathf.Clamp01(m.values[idx] * invY);
			float y = Mathf.Lerp(r.yMax, r.y, y01);

			if (y < colMin[cx]) colMin[cx] = y;
			if (y > colMax[cx]) colMax[cx] = y;
		}

		// Build poly on the fly (midpoint of envelope)
		int nPts = 0;
		for (int x = 0; x < w; x++)
		{
			if (!float.IsFinite(colMin[x])) continue;
			float mx = r.x + (x / (float)(w - 1)) * r.width;
			float my = 0.5f * (colMin[x] + colMax[x]);
			if (nPts >= polyPts.Length) Array.Resize(ref polyPts, nPts + 64);
			polyPts[nPts++] = new Vector2(mx, my);
		}

		if (nPts >= 2) DrawPolylineBatched(polyPts, nPts, m.color, lineWidth);
	}

	void BuildPolylinePoints(Rect r, float now, float invY, float[] src, out Vector2[] pts, out int n)
	{
		int start = (head - count + capacity) % capacity;
		int cap = Mathf.Min(count, Mathf.RoundToInt(r.width)); // rough cap
		if (polyPts == null || polyPts.Length < cap) polyPts = new Vector2[Mathf.Max(cap, 128)];
		n = 0;
		float minTime = now - windowSeconds;
		for (int i = 0; i < count; i++)
		{
			int idx = (start + i) % capacity;
			float t = timestamps[idx];
			if (t < minTime) continue;
			float x01 = 1f - Mathf.Clamp01((now - t) / windowSeconds);
			float x = Mathf.Lerp(r.x, r.xMax, x01);
			float y01 = Mathf.Clamp01(src[idx] * invY);
			float y = Mathf.Lerp(r.yMax, r.y, y01);
			polyPts[n++] = new Vector2(x, y);
		}

		pts = polyPts;
	}

	// ------- batched GL drawing -------
	static void EnsureLineMaterial()
	{
		if (lineMat != null) return;
		Shader s = Shader.Find("Hidden/Internal-Colored");
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

		for (int i = 0; i < n - 1; i++)
		{
			Vector2 a = pts[i], b = pts[i + 1];
			Vector2 d = b - a;
			float len = d.magnitude;
			if (len <= 0.001f) continue;
			Vector2 nrm = new Vector2(-d.y, d.x) / len * (width * 0.5f);
			GL.Vertex(a - nrm);
			GL.Vertex(a + nrm);
			GL.Vertex(b + nrm);
			GL.Vertex(b - nrm);
		}

		GL.End();
		GL.PopMatrix();
	}

	void DrawBackgroundAndGrid(Rect r)
	{
		// background
		EnsureLineMaterial();
		lineMat.SetPass(0);
		GL.PushMatrix();
		GL.LoadPixelMatrix(0, Screen.width, Screen.height, 0);
		GL.Begin(GL.QUADS);
		GL.Color(backgroundColor);
		GL.Vertex3(r.x, r.y, 0);
		GL.Vertex3(r.xMax, r.y, 0);
		GL.Vertex3(r.xMax, r.yMax, 0);
		GL.Vertex3(r.x, r.yMax, 0);
		GL.End();
		GL.PopMatrix();

		// border
		DrawPolylineBatched(
			new[]
			{
				new Vector2(r.x, r.y), new Vector2(r.xMax, r.y), new Vector2(r.xMax, r.yMax), new Vector2(r.x, r.yMax),
				new Vector2(r.x, r.y)
			}, 5, borderColor, gridLineWidth);

		// horizontal grid
		if (gridLinesY > 0)
		{
			for (int i = 1; i < gridLinesY; i++)
			{
				float y = Mathf.Lerp(r.y, r.yMax, i / (float)gridLinesY);
				DrawPolylineBatched(new[] { new Vector2(r.x, y), new Vector2(r.xMax, y) }, 2, borderColor, 1f);
			}
		}
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
