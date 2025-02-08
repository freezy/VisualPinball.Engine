# Packaging

By packaging we mean serializing a table file. Table files in VPE come with the `.vpe` file extension and are based on three common technologies:

- The container format is a ZIP file.
- The mesh and texture data is stored as a [glTF binary](https://www.khronos.org/gltf/).
- The non-binary metadata is stored in JSON files.

> [!NOTE]  
> Originally, there were thoughts about using the same container format as VPX (the [Compound Binary File](https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-cfb/53989ce4-7b05-4f8d-829b-d08d6148375b)), but ultimately, given the inner structure would be quite different anyway, there was no real benefit.
>
> We've also tested [a more efficient packing structure](https://github.com/Cysharp/MemoryPack) than JSON, but since the metadata to which it would apply is minuscule compared to the rest, the performance advantage was quickly outweighed by its unreadability and the hassle to set up.

## File Structure

If you extract a `.vpe` file, you'll see the following structure:

```plain
table
  ├─ 📁 assets
  │   └─ 📁 PhysicsMaterial
  │       ├─ 📄 WallMaterial.json
  │       └─ 📄 WallMaterial.meta.json
  ├─ 📁 global
  │   ├─ 📄 coils.json
  │   ├─ 📄 lamps.json
  │   ├─ 📄 switches.json
  │   └─ 📄 wires.json
  ├─ 📁 items
  │   ├─ 📁 0
  │   ├─ 📁 0.0
  │   │     ...
  │   └─ 📁 0.0.5.0
  │       ├─ 📁 Bumper
  │       │   └─ 📄 0.json  
  │       └─ 📁 BumperCollider
  │           └─ 📄 0.json  
  ├─ 📁 meta
  │   └─ 📄 colliders.json
  ├─ 📁 refs
  │   ├─ 📁 0
  │   ├─ 📁 0.1
  │   │     ...
  │   └─ 📁 0.1.2.3
  │       ├─ 📁 BumperCollider
  │       └─ 📁 BumperSound
  │           └─ 📄 0.json
  ├─ 📄 table.glb
  └─ 📄 colliders.glb

```

## Export

Let's go through those items by looking at how they are written. Afterwards, we'll go through the reading process as well, to see the differences between editor and runtime loading.

You can open up `PackageWriter` to see the implementation.

### glTF Export

We start by exporting the entire GameObject hierarchy starting at the table node as glTF. We're using [Unity's fork](https://docs.unity3d.com/Packages/com.unity.cloud.gltfast@6.10/manual/index.html) of [`atteneder/glTFast`](https://github.com/atteneder/glTFast) for this. The binary data is [streamed](https://docs.unity3d.com/Packages/com.unity.cloud.gltfast@6.10/api/GLTFast.Export.GameObjectExport.html#GLTFast_Export_GameObjectExport_SaveToStreamAndDispose_System_IO_Stream_System_Threading_CancellationToken_) directly into the ZIP archive's input stream to keep the memory footprint low.

The glTF export includes the hierarchy, meshes, and materials, but does not include any component data, external assets, or other metadata needed for the table to run.

> [!NOTE]
> We are not 100% sure yet how materials work. They seem to be restored correctly in HDRP, but they might use special shaders after import. We might need to side-load them as well.
>
> The resulting binary ends up at the root of the archive as 📄 table.glb.

### Collider Meshes

Some of our components use a mesh for physics collision. In Unity, these are references to regular meshes, but they aren't part of the hierarchy recognized by the glTF exporter, so we export them separately.

We do this by fetching all `IMeshCollider` components, generating a GUID for each, and exporting them (named by the GUID) in another `.glb` file, `📄 colliders.glb`. We keep a reference between the meshes’ instance IDs and these GUIDs for when we export the components.

Additionally, we save whether the component using the collider mesh is part of a prefab. This is important so we can identify the prefab when the package is re-imported into the editor, and re-link it, if so. This data is saved as `📄 colliders.json` in the `📁 meta` folder and looks like this:

```json
{
  "9c42922e-a8b8-416a-b859-b22f24fa205e": {
    "IsPrefabMeshOverriden": false,
    "PrefabGuid": "1d547d87da8b11c44a083695469ff8b8",
    "PathWithinPrefab": ""
  }
}
```

Here, we store a map keyed by the GUID for quick lookup, along with the prefab’s GUID if there is one, and whether the mesh was actually overridden. The `PathWithinPrefab` property points to the object within the prefab, because there might be multiple objects.

### Components

Next, we serialize the GameObject and component data into `📁 items` and `📁 refs`. The structure inside these folders is the same. On the first level, folders are named after each GameObject’s indices in the hierarchy. The second level defines the type of the component (for example, `📁 Bumper` maps to `BumperComponent` through the class's `[PackAs]` attribute). Finally, at the third level, the actual data is stored as JSON.

Each component determines for itself which data is written to `📁 items` and which to `📁 refs`. The purpose of these two folders is that data is read in two passes during import: the first pass creates the components, and the second pass updates cross-references between them.

### Globals

Global data is then written to `📁 global`. Currently, this folder contains mappings for switches, coils, lamps, and wires.

### Assets

In this context, assets are instances of `ScriptableObject`, usually serialized in the editor as `.asset` files. We save them to the `📁 assets` folder in our package.

Assets are grouped into folders based on their type (again determined by the `[PackAs]` attribute). Because they are deserialized as-is, we need an easy way to reference them, which is the purpose of their `*.meta.json` counterparts. The goal of these meta files is to link each asset to an identifier, which is then used by the component data.

### More to come

Future additions will include sounds, shaders, external dependencies such as PinMAME, MPF, and Visual Scripting, and more.


## Import

We'll quickly go through the editor import process to explain a few details that were only implied in the export section above.

One important point is that loading a `.vpe` file during runtime is fundamentally different from loading it into the editor. While the runtime goal is simply to play the table, the editor goal is to import it so it can be easily modified. It's also supposed to save all the data in a folder structure that is easily accessible to the user.

- The glTF import uses different APIs in the editor versus runtime. In the editor, we write the .glb binary to the asset folder of the Unity project, load it as a prefab, and instantiate a GameObject from it. At runtime, we instantiate a GameObject directly from the binary in memory.
- The order in which data is imported is important for both runtime and edit time, because some steps depend on others:
	1. Load `📄 table.glb`, which gives us the scene hierarchy.
	2. Unpack `📁 assets` and `📄 colliders.glb`
	3. Loop through `📁 items` and do, in this order:
		1. Instantiate and apply components
		2. Link them to their prefab (if the prefab exists in the editor).
		3. Apply component data.
	4. Loop through `📁 refs` and restore cross-references between components.
	5. Import data from `📁 global`.
