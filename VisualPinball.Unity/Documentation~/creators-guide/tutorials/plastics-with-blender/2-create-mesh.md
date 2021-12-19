---
uid: plastics_with_blender_2
title: Realistic Looking Plastics - Create Mesh
description: How to create the meshes in Blender
---

# Create Mesh

We're now going to import the SVG created in the [previous step](xref:plastics_with_blender_1) and create a mesh that is beveled on the top.

## Import

Open Blender, clear the scene with `A`, `X`, `Enter`. Then, click on *File -> Import -> SVG*, navigate to where you've saved the SVG in the previous step, select `Plastics.svg`, and hit *Import*.

You probably won't see much due to the imported size. If there were no errors, you should see your imported plastics in the Outliner. Select them and press numpad `.` to zoom in. 

> [!note]
> You might run into another issue due to the size of the plastics: Camera clipping. To fix that, press `N` with your cursor over the 3D Viewport, and set something like 0.001m for the minimal distance.

Your viewport should look like this now:

![2D Outlines](blender-shapes.png)

## Extrude

Before we start extruding, let's make it one single object so we can easily apply everyting we do in one step. Hit `A` for select all, then `Ctrl`+`J` for joining all curves.

Then, select the curve and click on the *Object Properties* tab in the *Properties* view on the right side. Under *Geometry*, there is an *Extrude* field, and a bit below a *Bevel* section.

Now, the *Extrude* value is difficult to judge. If you have access to the physical plastics, you can calculate the scale between the real world and the object in Blender by physically measuring the size of a plastic and dividing it by the measured value in Blender. Then, also measure the thickness of the real-world plastic and multiply it by that factor. Personally, I just eyeballed it and ended up with 0.001m for the *Extrude* value.

However, this when beveling, Blender actually adds the bevel depth to the object, so the size will grow. We recommend beveling about a fourth of the thickness, so if you measured the actual thickness, set the *Bevel Depth* under *Bevel* to about a fourth and multiply the *Extrude* value by 0.8.

Also be sure to set the *Resolution* of the bevel to 0. Your mesh should now look like this:

![Extruded Shapes](blender-extruded.png)

## Remove Lower Bevel

Next step is to convert the extruded curve to a mesh. Select the mesh, then *Object -> Convert -> Mesh*. Hit `Tab` to switch to *Edit Mode* and check that there were no issues when creating the geometry.

We'll now remove the lower bevel by subtracting a cube from our mesh. Hit `Tab` to get back to *Object Mode*, then *Add -> Mesh -> Cube*. Resize the cube so it covers all plastics and move so it just covers the lower edges:

![Cube to remove lower bevel](blender-removing-cube.png)

Select the plastics object, go to the *Modifiers* tab, *Add Modifier -> Boolean*. Make sure *Difference* is selected, then either pick or select the cube under *Object*. Then hit `Ctrl`+`A` to apply it. Select the cube and hit `D`, `Enter` to delete it.

The geometry of your meshes should now look like that:

![No more lower bevel](blender-no-lower-bevel.png)

If that's the case, congrats, you're done with the meshes!