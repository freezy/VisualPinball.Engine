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

"""Optimizes a procedural insert's UVs and bakes its normal map.

Unity runs it as

    blender -b --python bake_insert.py -- <request.json>

and it writes, next to the request, normal.png and result.json: the new UV and
tangent of every corner, in the request's triangle order, plus a report. When
anything fails, result.json carries the error and Blender exits non-zero. With
`-- --probe` instead, it only prints what this Blender has installed.

The mesh arrives in Unity's local space and stays in it. Unity and Blender
agree numerically on everything that matters here: a triangle faces where
cross(b - a, c - a) points, the bitangent is sign * cross(normal, tangent),
and V runs up the image. The insert just lies in Blender's XZ plane, which
nothing below cares about.

The bake itself is the Starburst Normal Baker extension's: the starburst is
baked from a flat copy of itself, the rest from the mesh as it is, both from a
high-poly whose creases are rounded off.
"""

import importlib
import json
import sys
import time
import traceback
from pathlib import Path

import addon_utils
import bmesh
import bpy
import numpy as np

BAKER = 'starburst_normal_baker'
UVPM = 'uvpackmaster4'
UV_NAME = 'UVMap'
GENERATOR_UV_NAME = 'Generator'

# Must match InsertBakeRequest.CurrentVersion.
VERSION = 1


class BakeFailure(Exception):
	pass


def find_module(name):
	"""The full module name of an installed extension or legacy add-on."""
	for module in addon_utils.modules(refresh=False):
		if module.__name__.split('.')[-1] == name:
			return module.__name__
	return None


def enable(name):
	"""Enables an extension for this run only, without touching the user's preferences."""
	module_name = find_module(name)
	if module_name is None:
		return None
	loaded, enabled = addon_utils.check(module_name)
	if not enabled or not loaded:
		if addon_utils.enable(module_name, default_set=False, persistent=False) is None:
			return None
	return module_name


def clear_scene():
	for obj in list(bpy.data.objects):
		bpy.data.objects.remove(obj, do_unlink=True)
	for mesh in list(bpy.data.meshes):
		bpy.data.meshes.remove(mesh)


def build_mesh(name, positions, triangles, normals, uvs):
	mesh = bpy.data.meshes.new(name)
	# Smooth faces, so the corner normals alone decide the shading. Flat ones
	# would bake their face normal wherever the generator shades smoothly.
	mesh.from_pydata(positions, [], triangles, shade_flat=False)
	if len(mesh.polygons) != len(triangles):
		raise BakeFailure(f'{name}: Blender kept {len(mesh.polygons)} of {len(triangles)} triangles.')
	uv = mesh.uv_layers.new(name=GENERATOR_UV_NAME)
	uv.data.foreach_set('uv', uvs.ravel())
	mesh.normals_split_custom_set(normals.tolist())
	obj = bpy.data.objects.new(name, mesh)
	bpy.context.scene.collection.objects.link(obj)
	return obj


def select_only(obj):
	for other in bpy.context.scene.objects:
		other.select_set(False)
	obj.select_set(True)
	bpy.context.view_layer.objects.active = obj


def mark_seams(mesh, charts):
	"""A seam wherever two triangles of different charts meet."""
	bm = bmesh.new()
	bm.from_mesh(mesh)
	for edge in bm.edges:
		faces = edge.link_faces
		edge.seam = len(faces) == 2 and charts[faces[0].index] != charts[faces[1].index]
	bm.to_mesh(mesh)
	bm.free()


def unwrap(obj, request, report):
	mesh = obj.data
	layer = mesh.uv_layers.new(name=UV_NAME)
	layer.data.foreach_set('uv', uv_array(mesh, GENERATOR_UV_NAME).ravel())
	mesh.uv_layers.active = layer
	layer.active_render = True
	select_only(obj)
	bpy.ops.object.mode_set(mode='EDIT')
	try:
		bpy.ops.mesh.select_mode(type='FACE')
		bpy.ops.mesh.select_all(action='SELECT')
		bpy.context.scene.tool_settings.use_uv_select_sync = True
		if request['Unwrap']:
			# Minimum stretch keeps the snapped slivers of the arrangement from
			# folding over, where angle based and conformal unwraps fold some.
			bpy.ops.uv.unwrap(method='MINIMUM_STRETCH', margin=0.0)
			bpy.ops.object.mode_set(mode='OBJECT')
			if folded(uv_array(mesh, UV_NAME)):
				report['Warnings'].append("Unwrapping folded some triangles over; kept the generator's layout instead.")
				layer.data.foreach_set('uv', uv_array(mesh, GENERATOR_UV_NAME).ravel())
			bpy.ops.object.mode_set(mode='EDIT')
			bpy.ops.mesh.select_all(action='SELECT')
			bpy.ops.uv.average_islands_scale()
		report['Packer'] = pack(request, report)
	finally:
		bpy.ops.object.mode_set(mode='OBJECT')


def folded(uvs):
	"""Whether any triangle is laid out the wrong way round, which a bake would turn inside out."""
	a, b, c = uvs[0::3], uvs[1::3], uvs[2::3]
	ab, ac = b - a, c - a
	return bool(np.any(ab[:, 0] * ac[:, 1] - ab[:, 1] * ac[:, 0] <= 0))


def pack(request, report):
	size = request['TextureSize']
	gap = 2 * request['Padding']
	if request['PreferUvPackMaster'] and report.get('UvPackMasterModule'):
		props = bpy.context.scene.uvpm4_props.default_main_props
		props.pixel_margin_enable = True
		props.pixel_margin = gap
		props.pixel_margin_tex_size = size
		props.pixel_border_margin_enable = True
		props.pixel_border_margin = request['Padding']
		props.rotation_enable = True
		props.rotation_step = 90
		props.flipping_enable = False
		props.normalize_scale = False
		props.heuristic_enable = False
		prefs = bpy.context.preferences.addons[report['UvPackMasterModule']].preferences
		result = bpy.ops.uvpackmaster4.pack('EXEC_DEFAULT', mode_id='pack.single_tile', pack_op_type='0')
		if 'FINISHED' in result and prefs.engine_retcode == 0:
			return 'UVPackMaster'
		report['Warnings'].append(f'UVPackMaster failed ({result}, engine code {prefs.engine_retcode}); packed with Blender instead.')
	bpy.ops.uv.pack_islands(rotate=True, margin_method='FRACTION', margin=gap / size, shape_method='CONCAVE')
	return 'Blender'


def uv_array(mesh, name):
	uvs = np.empty(len(mesh.loops) * 2, dtype=np.float32)
	mesh.uv_layers[name].data.foreach_get('uv', uvs)
	return uvs.reshape(-1, 2)


def build_flat(regular, request):
	"""The flat faces alone, laid at the flat height, facing straight up, with the regular's final UVs."""
	flat_faces = request['FlatFaces']
	triangles = np.asarray(request['Triangles'], dtype=np.int64).reshape(-1, 3)[flat_faces]
	used, inverse = np.unique(triangles, return_inverse=True)
	positions = np.asarray(request['Positions'], dtype=np.float64).reshape(-1, 3)[used]
	positions[:, 1] = request['FlatHeight']
	corners = (np.asarray(flat_faces, dtype=np.int64)[:, None] * 3 + np.arange(3)).ravel()
	uvs = uv_array(regular.data, UV_NAME)[corners]
	normals = np.tile([0.0, 1.0, 0.0], (len(corners), 1))
	flat = build_mesh('Flat', positions.tolist(), inverse.reshape(-1, 3).tolist(), normals, uvs)
	flat.data.uv_layers[GENERATOR_UV_NAME].name = UV_NAME
	return flat


def build_high(regular, request):
	"""The regular mesh with every crease rounded off by the bevel width."""
	high = regular.copy()
	high.data = regular.data.copy()
	high.name = high.data.name = 'High'
	bpy.context.scene.collection.objects.link(high)
	if request['EdgeBevel'] <= 0 or not request['Creases']:
		return high
	mesh = high.data
	index = {tuple(sorted(edge.vertices)): edge.index for edge in mesh.edges}
	weights = np.zeros(len(mesh.edges), dtype=np.float32)
	creases = np.asarray(request['Creases'], dtype=np.int64).reshape(-1, 2)
	for a, b in creases:
		weights[index[(min(a, b), max(a, b))]] = 1.0
	attribute = mesh.attributes.get('bevel_weight_edge') or mesh.attributes.new('bevel_weight_edge', 'FLOAT', 'EDGE')
	attribute.data.foreach_set('value', weights)
	bevel = high.modifiers.new('Bevel', 'BEVEL')
	bevel.width = request['EdgeBevel']
	bevel.segments = max(1, request['EdgeBevelSegments'])
	bevel.limit_method = 'WEIGHT'
	bevel.use_clamp_overlap = True
	bevel.harden_normals = False
	return high


def save_png(image, path, bits):
	scene = bpy.context.scene
	scene.view_settings.view_transform = 'Raw'
	scene.view_settings.look = 'None'
	scene.view_settings.exposure = 0
	scene.view_settings.gamma = 1
	scene.render.dither_intensity = 0
	settings = scene.render.image_settings
	settings.file_format = 'PNG'
	settings.color_mode = 'RGB'
	settings.color_depth = str(bits)
	settings.color_management = 'FOLLOW_SCENE'
	image.save_render(str(path), scene=scene)


def tangents(mesh):
	mesh.calc_tangents(uvmap=UV_NAME)
	count = len(mesh.loops)
	tangent = np.empty(count * 3, dtype=np.float32)
	sign = np.empty(count, dtype=np.float32)
	mesh.loops.foreach_get('tangent', tangent)
	mesh.loops.foreach_get('bitangent_sign', sign)
	mesh.free_tangents()
	return np.hstack([tangent.reshape(-1, 3), sign[:, None]])


def uv_fill(uvs):
	a, b, c = uvs[0::3], uvs[1::3], uvs[2::3]
	ab, ac = b - a, c - a
	return float(np.abs(ab[:, 0] * ac[:, 1] - ab[:, 1] * ac[:, 0]).sum() / 2)


def run(request, folder, report):
	if request.get('Version') != VERSION:
		raise BakeFailure(f'The request is version {request.get("Version")}, this script reads version {VERSION}.')
	bpy.context.preferences.use_preferences_save = False
	clear_scene()

	baker_name = enable(BAKER)
	if baker_name is None:
		raise BakeFailure('The Starburst Normal Baker extension is not installed in this Blender.')
	baker_package = importlib.import_module(baker_name)
	baker = importlib.import_module(baker_name + '.baker')

	if request['PreferUvPackMaster'] and find_module(UVPM):
		try:
			# UVPackMaster builds shaders as it loads, which background mode
			# only allows once the GPU module is up.
			if not addon_utils.check(find_module(UVPM))[1]:
				import gpu
				gpu.init()
			report['UvPackMasterModule'] = enable(UVPM)
		except Exception as exc:
			report['Warnings'].append(f'UVPackMaster could not be loaded: {exc}')

	started = time.perf_counter()
	positions = np.asarray(request['Positions'], dtype=np.float64).reshape(-1, 3)
	triangles = np.asarray(request['Triangles'], dtype=np.int64).reshape(-1, 3)
	normals = np.asarray(request['Normals'], dtype=np.float64).reshape(-1, 3)
	uvs = np.asarray(request['Uvs'], dtype=np.float32).reshape(-1, 2)
	regular = build_mesh('Regular', positions.tolist(), triangles.tolist(), normals, uvs)
	mark_seams(regular.data, request['Charts'])
	unwrap(regular, request, report)
	final_uvs = uv_array(regular.data, UV_NAME)
	report['UvFill'] = uv_fill(final_uvs)
	report['Timings']['uvs'] = time.perf_counter() - started

	flat = build_flat(regular, request)
	high = build_high(regular, request)

	started = time.perf_counter()
	job = bpy.context.scene.starburst_baker.jobs.add()
	job.name = 'Insert'
	job.high = high
	job.flat = flat
	job.shaped = regular
	job.uv_name = UV_NAME
	job.flat_uv_name = UV_NAME
	job.width = job.height = request['TextureSize']
	job.margin = request['Padding']
	job.extrusion = request['RegularExtrusion']
	job.ray_distance = 0.0
	job.separate_flat_rays = True
	job.flat_extrusion = request['FlatExtrusion']
	job.flat_ray_distance = 0.0
	job.normal_y = 'POS_Y'
	job.device = 'GPU' if request['Gpu'] else 'CPU'
	job.output_dir = str(folder)
	job.output_name = 'normal-16'
	job.overwrite = True
	bpy.context.view_layer.update()
	baker_package.mark_faces(job, set(request['FlatFaces']))
	path = baker.bake_job(bpy.context, job, lambda message: print('[bake_insert]', message, flush=True))
	save_png(job.result_image, folder / 'normal.png', 8)
	Path(path).unlink(missing_ok=True)
	report['Timings']['bake'] = time.perf_counter() - started

	return {
		'Uvs': final_uvs.ravel().tolist(),
		'Tangents': tangents(regular.data).ravel().tolist(),
	}


def probe():
	"""What this Blender can do, for Unity to show before it bakes anything."""
	print('[bake_insert] ' + json.dumps({
		'Blender': bpy.app.version_string,
		'Version': VERSION,
		'Baker': find_module(BAKER) is not None,
		'UvPackMaster': find_module(UVPM) is not None,
	}), flush=True)


def main():
	argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
	if argv == ['--probe']:
		probe()
		return
	if len(argv) != 1:
		print('usage: blender -b --python bake_insert.py -- <request.json> | --probe', file=sys.stderr)
		sys.exit(2)
	request_path = Path(argv[0]).resolve()
	folder = request_path.parent
	report = {'Blender': bpy.app.version_string, 'Warnings': [], 'Timings': {}}
	result = {}
	code = 0
	try:
		request = json.loads(request_path.read_text(encoding='utf-8'))
		result = run(request, folder, report)
	except Exception as exc:
		traceback.print_exc()
		report['Error'] = str(exc) or type(exc).__name__
		code = 1
	result['Report'] = report
	(folder / 'result.json').write_text(json.dumps(result), encoding='utf-8')
	print('[bake_insert]', json.dumps(report), flush=True)
	sys.exit(code)


if __name__ == '__main__':
	main()
