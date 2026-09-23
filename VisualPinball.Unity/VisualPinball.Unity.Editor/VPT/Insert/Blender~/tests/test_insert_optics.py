# Visual Pinball Engine
# Copyright (C) 2026 freezy and VPE Team
# SPDX-License-Identifier: GPL-3.0-or-later
"""blender -b --python-exit-code 1 --python test_insert_optics.py"""
import sys
from pathlib import Path

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
from test_bake_insert import bake, load_png, texels
import numpy as np


def check_optics(request, folder, result):
	optical = load_png(folder / 'optical-normal.png')
	response = (load_png(folder / 'optical-response.png') + 1) * 0.5
	normal_map = load_png(folder / 'normal.png')
	assert np.isfinite(optical).all() and np.isfinite(response).all()
	assert response.min() >= 0 and response.max() <= 1
	uvs = np.asarray(result['Uvs']).reshape(-1, 2)
	tangents = np.asarray(result['Tangents']).reshape(-1, 4)
	normals = np.asarray(request['Normals']).reshape(-1, 3)
	errors, interiors, glow, edges, transmission = [], [], [], [], []
	flat = set(request['FlatFaces'])
	for face, weights, row, column in texels(uvs, request['TextureSize'], range(len(uvs) // 3)):
		corners = np.arange(face * 3, face * 3 + 3)
		n = np.array([0., 1., 0.]) if face in flat else np.asarray(weights) @ normals[corners]
		n /= np.linalg.norm(n)
		t = np.asarray(weights) @ tangents[corners, :3]
		b = tangents[corners[0], 3] * np.cross(n, t)
		encoded = normal_map[row, column]
		decoded = encoded[0] * t + encoded[1] * b + encoded[2] * n
		decoded /= np.linalg.norm(decoded)
		o = optical[row, column]
		o /= max(np.linalg.norm(o), 1e-6)
		errors.append(np.degrees(np.arccos(np.clip(decoded @ o, -1, 1))))
		g, e, tr = response[row, column]
		if e < 0.01:
			interiors.append(errors[-1])
		glow.append(g); edges.append(e); transmission.append(tr)
	assert len(errors) > 500
	assert np.median(errors) < 3, f'object and tangent normal bakes disagree: {np.median(errors):.2f} degrees'
	# The optical bake intentionally keeps faces sharp; the surface bake is
	# rounded. Compare their directions away from bevels, not across them.
	assert len(interiors) > 500
	assert np.percentile(interiors, 95) < 5, f'optical normal projection or chart rotation is wrong: {np.percentile(interiors, 95):.2f} degrees'
	assert np.mean(glow) > .05 and np.std(glow) > .02, 'scatter bake is empty or uniform'
	assert max(edges) > .2 and np.mean(edges) < .5, 'edge mask is empty or covers whole faces'
	assert min(transmission) < .5 and max(transmission) > .9, 'walls and floor need distinct transmission'
	positions = np.asarray(request['Positions']).reshape(-1, 3)
	lamp = result['OpticalLamp']
	assert len(lamp) == 4 and lamp[1] < positions[:, 1].min() and lamp[3] > 0
	print(f'optics ok: {len(errors)} texels, normal median {np.median(errors):.2f}, p95 {np.percentile(errors, 95):.2f}, glow mean {np.mean(glow):.2f}')


for flat in [None, []]:
	overrides = dict(TextureSize=256, Padding=2, HybridOptics=True, PreferUvPackMaster=False)
	if flat is not None:
		overrides['FlatFaces'] = flat
	request, folder, result, report = bake(**overrides)
	check_optics(request, folder, result)
print('INSERT OPTICS TESTS PASSED')
