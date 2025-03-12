---
uid: asset_library_guide
title: Asset Library Style Guide
description: These guidelines describe how the game assets of the pinball asset library should be created so they are of high quality, customizable, consistent and optimized.
---

# Asset Library Style Guide

This document serves as a comprehensive style guide for all 3D assets included in the asset library. The goal is that all assets are of **high quality**, visually **consistent**, **optimized** for performance, and **customizable**. Following these guidelines is essential in providing assets that can be used in various types of tables. That's not to say that there can't be any exceptions, but the vast majority, this should be applicable.

## Design Language

We're aiming for a photorealistic look, as opposed to stylized visuals. Shapes should be the same as in the real world and also have the same size. 

## Geometry Guidelines

This section is about modeling, i.e. how the mesh, which consists of vertices, edges and faces, should be created.

### Topology

The topology is how you arrange your vertices to form the shapes of your model. There are countless opinions and practices around good topology. Since our models serve as game assets and not for rendering out close-up product shots, we're allowed to be less strict. Here are some guidelines when modelling:

- Use quads where possible and avoid n-gons.
- Maintain clean topology with proper edge flow, if possible.
- Apply proper smoothing groups/hard edges for accurate normal calculation (shade smooth/flat in Blender).
- Avoid non-manifold geometry and floating vertices (make the mesh watertight).

If you're converting CAD models which aren't polygon-based, you'll probably have to apply some [retopology](https://en.wikipedia.org/wiki/Retopology). The same goes for 3D scans.

{shot from screw automatically converted by STEP versus retopoed)

<i>STEP model of a screw imported in Blender (left), versus the re-modeled version</i>

### Poly Count

In [polygonal modeling](https://en.wikipedia.org/wiki/Polygonal_modeling) (which is what we're doing here), the poly count is the number of polygons used in a model, and given that in game engines, quads and n-gons are converted to triangles, it's the number of triangles. The more triangles a model has, the more detailed it is, but the slower it is to render.

- Spinners, drop targets, and other small objects can usually remain under 500 triangles.
- Aim for low-to-mid poly models for standard playfield assets such as flippers and bumpers (e.g., 500–2,000 triangles).
- Hero pieces (large ramps, toys) can go higher but be mindful not to exceed necessary detail.

{pics of a model with multiple poly counts}

### Scale and Orientation

Unity uses a [left-handed](https://en.wikipedia.org/wiki/Right-hand_rule) coordinate system, where X points to the right, Y up, and Z forward. This is also how your model should be oriented.

For the scale, use meters. It's important to model in real world units, so that the relations between assets are correct, as otherwise it's very difficult to correctly scale anything. Also make sure that scaling is applied to the model, i.e. the actual geometry is at the correct scale and doesn't need to be rescaled by the game engine.

### Pivot Point

The pivot point, also known as *object origin* or *local origin*, defines where your model actually appears for a given position in 3D space.

- Static objects should always have their vertical axis (the Y axis in Unity, or Z axis in VPX) of the pivot point at playfield height, so setting it to 0 will position the object on the playfield.
- Objects that rotate obviously need their pivot point on the rotation axis. However, if such an object is parented to another (static) object, the parent should also have its vertical origin at playfield height.
- On the horizontal plane, the pivot point should be in the center if no other more obvious position is given by the object's topology.

{shots with different correct and wrong origins}

### UV Maps

All models must be [UV-mapped](https://en.wikipedia.org/wiki/UV_mapping).

- UVs should be unwrapped with minimal stretching.
- Maintain 2-4 pixel padding between UV islands.
- Keep UV shells proportional to their 3D size.
- Organize UVs in 0-1 UV space

### Decals

If your model contains art that varies from instance to instance, use a [decal mesh](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@17.2/manual/understand-decals.html). Decals should be used where you would find literal decals or imprints in the real world. Examples include spinners, aprons, targets and bumpers.

{example of one or more decal meshes}

The decal geometry should be in a separate object parented to the main object. The UVs of the decal mesh should be laid out in a way so its textures can be created with non-specialized image editors. This usually means front projection without any stretching, and centered.

{shots with good vs bad decal uvs}

> [!note]
> #### Why Decals?
> Decals are great because they make your workflow more flexible and are at the same time more performant:
> - Flexible, because it allows us to texture our models in a generic way so that they can be used by anybody. Imagine a drop target with a star on it. Without decals, the star would be baked into the texture, and if anybody else would want reuse that target, they would need to recreate the texture with another subject. With decals, they only need to swap out the decal texture.
> - Performance, because Unity is optimized for having thousands of decals in a scene, allowing us to use higher-resolution textures for our decals without having to waste resources on the rest of the object.

### Colliders

VPE uses separate meshes for collision for some items (currently drop targets and hit targets). These meshes are to be included in the model as well.

- Their pivot point must align with the pivot point of the main mesh.
- The scale must be applied and correspond to the main mesh's scale.
- They shouldn't include any UVs.

{pic of a target with its collider mesh}

### LODs

In terms of [LODs](https://en.wikipedia.org/wiki/Level_of_detail_(computer_graphics)), we're only using one LOD, since the playfield size is compact enough so that most elements would be rendered at the same LOD anyway. Also, most assets will be under 1,000 triangles and thus the performance impact of LODs would be minimal.

## Material Guidelines

As mentioned, we're aiming for realistic visuals, which can be achieved with physically based rendering ([PBR](https://en.wikipedia.org/wiki/Physically_based_rendering)). In HDRP, this means using the [Lit Shader](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@17.2/manual/lit-material.html).

> [!note]
> We're not sure yet whether authors will be able to choose other shaders or even create their own, or whether usage will be restricted. We'll update this section as soon as we know more. For now, we focus on the how to author using the Lit Shader.

### Texture Maps

In the PBR workflow there are five maps that are most relevant:

- Color map, also called diffuse or albedo map
- Normal map, often called bump map
- Metallic map
- Smoothness map (which is an inverted roughness map)
- Ambient occlusion map

Unity also supports emissive maps, detail maps, and some others, depending on the material type, but we'll focus on those mentioned above.

### Color Maps

The color of a material are RGB values that contain no lighting information. 

If your asset or parts of your asset exist in multiple color variations, you should consider using only gray tones and tint the material with the Lit shader's base color. This will make it customizable without having to render out the texture for each color variant (and also more efficient, memory-wise).

{shots of an object with gray and tinted variations}

As mentioned above, don't bake art that varies into the texture, but use decals instead. Single-color decals that come in multiple color variations should also use a gray-tone color map and tinted through the Lit Shader directly, so they can be more easily customized and don't need multiple material instances.

### Normal Maps

The goal of [Normal Maps](https://en.wikipedia.org/wiki/Normal_mapping) is to fake lighting of bumps and dents. In general, the rule is: If a detail doesn't have any silhouette-defining features and isn't deep enough to cast visible shadows, flatten it and bake it as a normal map.

{shot of an item detailed as geometry, as low poly, and as low poly with normal maps}

One element that is particularly important for getting realistic visuals are the edges. In the real world, light always gets reflected off edges, because they are never perfect. To simulate this, **you should always bevel your edges**. If your model contains very few prominent edges, you can do that in geometry. However, more often than not, baking the bevel into a normal map is the more efficient approach.

{shot of an item with no bevel, baked bevel and geo bevel}

To summarize, you should use normal maps for:

- Surface details (scratches, small dents, panel seams)
- Shallow details (<5mm in real scale)
- Text or logo embossing
- Pattern detailing

### Metallic / Smoothness Maps

With a metallic map, you can define on pixel level whether your material is metallic, or not. 
- Only use this if your material covers both metallic and non-metallic parts of your model. Otherwise, use metallicness property of the Lit Shader directly.  
- You should only use 0 or 1 as values, because materials that are partly metallic and partly don't exist in the real world.

{shot of an object with and without metallic values}

The smoothness map, which is the inverse of the roughness map, defines how regular light is reflected on a micro-surface level. A level of 1 behaves like a mirror, while a level of 0 is more like an eraser.

{shot of a sphere with 10 different smoothness values}

### Wear

In general, there should be visible wear on all items, i.e. more as if the table just shipped brand new from the factory.

{an asset with no wear, wear, and too much wear}

### Texture Map Resolution

All texture maps must use the power of two for width and height. They don't have to be square, though. 

We're aiming for a resolution of about 6px/mm (around 150 dpi). For a playfield texture that means roughly a resolution at 4096×8192 pixels. If you can, go as high as that, but don't upscale images. The highest resolution should be the one from your source. This applies to both color and normal maps. For metallic/smoothness you can go half the size.

You can determine the resolution by looking at your UV map and the size of the asset. 

{images with explications}

## Attribution
