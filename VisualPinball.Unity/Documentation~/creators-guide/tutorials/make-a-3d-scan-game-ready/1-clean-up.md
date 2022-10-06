---
uid: tutorial_3d_scan_1
title: Clean Up Your 3D Scan
description: Get rid of unwanted geometry, and use a reasonable poly count.
---

# Clean Up

Open Blender and import your model. 

## Reposition

The first thing you'll see is that it's probably not correctly sized and positioned. Using the `R` (rotate) and `G` (grab) keys, place your object at the origin.

![Proper position](positioned.jpg)

Be sure to apply everything by hitting `Ctrl+A` in the scene view and choose *All Transforms*.

## Remove Unwanted Geometry

![Unwanted Geometry](geometry-to-clean.jpg)

As you can see, our scan has a bunch of artifacts that aren't actually part of the owl. We're going to focus on the bottom part.

1. Zoom in on the part you want to remove.
2. Toggle *X-Ray*, so you can select occluded geometry, and hit `TAB` to enter Edit Mode.
3. In Edit Mode, hit `1` and select the vertices you want to remove.
4. Hit `X` and select *Delete Vertices*
5. Press `2` for edge selection, and select the boundary of the hole you've just created. Hit `F` to create a face and `Ctrl+T` to triangulate it.
6. From Edit Mode, change into *Sculpt Mode*, select the *Smooth* tool, adjust the radius, and smoothen up the hole you've filled.

![Steps 1-7](cleanup-steps.jpg)
<small><i>Steps 1 to 6.</i></small>

Repeat this for all the other parts that are unwanted in your scan.

> [!note]
> If you have free floating geometry that you want to quickly erase, do this:
> 1. In Edit Mode, select a vertex of the part you want to keep.
> 2. Hit `Ctrl+L` to select all linked vertices
> 3. Hit `Ctrl+I` to invert the selection.
> 4. Hit `X` and select *Delete Vertices* to delete all loose parts.

## Decimate the Mesh

This step is optional, but 1.8mio triangles is hard to handle, so we'll decimate the mesh to 10% so we have something more light to work with. 180k triangles is still plenty enough, even later when we use it to bake in the details.

1. Go back to object mode by hitting `TAB`.
2. Go to the *Modifier* panel on the right hand side.
3. Add a *Decimate* modifier.
4. Set the *Ratio* to 0.1 (Blender will probably freeze for a few seconds).
5. From the drop down, select *Apply*.

Finally, export the mesh as *Wavefront (.obj)* and name it `hi-poly.obj`.

Now that we have a good hi-poly mesh, let's continue with the next step, [retopology](xref:tutorial_3d_scan_2).
