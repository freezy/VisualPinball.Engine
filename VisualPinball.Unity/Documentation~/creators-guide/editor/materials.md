---
uid: materials_index
title: Materials
description: How VPE deals with materials.
---

![Unity Materials](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@13.0/manual/images/HDRPFeatures-LitShader.png)

# Materials

Materials are what you apply to an object in order to make it look or behave like something in the real world. Materials are one of the key components of a table, because they define the visuals and the physical behavior. However, the term *material* can be confusing, because it can have different meanings. So let's define them first.

## Rendered Materials

We refer to rendered materials just as **materials**. They describe how a surface of a mesh is drawn on the sceen. In Unity, [materials](https://docs.unity3d.com/Manual/materials-introduction.html) and [shaders](https://docs.unity3d.com/Manual/Shaders.html) are closely linked - every material uses a shader, which you can configure. The most common shader is the [Lit shader](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@12.0/manual/Lit-Shader.html), which works well for rigid materials that interact with light. 

A material typically includes one or more textures that define the color, normals, roughness, metalness and many more parameters on a per-pixel basis. 

> [!note]
> In Visual Pinball, materials don't include the texture. Instead, the texture is applied on a per-object basis.

## Physics Materials

We refer to how the material interacts with the ball during the physics simulation as the **physics material**. It has these properties:

- **Elasticity** - The bounciness, how much the ball is thrown back when it collides.
- **Elasticity Falloff** - Pinball tables have a lot of rubber parts, and rubber has a special attribute: it gets less bouncy when hit at higher velocity. The falloff parameter controls how much.
- **Friction** - The Coulomb coefficient that controls sliding, grip, and tangential collision and contact impulses.
- **Rolling Resistance** - A dimensionless coefficient (`Crr`) that removes energy from sustained near-no-slip rolling without changing impact response.
- **Scatter** - Adds a random factor to the collision angle.

Physics materials are a way to group common behavior among certain objects, but contrarily to rendered materials, you can also *not* assign a physics material to an object and set each parameter individually.

Friction and rolling resistance are separate controls. Increasing friction gives the ball more grip and changes oblique contact behavior. Increasing rolling resistance slows a ball that is already rolling without slip. Rolling resistance is applied only to sustained generic surface contacts; it does not affect impacts, ball-to-ball contacts, or the specialized flipper contact solver.

> [!IMPORTANT]
> Rolling resistance defaults to zero. VPE does not recommend a nonzero preset until measurements on a physical playfield have been completed. See [Calibrating Rolling Resistance](xref:rolling-resistance-calibration) before choosing a value.

> [!note]
> In Visual Pinball, the physical parameters are part of the rendered material, so there is only one notion of material.

## Conversion from Visual Pinball

As mentioned above, there are two differences between Visual Pinball and VPE how materials are handled:

1. VPE includes textures in the material, while Visual Pinball does not.
2. VPE differentiates between rendered and the physical material.

When importing a `.vpx` file, VPE converts the "visual part" of Visual Pinball materials into materials for the current render pipeline. It does that by creating a new material for every material/texture combination in Visual Pinball. The materials are then written to the `Materials` asset folder of the imported table where they can be easily edited and referenced. Since Visual Pinball uses different shaders than Unity, the results of the conversion are approximations and should be heavily tweaked. 

Since VPE uses the same physics engine as Visual Pinball, the physical values that exist in Visual Pinball are copied 1:1 into a new physics material and saved in the asset folder. VPX does not contain a rolling-resistance value, so imported physics materials start with `Rolling Resistance = 0` unless the table author opts in afterward.
