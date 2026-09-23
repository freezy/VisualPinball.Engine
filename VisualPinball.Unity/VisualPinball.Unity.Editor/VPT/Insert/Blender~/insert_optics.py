# Visual Pinball Engine
# Copyright (C) 2026 freezy and VPE Team
# SPDX-License-Identifier: GPL-3.0-or-later

"""Bake data for the hybrid insert, in the normal bake's final UV atlas.

The generated tray is not a closed optical solid. A white cavity lit from above
is a deliberately cheap proxy for light scattered inside it, not a transmission
or caustics simulation. RGB response stores this soft irradiance, bevel edges,
and an upward-facing transmission mask. The runtime adds view-dependent light
using the separately baked, unamplified object-space optical normal.
"""

import bpy
import numpy as np
from mathutils import Vector


def projected(api, regular, flat, high, request, name, bake_type='EMIT', samples=16):
	"""Match the normal bake's projections and its padded flat-region merge."""
	image = api.new_image(name, request['TextureSize'])
	regular.data.materials.clear()
	regular.data.materials.append(api.target(image))
	for face in regular.data.polygons:
		face.material_index = 0
	api.bake(regular, request, high, request['RegularExtrusion'], bake_type, samples)
	pixels = api.image_pixels(image)
	if flat is not None:
		flat_image = api.new_image(name + ' Flat', request['TextureSize'])
		flat.data.materials.clear()
		flat.data.materials.append(api.target(flat_image))
		api.bake(flat, request, high, request['FlatExtrusion'], bake_type, samples)
		api.merge_flat(regular, flat_image, pixels, request)
	return pixels


def bake_optics(api, regular, flat, high, request, folder):
	positions = np.asarray(request['Positions']).reshape(-1, 3)
	low, upper = positions.min(axis=0), positions.max(axis=0)
	center = (low + upper) * 0.5
	radius = max(float(upper[0] - low[0]) * 0.4, 0.001)
	depth = max(radius * 1.5, 0.005)
	# A diffuse cavity proxy: the emitter is above the open tray. Its runtime
	# virtual counterpart sits below it, where the real insert's bulb would be.
	lamp_data = bpy.data.lights.new('Internal scatter proxy', 'AREA')
	lamp_data.energy = 1.0
	lamp_data.shape = 'DISK'
	lamp_data.size = radius * 2
	lamp = bpy.data.objects.new(lamp_data.name, lamp_data)
	bpy.context.scene.collection.objects.link(lamp)
	lamp.location = (center[0], upper[1] + depth, center[2])
	lamp.rotation_euler = Vector((0, -1, 0)).to_track_quat('-Z', 'Y').to_euler()
	bpy.context.scene.world = bpy.data.worlds.new('Black optical bake world')
	bpy.context.scene.world.use_nodes = True
	bpy.context.scene.world.node_tree.nodes.get('Background').inputs['Strength'].default_value = 0
	bpy.context.scene.cycles.diffuse_bounces = 4
	bpy.context.scene.cycles.max_bounces = 6
	bpy.context.view_layer.update()
	print('[bake_insert] Baking internal scatter proxy', flush=True)
	glow = projected(api, regular, flat, high, request, 'Internal Glow', 'DIFFUSE', 64)
	# Exposure-independent coefficients. Runtime emission carries the lamp's
	# actual colour and brightness. Keep rare fireflies out of normalization.
	strength = glow[:, :, :3].mean(axis=2)
	positive = strength[strength > 0]
	white = max(float(np.percentile(positive, 99)) if len(positive) else 0, 1e-6)
	strength = np.clip(strength / white, 0, 1)
	# This term represents scattered light, so suppress Monte Carlo grain in
	# that low-frequency response. Two texels stay inside the padded islands.
	for axis in (0, 1):
		padding = [(0, 0), (0, 0)]
		padding[axis] = (2, 2)
		padded = np.pad(strength, padding, mode='edge')
		strength = sum(weight * np.take(padded, np.arange(strength.shape[axis]) + offset, axis=axis)
			for offset, weight in enumerate((1, 4, 6, 4, 1))) / 16

	material = high.data.materials[0]
	nodes, links = material.node_tree.nodes, material.node_tree.links
	geometry = nodes.new('ShaderNodeNewGeometry')
	bevel = next((n for n in nodes if n.type == 'BEVEL'), None)
	normal = bevel.outputs['Normal'] if bevel else geometry.outputs['Normal']
	emission = nodes.new('ShaderNodeEmission')
	output = next(n for n in nodes if n.type == 'OUTPUT_MATERIAL')
	links.new(emission.outputs[0], output.inputs['Surface'])
	encode = nodes.new('ShaderNodeVectorMath')
	encode.operation = 'MULTIPLY_ADD'
	# Keep optical faces sharp. Bevel normals interpolate through the crease
	# and turn it into a bright line; the surface normal map already supplies
	# the subtle rounded reflection separately.
	links.new(geometry.outputs['Normal'], encode.inputs[0])
	encode.inputs[1].default_value = (0.5, 0.5, 0.5)
	encode.inputs[2].default_value = (0.5, 0.5, 0.5)
	links.new(encode.outputs['Vector'], emission.inputs['Color'])
	print('[bake_insert] Baking optical normals', flush=True)
	optical = projected(api, regular, flat, high, request, 'Optical Normal')
	image = api.new_image('Optical Normal Result', request['TextureSize'])
	image.pixels.foreach_set(optical.ravel())
	image.update()
	api.save_png(image, folder / 'optical-normal.png', 16)

	# The angle introduced by the bevel marks ridges, hole rims and channel
	# edges without painting stripes onto a particular UV layout.
	dot = nodes.new('ShaderNodeVectorMath')
	dot.operation = 'DOT_PRODUCT'
	links.new(normal, dot.inputs[0])
	links.new(geometry.outputs['Normal'], dot.inputs[1])
	edge = nodes.new('ShaderNodeMapRange')
	edge.clamp = True
	edge.inputs['From Min'].default_value = 1.0
	edge.inputs['From Max'].default_value = 0.85
	links.new(dot.outputs['Value'], edge.inputs['Value'])
	separate = nodes.new('ShaderNodeSeparateXYZ')
	links.new(normal, separate.inputs[0])
	transmission = nodes.new('ShaderNodeMapRange')
	transmission.clamp = True
	transmission.inputs['From Min'].default_value = 0.0
	transmission.inputs['From Max'].default_value = 1.0
	transmission.inputs['To Min'].default_value = 0.15
	transmission.inputs['To Max'].default_value = 1.0
	links.new(separate.outputs['Y'], transmission.inputs['Value'])
	combine = nodes.new('ShaderNodeCombineXYZ')
	combine.inputs['X'].default_value = 1.0
	links.new(edge.outputs['Result'], combine.inputs['Y'])
	links.new(transmission.outputs['Result'], combine.inputs['Z'])
	links.new(combine.outputs[0], emission.inputs['Color'])
	print('[bake_insert] Baking optical response masks', flush=True)
	response = projected(api, regular, flat, high, request, 'Optical Response')
	response[:, :, 0] = strength
	image = api.new_image('Optical Response Result', request['TextureSize'])
	image.pixels.foreach_set(response.ravel())
	image.update()
	api.save_png(image, folder / 'optical-response.png', 16)
	return [float(center[0]), float(low[1] - depth), float(center[2]), radius]
