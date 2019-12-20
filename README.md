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

## How to Use

This produces a DLL (or three, if you count the dependencies). In Unity, drop 
these into your Assets folder. Then create a script and attach it to something,
like the camera. Then create the scene on startup:

```cs
using UnityEngine;
using VisualPinball.Engine.VPT.Table;

public class Init : MonoBehaviour
{

	// Start is called before the first frame update
	void Start()
	{
		var scale = 0.002f;
		var table = Table.Load(@"D:\Pinball\Visual Pinball\Tables\Batman Dark Knight tt&NZ 1.2.vpx");
		var material = new Material(Shader.Find("Specular"));

		foreach (var primitive in table.Primitives.Values)
		{
			var gameObj = new GameObject(primitive.Name);
			gameObj.AddComponent<MeshFilter>();
			gameObj.AddComponent<MeshRenderer>();
			
			var mesh = new Mesh();
			gameObj.GetComponent<MeshFilter>().mesh = mesh;
			gameObj.transform.localScale = new Vector3(scale, scale, scale);
			gameObj.transform.eulerAngles = new Vector3(-90, 0, 0);
			gameObj.GetComponent<MeshRenderer>().material = material;

			// vertices
			var vertices = new Vector3[primitive.Data.NumVertices];
			var normals = new Vector3[primitive.Data.NumVertices];
			var uvs = new Vector2[primitive.Data.NumVertices];
			for (var i = 0; i < vertices.Length; i++) {
				var vertex = primitive.Data.Mesh.Vertices[i];
				vertices[i] = new Vector3(vertex.X, vertex.Y, vertex.Z);
				normals[i] = new Vector3(vertex.Nx, vertex.Ny, vertex.Nz);
				uvs[i] = new Vector2(vertex.Tu, vertex.Tv);
			}
			mesh.vertices = vertices;
			mesh.normals = normals;
			mesh.uv = uvs;

			// faces
			mesh.triangles = primitive.Data.Mesh.Indices;

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