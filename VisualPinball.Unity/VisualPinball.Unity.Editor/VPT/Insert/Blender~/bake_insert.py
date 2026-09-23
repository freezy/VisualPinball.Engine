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
`-- --probe` instead, it only prints what this Blender can do.

The mesh arrives in Unity's local space and stays in it. Unity and Blender
agree numerically on everything that matters here: a triangle faces where
cross(b - a, c - a) points, the bitangent is sign * cross(normal, tangent),
and V runs up the image. The insert just lies in Blender's XZ plane, which
nothing below cares about.

The normal map is baked from a copy of the mesh whose every sharp edge a Bevel
shader rounds off. The raised middle with its starburst is baked from a flat
copy of itself and everything else from the mesh as it is, so the starburst
keeps a flat tangent basis. UVPackMaster packs the UVs when Blender has it
installed; nothing else needs installing.
"""

import json
import sys
import time
import traceback
from pathlib import Path

import addon_utils
import bmesh
import bpy
import numpy as np

UVPM = 'uvpackmaster4'
UV_NAME = 'UVMap'
GENERATOR_UV_NAME = 'Generator'

# Must match InsertBakeRequest.CurrentVersion.
VERSION = 3

# How big the walls are laid out against the floors. They are seen side on, if
# at all, and give their texels to the floors.
WALL_SCALE = 0.5

# Rays the Bevel shader traces per sample, and samples per texel. Fewer of either
# leave the rounded edges grainy.
BEVEL_RAYS = 16
BEVEL_SAMPLES = 16


class BakeFailure(Exception):
	pass


def find_module(name):
	"""The full module name of an installed extension or legacy add-on, or None."""
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


def select_only(*objects):
	for other in bpy.context.scene.objects:
		other.select_set(False)
	for obj in objects:
		obj.select_set(True)
	bpy.context.view_layer.objects.active = objects[0]


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
			# Minimum stretch pulls the starburst's long thin fans out of shape,
			# where the angle based unwrap lays them out as they are.
			bpy.ops.uv.unwrap(method='ANGLE_BASED', margin=0.0)
			bpy.ops.object.mode_set(mode='OBJECT')
			if folded(uv_array(mesh, UV_NAME)):
				report['Warnings'].append("Unwrapping folded some triangles over; kept the generator's layout instead.")
				layer.data.foreach_set('uv', uv_array(mesh, GENERATOR_UV_NAME).ravel())
			bpy.ops.object.mode_set(mode='EDIT')
			bpy.ops.mesh.select_all(action='SELECT')
			bpy.ops.uv.average_islands_scale()
		bpy.ops.object.mode_set(mode='OBJECT')
		shrink_walls(mesh, request)
		bpy.ops.object.mode_set(mode='EDIT')
		bpy.ops.mesh.select_all(action='SELECT')
		report['Packer'] = pack(request, report)
	finally:
		bpy.ops.object.mode_set(mode='OBJECT')


def corners_of(faces):
	"""The corner indices of the given triangles."""
	return (np.asarray(faces, dtype=np.int64).reshape(-1, 1) * 3 + np.arange(3)).ravel()


def shrink_walls(mesh, request):
	"""Scales every wall's island about its middle, before packing spreads them out."""
	charts = np.asarray(request['Charts'])
	uvs = uv_array(mesh, UV_NAME)
	for chart in request['WallCharts']:
		corners = corners_of(np.nonzero(charts == chart)[0])
		if len(corners):
			middle = uvs[corners].mean(axis=0)
			uvs[corners] = middle + (uvs[corners] - middle) * WALL_SCALE
	mesh.uv_layers[UV_NAME].data.foreach_set('uv', uvs.ravel())


def folded(uvs):
	"""Whether any triangle is laid out the wrong way round, which a bake would turn inside out."""
	a, b, c = uvs[0::3], uvs[1::3], uvs[2::3]
	ab, ac = b - a, c - a
	return bool(np.any(ab[:, 0] * ac[:, 1] - ab[:, 1] * ac[:, 0] <= 0))


def overlapping(uvs, size):
	"""The first triangle whose UV interior shares a texel centre with another's, or None."""
	covered = np.zeros((size, size), dtype=bool)
	for face in range(len(uvs) // 3):
		a, b, c = uvs[face * 3:face * 3 + 3].astype(np.float64) * size
		low = np.maximum(0, np.floor(np.minimum(np.minimum(a, b), c)).astype(int))
		high = np.minimum(size, np.ceil(np.maximum(np.maximum(a, b), c)).astype(int))
		det = (b[1] - c[1]) * (a[0] - c[0]) + (c[0] - b[0]) * (a[1] - c[1])
		if np.any(high <= low) or abs(det) < 1e-12:
			continue
		x, y = np.meshgrid(np.arange(low[0], high[0]) + 0.5, np.arange(low[1], high[1]) + 0.5)
		u = ((b[1] - c[1]) * (x - c[0]) + (c[0] - b[0]) * (y - c[1])) / det
		v = ((c[1] - a[1]) * (x - c[0]) + (a[0] - c[0]) * (y - c[1])) / det
		inside = (u > 1e-7) & (v > 1e-7) & (1 - u - v > 1e-7)
		region = covered[low[1]:high[1], low[0]:high[0]]
		if np.any(region & inside):
			return face
		region |= inside
	return None


def pack(request, report):
	size = request['TextureSize']
	gap = 2 * request['Padding']
	if request['PreferUvPackMaster'] and report.get('UvPackMasterModule'):
		props = bpy.context.scene.uvpm4_props.default_main_props
		props.pixel_margin_enable = True
		# Its margin is kept only to within a pixel or so.
		props.pixel_margin = gap + 2
		props.pixel_margin_tex_size = size
		props.pixel_border_margin_enable = True
		props.pixel_border_margin = request['Padding']
		props.rotation_enable = True
		# Long thin charts nest far better turned by a few degrees than by quarter
		# turns only, and a tangent space map turns with them.
		props.rotation_step = 5
		props.flipping_enable = False
		props.normalize_scale = False
		props.heuristic_enable = False
		prefs = bpy.context.preferences.addons[report['UvPackMasterModule']].preferences
		result = bpy.ops.uvpackmaster4.pack('EXEC_DEFAULT', mode_id='pack.single_tile', pack_op_type='0')
		if 'FINISHED' in result and prefs.engine_retcode == 0:
			return 'UVPackMaster'
		report['Warnings'].append(f'UVPackMaster failed ({result}, engine code {prefs.engine_retcode}); packed with Blender instead.')
	# Blender keeps the margin clear round every island, so two of them end up
	# twice the margin apart: half the gap is the gap.
	bpy.ops.uv.pack_islands(rotate=True, margin_method='FRACTION', margin=gap / 2 / size, shape_method='CONCAVE')
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
	corners = corners_of(flat_faces)
	uvs = uv_array(regular.data, UV_NAME)[corners]
	normals = np.tile([0.0, 1.0, 0.0], (len(corners), 1))
	flat = build_mesh('Flat', positions.tolist(), inverse.reshape(-1, 3).tolist(), normals, uvs)
	flat.data.uv_layers[GENERATOR_UV_NAME].name = UV_NAME
	return flat


def build_high(regular, request):
	"""The regular mesh, shaded with every sharp edge rounded off by the bevel width.

	A Bevel shader rather than a bevelled mesh: the arrangement snaps edges down
	to a fraction of a pixel, which a geometric bevel has to clamp to, all of it,
	or else fold over. The shader rounds each point by what lies around it.
	"""
	high = regular.copy()
	high.data = regular.data.copy()
	high.name = high.data.name = 'High'
	bpy.context.scene.collection.objects.link(high)
	material = bpy.data.materials.new('Rounded')
	material.use_nodes = True
	nodes = material.node_tree.nodes
	nodes.clear()
	output = nodes.new('ShaderNodeOutputMaterial')
	shader = nodes.new('ShaderNodeBsdfDiffuse')
	material.node_tree.links.new(shader.outputs[0], output.inputs['Surface'])
	if request['EdgeBevel'] > 0:
		bevel = nodes.new('ShaderNodeBevel')
		bevel.samples = BEVEL_RAYS
		bevel.inputs['Radius'].default_value = request['EdgeBevel']
		material.node_tree.links.new(bevel.outputs['Normal'], shader.inputs['Normal'])
	high.data.materials.clear()
	high.data.materials.append(material)
	return high


def new_image(name, size):
	image = bpy.data.images.new(name, width=size, height=size, alpha=True, float_buffer=True, is_data=True)
	image.colorspace_settings.name = 'Non-Color'
	return image


def image_pixels(image):
	pixels = np.empty(image.size[0] * image.size[1] * 4, dtype=np.float32)
	image.pixels.foreach_get(pixels)
	return pixels.reshape(image.size[1], image.size[0], 4)


def target(image, color=(1.0, 1.0, 1.0, 1.0)):
	"""An emissive material whose active image node is where a bake lands."""
	material = bpy.data.materials.new('Bake Target')
	material.use_nodes = True
	nodes = material.node_tree.nodes
	nodes.clear()
	output = nodes.new('ShaderNodeOutputMaterial')
	emission = nodes.new('ShaderNodeEmission')
	emission.inputs['Color'].default_value = color
	material.node_tree.links.new(emission.outputs[0], output.inputs['Surface'])
	node = nodes.new('ShaderNodeTexImage')
	node.image = image
	node.select = True
	nodes.active = node
	return material


def bake(obj, request, high=None, extrusion=0.0):
	"""Bakes the high-poly's tangent-space normals onto obj or, without one, obj's own emission."""
	select_only(obj, high) if high else select_only(obj)
	bpy.context.scene.cycles.samples = BEVEL_SAMPLES if high and request['EdgeBevel'] > 0 else 1
	result = bpy.ops.object.bake(
		'EXEC_DEFAULT', type='NORMAL' if high else 'EMIT',
		target='IMAGE_TEXTURES', save_mode='INTERNAL', use_clear=True,
		use_selected_to_active=high is not None, use_cage=False,
		cage_extrusion=extrusion, max_ray_distance=0.0,
		margin=request['Padding'], margin_type='EXTEND', uv_layer=UV_NAME,
		normal_space='TANGENT', normal_r='POS_X', normal_g='POS_Y', normal_b='POS_Z',
	)
	if result != {'FINISHED'}:
		raise BakeFailure('Blender cancelled the bake.')


def bake_normal_map(regular, flat, high, request, path):
	"""Bakes the flat region from its flat copy and everything else from the mesh, and merges the two."""
	scene = bpy.context.scene
	scene.render.engine = 'CYCLES'
	scene.cycles.device = 'GPU' if request['Gpu'] else 'CPU'
	scene.cycles.use_denoising = False
	size = request['TextureSize']
	regular_image = new_image('Regular', size)

	# The regular mesh's rays start just clear of its own surface, so they
	# cannot reach across to a neighbouring one; the flat copy's start above
	# the starburst's highest point.
	print('[bake_insert] Baking normals onto the regular mesh', flush=True)
	regular.data.materials.append(target(regular_image))
	bake(regular, request, high, request['RegularExtrusion'])
	pixels = image_pixels(regular_image)

	if flat is not None:
		print('[bake_insert] Baking normals onto the flat mesh', flush=True)
		flat_image = new_image('Flat', size)
		flat.data.materials.append(target(flat_image))
		bake(flat, request, high, request['FlatExtrusion'])
		merge_flat(regular, flat_image, pixels, request)

	print('[bake_insert] Saving the normal map', flush=True)
	pixels[:, :, 3] = 1.0
	result = new_image('Normal', size)
	result.pixels.foreach_set(pixels.ravel())
	result.update()
	save_png(result, path, 8)


def merge_flat(regular, flat_image, pixels, request):
	"""Takes the flat bake's texels wherever the flat region is, padding included."""
	mask_image = new_image('Mask', request['TextureSize'])

	# Red on the flat region, green elsewhere, padded like the bakes, so the
	# padding round the flat region is taken from the flat bake as well.
	print('[bake_insert] Baking the flat region mask', flush=True)
	regular.data.materials.clear()
	regular.data.materials.append(target(mask_image, (0.0, 1.0, 0.0, 1.0)))
	regular.data.materials.append(target(mask_image, (1.0, 0.0, 0.0, 1.0)))
	indices = np.zeros(len(regular.data.polygons), dtype=np.int32)
	indices[request['FlatFaces']] = 1
	regular.data.polygons.foreach_set('material_index', indices)
	bake(regular, request)

	mask = image_pixels(mask_image)
	flat_region = mask[:, :, 0] > mask[:, :, 1]
	# Encoded normals are copied, never blended, which would shorten them.
	pixels[flat_region] = image_pixels(flat_image)[flat_region]


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
	overlap = overlapping(final_uvs, request['TextureSize'])
	if overlap is not None:
		raise BakeFailure(f'The packed UVs overlap at triangle {overlap}, which would bake two surfaces into one texel.')
	report['UvFill'] = uv_fill(final_uvs)
	report['Timings']['uvs'] = time.perf_counter() - started

	started = time.perf_counter()
	flat = build_flat(regular, request) if request['FlatFaces'] else None
	high = build_high(regular, request)
	bpy.context.view_layer.update()
	bake_normal_map(regular, flat, high, request, folder / 'normal.png')
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
		'Supported': bpy.app.version >= (5, 0, 0),
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
