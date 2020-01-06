# Visual Pinball Engine for C#

*Work in progress*

## Why?

Today we have nice game engines like Unity or Godot that support C# out of the
box. The goal of this library is to easily provide what Visual Pinball makes so
great to other "current gen" engines, while keeping backwards-compatibility.

In other words, lay the foundations of a Visual Pinball player that loads any
table that Visual Pinball (or at least a recent version) plays.

Concretely, it should:

- Read VPX files
- Provide an API to extract geometry, textures and materials
- Provide a hook for a game loop that runs the game
- Provide an event API for geometry changes

## Status Quo

- Table loader created
- Table info, primitives, materials and textures are parsed

## Unity

There's an included project that should make it easy to integrate with Unity.
The goal is to bridge Unity's APIs with the ones the `VisualPinball.Engine`
library provides. It currently consist of extension methods that add
Unity-specific methods, as well as an importer.

The `VisualPinball.Unity` project comes with a target that copies all needed
DLLs to your Unity project's asset folder. In order to activate it, set the
`VPE_UNITY_SCRIPTS` environment variable and point it to the correct path.
Apart from the dependencies, it will copy the the following DLLs:

- `VisualPinball.Engine.dll` - The game engine agnostic main library
- `VisualPinball.Unity.dll` - The Unity extensions

There are two ways of importing a table. When importing runtime, create a new
script, attach it to something, and do the import on start:

```cs
using UnityEngine;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Unity.Extensions;

public class Init : MonoBehaviour
{

	// Start is called before the first frame update
	void Start()
	{
		VpxImporter.ImportVpxRuntime(@"<path-to-vpx>");
	}

	// Update is called once per frame
	void Update()
	{
	}
}
```

We also provide an editor import function. When the Unity DLL is in your
project, you'll have a *Visual Pinball* menu where you can import .vpx files.


## License

GPLv2, see [LICENSE](LICENSE).
