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
- Table info is parsed
- Primitives are parsed

## Unity

There's an included project that should make it easy to integrate with Unity. 
The goal is to bridge Unity's APIs with the ones the `VisualPinball.Engine` 
library provides. It currently consist of extension methods that add 
Unity-specific methods. 

Drop the following DLLs from your `VisualPinball.Unity` build folder into Unity's
asset folder:

- `VisualPinball.Engine.dll` - The game engine agnostic main library
- `VisualPinball.Unity.dll` - The Unity extensions
- `OpenMcdf.dll` - The VPX file format dependency
- `zlib.net.dll` - The ZLib compression dependency

Then create a script and attach it to something, like the camera. The script
could look something like this:

```cs
using UnityEngine;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Unity.Extensions;

public class Init : MonoBehaviour
{

	// Start is called before the first frame update
	void Start()
	{
		var scale = 0.002f;
		var table = Table.Load(@"path-to-a-vpx-file");
		var material = new Material(Shader.Find("Specular"));

		foreach (var primitive in table.Primitives.Values)
		{
			var vpMesh = primitive.GetMesh(table);
			var mesh = vpMesh.ToUnityMesh();
			var gameObj = new GameObject(primitive.Name);
			gameObj.AddComponent<MeshFilter>();
			gameObj.AddComponent<MeshRenderer>();
			
			gameObj.GetComponent<MeshFilter>().mesh = mesh;
			gameObj.GetComponent<MeshRenderer>().material = material;
			gameObj.transform.localScale = new Vector3(scale, scale, scale);
			gameObj.transform.eulerAngles = new Vector3(-90, 0, 0);
		}
	}

	// Update is called once per frame
	void Update()
	{
	}
}
```

I'm sure there are better ways though, but that's what I've figured out in my 
first hour with Unity. ;)

## License

GPLv2, see [LICENSE](LICENSE).
