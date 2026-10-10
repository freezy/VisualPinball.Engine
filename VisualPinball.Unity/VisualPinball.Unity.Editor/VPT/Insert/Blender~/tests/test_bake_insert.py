# Visual Pinball Engine
# Copyright (C) 2026 freezy and VPE Team
#
# This program is free software: you can redistribute it and/or modify
# it under the terms of the GNU General Public License as published by
# the Free Software Foundation, either version 3 of the License, or
# (at your option) any later version.
#
# This program is distributed in the hope that it will be useful,
# but WITHOUT ANY WARRANTY; without even the implied warranty of
# MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
# GNU General Public License for more details.
#
# You should have received a copy of the GNU General Public License
# along with this program. If not, see <https://www.gnu.org/licenses/>.

"""Headless tests for bake_insert.py.

Run with

    blender -b --python-exit-code 1 --python test_bake_insert.py

It uses UVPackMaster when Blender has it installed and needs nothing else.
insert.json is a request written by Unity for the component's default triangle
with a hole in each corner.
"""

import json
import struct
import sys
import tempfile
from pathlib import Path

import bpy
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from mathutils.interpolate import poly_3d_calc

HERE = Path(__file__).resolve().parent
# No __pycache__ next to the script, where Unity would import it.
sys.dont_write_bytecode = True
sys.path.insert(0, str(HERE.parent))
import bake_insert  # noqa: E402

FIXTURE = json.loads((HERE / 'insert.json').read_text(encoding='utf-8'))


def bake(**overrides):
	request = dict(FIXTURE, **overrides)
	folder = Path(tempfile.mkdtemp(prefix='bake-insert-'))
	report = {'Warnings': [], 'Timings': {}}
	result = bake_insert.run(request, folder, report)
	return request, folder, result, report


def check_unwrap_fallback():
	"""A rejected unwrap must restore the generator's island shapes across mode switches."""
	request = dict(FIXTURE, Unwrap=True, PreferUvPackMaster=False)
	bake_insert.clear_scene()
	positions = np.asarray(request['Positions']).reshape(-1, 3)
	triangles = np.asarray(request['Triangles']).reshape(-1, 3)
	normals = np.asarray(request['Normals']).reshape(-1, 3)
	uvs = np.asarray(request['Uvs'], dtype=np.float32).reshape(-1, 2)
	regular = bake_insert.build_mesh('Fallback', positions.tolist(), triangles.tolist(), normals, uvs)
	bake_insert.mark_seams(regular.data, request['Charts'])
	original_folded, original_shrink = bake_insert.folded, bake_insert.shrink_walls
	checked = []

	def check_restored(mesh, settings):
		actual = bake_insert.uv_array(mesh, bake_insert.UV_NAME)
		# A chart may contain several disconnected islands. Their scales may
		# differ after averaging, but each triangle must retain its shape.
		for face in range(len(triangles)):
			corners = slice(face * 3, face * 3 + 3)
			expected = uvs[corners] - uvs[corners].mean(axis=0)
			found = actual[corners] - actual[corners].mean(axis=0)
			expected /= np.linalg.norm(expected)
			found /= np.linalg.norm(found)
			assert np.allclose(found, expected, atol=1e-3), f'fallback did not restore triangle {face}'
		checked.append(True)
		original_shrink(mesh, settings)

	try:
		# Force rejection of the actual Blender unwrap, independent of solver version.
		bake_insert.folded = lambda _: True
		bake_insert.shrink_walls = check_restored
		report = {'Warnings': []}
		bake_insert.unwrap(regular, request, report)
		assert checked and any('generator' in warning for warning in report['Warnings'])
		packed = bake_insert.uv_array(regular.data, bake_insert.UV_NAME)
		assert not original_folded(packed)
		assert bake_insert.overlapping(packed, request['TextureSize']) is None
	finally:
		bake_insert.folded, bake_insert.shrink_walls = original_folded, original_shrink
	print('unwrap fallback ok: generator shapes restored, no folds or overlapping texels')


def texels(uvs, size, faces):
	"""Every texel centre inside a triangle: (triangle, barycentric weights, row, column)."""
	for face in faces:
		a, b, c = uvs[face * 3:face * 3 + 3] * size
		low = np.floor(np.minimum(np.minimum(a, b), c)).astype(int)
		high = np.ceil(np.maximum(np.maximum(a, b), c)).astype(int)
		x, y = np.meshgrid(np.arange(low[0], high[0]) + 0.5, np.arange(low[1], high[1]) + 0.5)
		det = (b[1] - c[1]) * (a[0] - c[0]) + (c[0] - b[0]) * (a[1] - c[1])
		u = ((b[1] - c[1]) * (x - c[0]) + (c[0] - b[0]) * (y - c[1])) / det
		v = ((c[1] - a[1]) * (x - c[0]) + (a[0] - c[0]) * (y - c[1])) / det
		w = 1 - u - v
		inside = (u > 1e-3) & (v > 1e-3) & (w > 1e-3)
		for uu, vv, ww, xx, yy in zip(u[inside], v[inside], w[inside], x[inside], y[inside]):
			yield face, (uu, vv, ww), int(yy), int(xx)


def load_png(path):
	image = bpy.data.images.load(str(path), check_existing=False)
	image.colorspace_settings.name = 'Non-Color'
	pixels = np.empty(image.size[0] * image.size[1] * 4, dtype=np.float32)
	image.pixels.foreach_get(pixels)
	size = image.size[0]
	bpy.data.images.remove(image)
	return pixels.reshape(size, size, 4)[:, :, :3] * 2 - 1


def check_layout(request, folder, result, report):
	corners = len(request['Triangles'])
	uvs = np.asarray(result['Uvs']).reshape(-1, 2)
	tangents = np.asarray(result['Tangents']).reshape(-1, 4)
	assert len(uvs) == corners and len(tangents) == corners
	assert np.allclose(np.linalg.norm(tangents[:, :3], axis=1), 1, atol=1e-3), 'tangents must be unit length'
	assert set(np.unique(tangents[:, 3])) <= {-1.0, 1.0}, 'bitangent signs must be one or minus one'

	assert uvs.min() >= 0 and uvs.max() <= 1, 'UVs must stay in the unit square'
	assert not bake_insert.folded(uvs), 'no triangle may be laid out the wrong way round'
	assert bake_insert.overlapping(uvs, request['TextureSize']) is None, 'no two triangles may share a texel'

	# Islands are packed twice the padding apart, so their padding cannot meet.
	charts = np.asarray(request['Charts'])
	triangles = np.asarray(request['Triangles']).reshape(-1, 3)
	edges = {}
	for face, corners_ in enumerate(triangles):
		for k in range(3):
			key = tuple(sorted((corners_[k], corners_[(k + 1) % 3])))
			edges.setdefault(key, []).append((face, k))
	segments, owners = [], []
	for key, faces in edges.items():
		if len(faces) == 2 and charts[faces[0][0]] == charts[faces[1][0]]:
			continue
		for face, k in faces:
			segments.append((uvs[face * 3 + k], uvs[face * 3 + (k + 1) % 3]))
			owners.append(charts[face])
	segments = np.asarray(segments) * request['TextureSize']
	owners = np.asarray(owners)
	gap = np.inf
	for i in range(len(segments)):
		others = owners != owners[i]
		gap = min(gap, segment_distance(segments[i], segments[others]).min())
	assert gap >= 2 * request['Padding'] - 1, f'islands are only {gap:.1f} px apart'

	png = (folder / 'normal.png').read_bytes()
	assert struct.unpack('>II', png[16:24]) == (request['TextureSize'],) * 2
	assert png[24] == 8, 'expected an 8-bit PNG'
	print(f'layout ok: {report["Packer"]}, fill {report["UvFill"]:.2f}, closest islands {gap:.1f} px')


def check_density(request, result, unwrapped=True):
	"""Every chart keeps its shape, the starburst included, and the walls get half the floors' texels a side."""
	uvs = np.asarray(result['Uvs']).reshape(-1, 3, 2)
	corners = np.asarray(request['Positions']).reshape(-1, 3)[np.asarray(request['Triangles']).reshape(-1, 3)]
	uv_area = np.abs(np.cross(uvs[:, 1] - uvs[:, 0], uvs[:, 2] - uvs[:, 0]))
	surface = np.linalg.norm(np.cross(corners[:, 1] - corners[:, 0], corners[:, 2] - corners[:, 0]), axis=1)
	usable = surface > 1e-10
	density = uv_area / np.maximum(surface, 1e-12)

	flat = np.zeros(len(uvs), dtype=bool)
	flat[request['FlatFaces']] = True
	if unwrapped:
		# The starburst is not stretched anywhere: its facets are laid out at their true size.
		spread = np.percentile(density[flat & usable], 95) / np.percentile(density[flat & usable], 5)
		assert spread < 1.1, f'the starburst is stretched: its faces vary {spread:.2f}x in size'
	else:
		spread = float('nan')

	walls = np.isin(np.asarray(request['Charts']), request['WallCharts'])
	ratio = np.median(density[walls & usable]) / np.median(density[~walls & ~flat & usable])
	assert 0.2 < ratio < 0.3, f"walls get {ratio:.2f} of the floors' density, not a quarter"
	print(f'density ok: starburst spread {spread:.3f}x, walls at {ratio:.2f} of the floors')


def segment_distance(segment, others):
	"""The distance from one segment to each of others, assuming none cross."""
	def point_to(points, a, b):
		ab = b - a
		t = np.clip(((points - a) * ab).sum(-1) / np.maximum((ab * ab).sum(-1), 1e-12), 0, 1)
		return np.linalg.norm(points - (a + t[..., None] * ab), axis=-1)
	a, b = segment
	return np.minimum.reduce([
		point_to(others[:, 0], a, b), point_to(others[:, 1], a, b),
		point_to(np.broadcast_to(a, others[:, 0].shape), others[:, 0], others[:, 1]),
		point_to(np.broadcast_to(b, others[:, 0].shape), others[:, 0], others[:, 1]),
	])


def regular_tilts(request, folder, result):
	"""The tilt of every texel outside the flat region, in degrees."""
	normals = load_png(folder / 'normal.png')
	uvs = np.asarray(result['Uvs']).reshape(-1, 2)
	flat = set(request['FlatFaces'])
	regular = [face for face in range(len(request['Charts'])) if face not in flat]
	return np.asarray([np.degrees(np.arccos(np.clip(normals[row, column, 2], -1, 1)))
		for _, _, row, column in texels(uvs, request['TextureSize'], regular)])


def check_identity(request, folder, result):
	"""Without a bevel the high-poly is the mesh itself, so everything outside the flat region bakes flat."""
	tilts = regular_tilts(request, folder, result)
	assert tilts.max() < 1.5, f'a flat surface baked with a tilt of {tilts.max():.1f} degrees'
	print(f'identity ok: {len(tilts)} texels, largest tilt {tilts.max():.2f} degrees')


def check_bevel(request, folder, result):
	"""The edges are rounded off, a thin band of tilted texels along each, and nothing is torn up."""
	tilts = regular_tilts(request, folder, result)
	rounded = (tilts > 5).mean()
	assert 0.005 < rounded < 0.3, f'{rounded:.1%} of the texels are tilted by the bevel'
	# Where three surfaces meet in a hollow corner a few texels lean further
	# than a rounded edge does, up to the high seventies at the holes'
	# corners, but nothing turns round.
	steep = (tilts > 60).mean()
	assert steep < 1e-4 and tilts.max() < 80, f'{steep:.3%} of the texels baked steeper than 60 degrees, up to {tilts.max():.0f}'
	print(f'bevel ok: {rounded:.1%} of {len(tilts)} texels rounded, {steep:.4%} steeper than 60 degrees')


def turned_high(regular, request):
	"""A high-poly whose normals all lean 20 degrees about X, so every texel has a direction to get right."""
	high = ORIGINAL_BUILD_HIGH(regular, dict(request, EdgeBevel=0.0))
	angle = np.radians(20.0)
	turn = np.array([[1, 0, 0], [0, np.cos(angle), -np.sin(angle)], [0, np.sin(angle), np.cos(angle)]])
	normals = np.asarray(request['Normals']).reshape(-1, 3) @ turn.T
	high.data.normals_split_custom_set(normals.tolist())
	return high


ORIGINAL_BUILD_HIGH = bake_insert.build_high


def check_directions(request, folder, result):
	"""Decoded the way Unity does with the returned tangents, the map gives the high-poly's normals.

	Only where the high-poly turns away from the mesh, since that is where a
	wrong tangent basis shows; and a mirrored bitangent must fail, or the check
	proves nothing.
	"""
	normals = load_png(folder / 'normal.png')
	uvs = np.asarray(result['Uvs']).reshape(-1, 2)
	tangents = np.asarray(result['Tangents']).reshape(-1, 4)
	positions = np.asarray(request['Positions']).reshape(-1, 3)
	triangles = np.asarray(request['Triangles']).reshape(-1, 3)
	corner_normals = np.asarray(request['Normals']).reshape(-1, 3)

	high = bpy.data.objects['High'].evaluated_get(bpy.context.evaluated_depsgraph_get())
	mesh = high.to_mesh()
	vertices = [vertex.co.copy() for vertex in mesh.vertices]
	tree = BVHTree.FromPolygons(vertices, [polygon.vertices[:] for polygon in mesh.polygons])
	high_normals = [corner.vector.copy() for corner in mesh.corner_normals]

	flat = set(request['FlatFaces'])
	regular = [face for face in range(len(triangles)) if face not in flat]
	errors, mirrored = [], []
	for face, weights, row, column in texels(uvs, request['TextureSize'], regular):
		corners = range(face * 3, face * 3 + 3)
		position = sum(w * positions[triangles[face][k]] for k, w in enumerate(weights))
		normal = sum(w * corner_normals[c] for w, c in zip(weights, corners))
		normal /= np.linalg.norm(normal)
		tangent = sum(w * tangents[c, :3] for w, c in zip(weights, corners))
		bitangent = tangents[face * 3, 3] * np.cross(normal, tangent)

		origin = Vector(position + normal * request['RegularExtrusion'] * 2)
		hit, _, index, _ = tree.ray_cast(origin, Vector(-normal))
		if hit is None:
			continue
		polygon = mesh.polygons[index]
		blend = poly_3d_calc([vertices[v] for v in polygon.vertices], hit)
		expected = np.asarray(sum((w * high_normals[polygon.loop_start + k] for k, w in enumerate(blend)), Vector()).normalized())
		if np.degrees(np.arccos(np.clip(expected @ normal, -1, 1))) < 10:
			continue

		encoded = normals[row, column]
		for sign, out in ((1, errors), (-1, mirrored)):
			decoded = encoded[0] * tangent + sign * encoded[1] * bitangent + encoded[2] * normal
			decoded /= np.linalg.norm(decoded)
			out.append(np.degrees(np.arccos(np.clip(decoded @ expected, -1, 1))))
	high.to_mesh_clear()

	assert len(errors) > 50, f'only {len(errors)} turned texels to check'
	median, worst = np.median(errors), np.percentile(errors, 95)
	assert median < 4 and worst < 15, f'decoded normals are off by {median:.1f} degrees (95%: {worst:.1f})'
	assert np.median(mirrored) > 2 * median + 5, 'a mirrored bitangent decodes just as well, so the check is blind'
	print(f'directions ok: {len(errors)} texels, off by {median:.1f} degrees (95%: {worst:.1f}), mirrored {np.median(mirrored):.1f}')


def check_surface_grain():
	"""Both normal projections pick up mild grain; zero strength and optical maps stay smooth."""
	plane = dict(
		Positions=[0, 0, 0, 0, 0, 0.02, 0.02, 0, 0.02, 0.02, 0, 0],
		Triangles=[0, 1, 2, 0, 2, 3], Normals=[0, 1, 0] * 6,
		Uvs=[0, 0, 1, 0, 1, 1, 0, 0, 1, 1, 0, 1],
		Charts=[0, 0], WallCharts=[], FlatFaces=[0], FlatHeight=0,
		TextureSize=128, Padding=4, EdgeBevel=0, Unwrap=False,
		PreferUvPackMaster=False, Gpu=False, HybridOptics=True,
		NormalGrainSize=0.001,
	)
	_, smooth_folder, smooth_result, _ = bake(**plane, NormalGrainStrength=0)
	request, grain_folder, result, _ = bake(**plane, NormalGrainStrength=0.25)
	assert result['Uvs'] == smooth_result['Uvs'] and result['Tangents'] == smooth_result['Tangents']
	smooth, grain = load_png(smooth_folder / 'normal.png'), load_png(grain_folder / 'normal.png')
	uvs = np.asarray(result['Uvs']).reshape(-1, 2)
	for face in range(2):
		samples = np.asarray([grain[row, col] for _, _, row, col in texels(uvs, request['TextureSize'], [face])])
		tilts = np.degrees(np.arctan2(np.linalg.norm(samples[:, :2], axis=1), samples[:, 2]))
		assert 0.5 < np.median(tilts) < 5, f'grain should be subtle in projection {face}: {np.median(tilts):.2f} degrees'
		assert np.percentile(tilts, 99) < 10, 'grain must not obscure the larger facets'
		assert np.std(samples[:, 0]) > 0.01 and np.std(samples[:, 1]) > 0.01, 'grain must vary in both tangent directions'
		print(f'surface grain ok: projection {face}, median {np.median(tilts):.2f} degrees, 99% {np.percentile(tilts, 99):.2f}')
	assert np.max(np.abs(smooth[:, :, :2])) < 0.005, 'zero strength must keep the smooth normal map'
	for name in ('optical-normal.png', 'optical-response.png'):
		assert np.allclose(load_png(smooth_folder / name), load_png(grain_folder / name), atol=2 / 65535), f'grain changed {name}'
	print('surface grain ok: optical normals and response unchanged')


def check_plain_stipple_target():
	"""A plain, channel-free cap receives the pyramid relief from a separate source."""
	positions = np.array([
		[0, 0, 0], [0, 0, 0.02], [0.02, 0, 0.02], [0.02, 0, 0],
	], dtype=np.float32)
	high_positions = np.vstack((positions, [0.01, 0.003, 0.01]))
	high_triangles = np.array([[0, 1, 4], [1, 2, 4], [2, 3, 4], [3, 0, 4]])
	face_normals = np.cross(
		high_positions[high_triangles[:, 1]] - high_positions[high_triangles[:, 0]],
		high_positions[high_triangles[:, 2]] - high_positions[high_triangles[:, 0]]
	)
	face_normals /= np.linalg.norm(face_normals, axis=1)[:, None]
	high_normals = np.repeat(face_normals, 3, axis=0)
	request, folder, result, _ = bake(
		Positions=positions.ravel().tolist(), Triangles=[0, 1, 2, 0, 2, 3],
		Normals=[0, 1, 0] * 6, Uvs=[0, 0, 0, 1, 1, 1, 0, 0, 1, 1, 1, 0],
		Charts=[0, 0], WallCharts=[], FlatFaces=[0, 1], FlatHeight=0,
		HighPositions=high_positions.ravel().tolist(), HighTriangles=high_triangles.ravel().tolist(),
		HighNormals=high_normals.ravel().tolist(), HighUvs=[0, 0, 0, 1, 0.5, 0.5] * 4,
		TextureSize=128, Padding=4, EdgeBevel=0, Unwrap=False,
		PreferUvPackMaster=False, Gpu=False, HybridOptics=False,
		NormalGrainStrength=0, RegularExtrusion=0.000254, FlatExtrusion=0.004,
	)
	uvs = np.asarray(result['Uvs']).reshape(-1, 2)
	normals = load_png(folder / 'normal.png')
	tilts = np.array([np.degrees(np.arctan2(np.linalg.norm(normals[row, col, :2]), normals[row, col, 2]))
		for _, _, row, col in texels(uvs, request['TextureSize'], request['FlatFaces'])])
	assert len(tilts) > 100 and (tilts > 5).mean() > 0.5, 'the detailed pyramids did not reach the plain cap map'
	print(f'plain stipple target ok: {len(tilts)} cap texels, {(tilts > 5).mean():.0%} tilted')


if __name__ == '__main__':
	check_plain_stipple_target()
	check_surface_grain()
	check_unwrap_fallback()
	request, folder, result, report = bake(EdgeBevel=0.0)
	check_layout(request, folder, result, report)
	check_identity(request, folder, result)

	bake_insert.build_high = turned_high
	request, folder, result, report = bake()
	bake_insert.build_high = ORIGINAL_BUILD_HIGH
	check_directions(request, folder, result)

	request, folder, result, report = bake()
	check_layout(request, folder, result, report)
	check_density(request, result)
	check_bevel(request, folder, result)

	# Without a raised middle there is no flat pass, and without a bevel then
	# every texel bakes flat.
	request, folder, result, report = bake(FlatFaces=[], EdgeBevel=0.0, Unwrap=False, PreferUvPackMaster=False)
	check_layout(request, folder, result, report)
	check_identity(request, folder, result)
	assert report['Packer'] == 'Blender'

	# Kept as the generator laid it out, the walls still shrink.
	request, folder, result, report = bake(Unwrap=False)
	check_density(request, result, unwrapped=False)

	print('BAKE INSERT TESTS PASSED', bpy.app.version_string)
