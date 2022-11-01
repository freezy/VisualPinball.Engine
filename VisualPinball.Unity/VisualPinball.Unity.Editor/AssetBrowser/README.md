# Asset Browser

When building pinball tables, be it in the real or digital world, parts are needed. And a surprisingly large percentage of parts used in pinball machines are standard parts.

VPE ships with a collection of parts that we call the [Asset Library](https://github.com/VisualPinball/VisualPinball.Unity.AssetLibrary). It contains a curated, high-quality set of ready-to use assets that you can use to build your game.

In order to facilitate access to those assets, we've added a rich set of meta data, letting you quickly browse by categories, tags, custom attributes, real-world accuracy, and we added additional info like web links and descriptions.

All this is done through the *Asset Browser*, which is accessible through the *Visual Pinball* menu under *Asset Browser* or directly through the Toolbox.

## The Browser Window

The asset browser is split into three main panels and the toolbar:

1. The *left panel* mainly serves for navigation. Here you can browse by category and tags.
2. The *asset panel* is where the assets are listed.
3. In the *details panel* you can view the meta data of a selected asset.
4. The *toolbar* just contains the search bar and a refresh button in case something didn't load (duh!).

### Categories and Tags

Every asset has one and only one associated *category*. Those categories should be your starting point when looking for assets. In case you have multiple libraries, the categories are grouped, i.e. you'll see assets from all libraries that have assets with a given category name when selecting a category.

Categories are flat, so there aren't multiple levels like sub-categories. However, we've found that *tagging* assets with their specific nature is a good solution to further categorize items. For example, we've tagged our screw assets with `Phillips Screw` and `Slotted`, depending on which type of driver they use. However, there are combo screw heads that allow for both, so those simply have both tags associated.

When selecting a category, all the tags used by the items in that category are displayed below, allowing you to quickly narrow down the search results of a given category.

### Attributes

When you click on an asset, you'll see an *Attributes* section in the details panel. These are key/value pairs that describe relevant properties of the asset. Clicking on the attribute value will automatically filter your assets. This allows you to quickly list all assets by author X, or all screws with a `6-32` thread, or all posts that are 1 1/16" tall, and so forth.

## Terminology

*[Assets](https://docs.unity3d.com/Manual/AssetWorkflow.html)* are items that you can also browse through Unity's [Project window](https://docs.unity3d.com/Manual/ProjectView.html). Our asset library currently contains [Prefabs](https://docs.unity3d.com/Manual/Prefabs.html), and we'll add support for other types of assets when necessary.

So, *Asset* is a standard Unity term, and we'll use it as such. What VPE adds on top of that is the notion of a *Library*. A Library is a collection of hand-picked assets. Assets of a Library must share the same root folder, but can be freely organized under that folder. VPE ships with one Library, but you can also create your own project-specific Library in order to make use of the advanced browsing features.

> [!note]
> Not everything below the Library root folder is automatically added to the Library. In fact, every asset needs to be added manually in order to show up in the browser.
> Why does there need to be a root folder then? Well, it's about making maintenance easier. As every asset must be linked to a category, and multiple libraries can have the same categories, we need a way to know to which library a new asset should be associated to. The most obvious way is to do that automatically based on the asset's file location.



The value of the asset browser is that it makes it easier to find assets, because it groups assets from multiple libraries into a single view. Since assets must be added explicitly to the library, you can quickly filter by assets that you can add to the scene, contrarily to the project window, where you'll see everything else like textures and materials.

The asset browser is split into three main panels and the toolbar:

1. In the *left panel* you can filter assets by library and category.
2. The *asset panel* is where the assets are listed.
3. In the *details panel* you can view and edit the meta data of a selected asset.

### Categories 

Each asset is assigned to a *category*. Categories are currently a flat structure, but will probably be extended to a nested, folder-like structure in the future. 

In the left panel, categories are merged by name. This means if two libraries contain a category with the same name, content from both libraries is listed under that name.

### Attributes

Each asset can have an unlimited number of *attributes*. An attribute is a key/value pair that you can easily search for. For example, you might add an *Author* attribute with the names of the people who worked on an asset. 

A neat feature is that we interpret a value as multiple values if you separate them by comma. That allows us to make the values clickable in the details panel, which then results in the correct search query.

### Other Details

Additionally, there is a description field where you can put free text, and some asset types like prefabs show additional information like number of vertices and triangles.

Finally, since package dependencies in Unity are read-only, a library can be locked, which means you cannot edit it.

## Browsing

You can quickly search for keywords by typing a query in the search bar at the top. It instantly filters as you type. The keyword is searched in the name of the asset and the attribute values (not the description, nor the attribute keys). In order to filter by a specific attribute, type `name:keyword`. This will only search for assets that have the given attribute name and match the attribute value.

By clicking on the categories in the left panel, you can filter assets by category. If no category is selected, all assets are shown. You can select multiple categories by holding the `CTRL` key.

You can also toggle entire libraries by toggling the checkbox in the library listing at the top of the left panel.

## Editing

If you want to contribute to the asset package, you'll need you git clone the repository and import it as a local package (as opposed to loading it through the VPE registry, which is the default). Once you do that, you can select the library asset and unlock it.

You add assets by dragging them from the project window into the asset panel. You need to have one category selected before doing that, so we know to which category to assign the asset. Alternatively, you can directly drop it on a category in the left panel.

You can edit the meta data in details panel when the asset is selected. 

When editing the attribute name, a list of already existing names is proposed, but you can also use your own. The same goes for the attribute values: If an attribute is found in the database, the values of that attribute are proposed as you type. This also works for comma-separated values (e.g. it only proposes to autocomplete the current value, not the entire string).

When creating a new category, you'll need to specify in which library it should be created. There is a drop-down next to the *Add* button in the left panel under the category listing. You can also delete categories, but they must be empty. In order to move an asset to another category, you drag and drop it from the asset panel into the new category in the left panel. If the category doesn't exist in the asset's library, it'll be created.

### Exporting Assets

When adding new assets, make sure the models are correctly sized and oriented (Unity uses a left-handed, Y-Up coordinate system, while Blender is right-handed and Z-Up). Checklist:

- Make sure that size and rotation is applied to all of your objects.
- Check that the origin is at the correct position for each object.
- When exporting, to FBX, make sure that:
  - *Forward* is set to "Z-Forward"
  - *Up* is set to "Y Up"
  - *Apply Transform* is checked

## Locking

When a library is locked, assets of that library are in read-only mode (you cannot edit their meta data through the Asset Library window). Since the categories in the left panel are grouped together from multiple libraries, we indicate how you can edit them:

- If a lock is displayed next to the category, all of the libraries with that category are locked.
- When at least one library is locked but others aren't, a small note is displayed which libraries the change will apply to.
- When all libraries of a category are unlocked, no note is displayed and the change is applied to all libraries.

All other manipulations are marked as failed when trying to edit locked assets. For example, you'll see a red bar in the asset panel with a message when trying to drop a new asset from a location under a locked library. You equally won't be able to move assets into a new category when it's locked.

Controls that cannot be used are usually hidden. For example, if all your libraries are locked, the *Add* button for categories is not shown.

## Import and Export

When exporting, the imported assets are bundled with your build. That means if at a later point the asset is updated in the package, you explicitly have to pull down the latest version of the package, and re-export your build.

# Asset Library Creation Guide

So you're good with modeling and you'd like to contribute to VPE's asset library. Great! This guide should provide you with the necessary info to make your assets available in VPE.

Note that we'll focus on 3D assets, i.e. textured models. In the future we will support other assets (materials, textures, etc), but 3D assets are the most common, and most difficult to get right.

## Modeling

We won't go into too much detail how to model your asset, but rather provide a checklist your model should comply with.

- **Use real-world scale** - You should model your asset based on real-world units. This will avoid many scaling issues. 
- **Verify origin** - The origin of your model should be where it makes sense. For example, items that sit typically on the playfield should have their origin where they touch the playfield. Note that we ignore where your object is in world space, what's important is the origin.
- **Apply scale and rotation** - Your object shouldn't have any non-applied transformations. Everything should be baked into the mesh (in Blender: `CTRL+A` -> *Apply Scale and Rotation*).
- **Add bevels to edges** - See *Baking* below.
- **Use quads instead of triangles** - This is somewhat less relevant for game assets, but a quad topology will give you mush less headache in general, specially when beveling edges. Also check out our [tutorial about 3D scans](xref:tutorial_3d_scan).
- **Make meshes watertight** - Close the holes, make sure your mesh has faces everywhere. This will avoid shadow and path tracing problems.

### Baking

Using [normal maps](https://en.wikipedia.org/wiki/Normal_mapping), it's possible to bake geometric details into a texture, which is much cheaper to render than using actual mesh geometry. While the majority of your normal maps will be defined by the material you're applying, there are a few cases where it's worth baking actual geometry into your normal map.

- **Bevels** - There are no sharp edges in the real world. Bevels are important because they reflect the light, and it's instantly noticeable when an edge has no bevel. Add a bevel modifier to your hi-poly meshes.
- **Notches** - Anything that goes *into* a mesh, where you can't really see a silhouette, is probably worth baking.

Blender can easily bake normal maps. You'll need an UV-mapped low-poly mesh, and a high poly mesh, and it'll create a normal map for the low-poly mesh with the high-poly meshes details.

### Materials

Unity's HDRP uses a [physically based renderer](https://www.adobe.com/ie/products/substance3d/discover/pbr.html). This means that your materials should include at least the following maps:

- Albedo (a.k.a. color map, a.k.a. base map)
- Ambient occlusion
- Normal map
- Metallic map
- Smoothness map

Note that HDRP packs AO, metallic and smoothness [into one single texture](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@12.1/manual/Mask-Map-and-Detail-Map.html) called *Mask Map*. In Blender, you should be able to do this in the node editor, Substance Painter has a preset that does it automatically when exporting the textures.

You'll probably run into a situation where your object has multiple materials. There are a couple of ways dealing with that. Keep in mind it's always better to keep the number of meshes and materials low.

1. **Multiple meshes** - The most expensive way is to split your mesh into multiple meshes, one mesh per material. You should only do this if you're able to re-use the meshes in other assets. For example, a post and its rubber is a good use case, since both the post and the rubber can be paired with other objects.
2. **Multiple materials** - Another way is to create multiple material slots, i.e. assign each vertex of the mesh to the appropriate material. This is appropriate when you want to be able to swap out materials individually, for example in a flipper. In this case, you'd have two separate materials for the rubber and the plastic, but only one mesh. This allows you to create material variations in the asset library, where you provide multiple materials for each slot, and the asset browser will show the combinations for all of them.
3. **Single material** - The cheapest way is to merge multiple materials into one, and it's the recommended way when materials don't need to be easily changed. In Substance Painter, you would create a layer for each material and mask it out so it applies only to the desired region. Note that this only works when the [surface type](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@12.1/manual/Surface-Type.html) of the materials is the same, i.e. the only difference between the materials are their texture maps.

It's also common to create materials made for multiple items. For example, in the library, all metal posts use the same material, so they share the same UV mapping. Grouping multiple items makes sense when it's likely that many of the items you're grouping will be used on the playfield.

### Format

Once you're happy with your model, you need to export it into a format that Unity can read. While Unity is able to deal with [multiple formats](https://docs.unity3d.com/Manual/3D-formats.html), it's recommended to use FBX.

When exporting, make sure the proper scaling is applied (Blender doesn't to that per default!), and that the coordinate system is correct. Unity uses a left-handed, Y-Up coordinate system, while most modeling software is right-handed and Z-Up. In Blender, when exporting to FBX, make sure to set:

  - *Forward* to "Z-Forward"
  - *Up* to "Y Up"
  - *Apply Transform* is checked

To test, drag your exported FBX into a Unity scene (*outside* the playfield), and make sure that scaling is one, rotation to zero, and that the size and orientation is correct.

## Where to Put Stuff

Given you're contributing to VPE's [Asset Library](https://github.com/VisualPinball/VisualPinball.Unity.AssetLibrary), we have the following structure:

- The root of the library is at `Assets/Library`.
- In the root, there are folders that correspond to the categories.
- Inside each category folder, there are usually more folders, where each folder corresponds to an FBX file.
- Beside the FBX file, the FBX folder contains the textures, materials and prefabs of items in the FBX.
- There is usually an `src~` folder (which is ignored by Unity due to the trailing `~`), which contains the `.blend` and other source files.

We usually pack multiple items into an FBX file. Unity has its own, internal asset structure, so it's up to you how to split your files. For items ported over from [pinball-parts](https://github.com/vbousquet/pinball-parts), we use the same file structure.



What has been working well so far:

- In Blender, use collections for your hi-poly and low-poly models.
- UV-map and export the low-polys into the FBX folder you're working in.
- Export the hi-polys into the `src~` folder.
- Open the low-poly into Substance Painter
- Bake the mesh maps using the hi-poly. Disable *Average Normals*, and you might decrease the min distance to 0.001.
- Texture the model and export the maps to the FBX folder.

## Unity Setup

So you've exported your model and textures. Now it's time to create the asset in Unity. For 3D assets, what you're going to create is a [Prefab](https://docs.unity3d.com/Manual/Prefabs.html).

[Creating a prefab](https://docs.unity3d.com/Manual/CreatingPrefabs.html) is simply done by dragging a GameObject from the Hierarchy into the Project window. However, you'll need to get your model *into* the hierarchy before that. So the first step is to drag your FBX file into the scene.

Now, FBX files are assets too, but what we want is to create an asset for *each* of the objects contained in the FBX file. That's why the next step is to unpack the FBX prefab that was just created. We can do that by right-clicking on the GameObject in the hierarchy and choose *Prefab -> Unpack*. Note that the GameObjects of the unpacked prefab still reference the meshes in the FBX file, it's just that now they can be placed individually where we want, as opposed to be a fixed part of the FBX hierarchy.

### Coordinate Systems

Since VPE's physics engine is a port of Visual Pinball, VPE uses the same coordinate system. Unfortunately, it's not the same as Unity's (or any other, existing system). In order to resolve that, the *Playfield* GameObject under your table has a transformation that we're hopefully going to be removing soon.


