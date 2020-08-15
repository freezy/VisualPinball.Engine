# Unity

[Unity](https://unity.com/) is one of the leading game engines. It has 
previously been dominating the mobile game segment, but in recent years
has caught up a lot in terms visual fidelity for high-end platforms.

Unity is free for non-commercial projects. VPE provides Unity support in a
separate DLL, called `VisualPinball.Unity`.

## Status

Currently this part of VPE is about importing the meshes, textures and 
materials correctly. You can either do a full import, meaning Unity will
generate its own assets for an imported table, a quick import which only loads
it into memory, or drag a `.vpx` file into Unity directly.

![Monster Bash in Unity](mb_unity_teaser.jpg)

It currently uses the built-in renderer, but will also be compatible with
Unity's [High Definition Render Pipeline](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@7.1/manual/index.html)

### Usage

This repository acts as a Unity Package. That means you can import it into
your Unity project as a package. In order to do that:

1. Open Package Manager (*Window / Package Manager*)
2. Click on the "plus" button on the top left
3. Choose *Add package from disk*
4. Navigate where you cloned this repo and select `package.json`

You'll then have a *Visual Pinball* menu in the Unity editor where you can 
import `.vpx` files. You'll be also able to drag and drop `.vpx` files into 
your asset folder and Unity will create the table model directly.

## Future

Unity allows extending its editor. This would allow us to use Unity as a table
editor, given VPE is able to write `.vpx` files. While the Unity editor is not
a modelling tool, it has excellent integration with existing tools like 
Blender, so would facilitate the workflow for table authors a lot.

Since the VPX file format acts like a virtual file system, it would be possible
to save additional assets such as custom materials or shaders to the `.vpx` 
file without breaking backwards compatibility.

This would allow table authors to provide tables that run in Visual Pinball and
at the same time make use of Unity's more advanced shaders.
