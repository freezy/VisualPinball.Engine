# Unity Patching System

While we handle most of VPX's quirks in a generic way, there are things that 
are impossible to deal with because they are implemented in the table via 
script. Examples are ball- or flipper shadows, or material properties that 
cannot be reproduced the same way in the Unity renderer.

This project allows to apply changes to an imported table in a simple, 
automated and reusable way.

## How?

In order to patch something, we create a class under `VisualPinball.Unity.Patcher.Patcher`.
There are currently two sub packages, `Common` for changes likely to be applied
to all tables, and a `Tables` package with classes that concern only specific
tables.

## Matcher

Before we can patch, we need to know *what* to patch. More precisely, we need
to:

1. Identify the table
2. Identify the element we want to patch.

We use [Attributes](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/concepts/attributes/)
to do that.

### Identify the Table

Create a matcher class by extending `TableMatchAttribute` or use one of the 
existing ones. If this matcher returns a negative value, all patch methods of
the underlying class are ignored.

For example, if you want to match a table with the file name "foo.vpx", you 
would create the attribute class like that:

```cs
public class FooMatchAttribute : TableMatchAttribute
{
	public override bool Matches(Engine.VPT.Table.Table table, string fileName)
	{
		return fileName == "foo.vpx";
	}
}
```

Then, decorate your patch class with the attribute (you can omit the `Attribute` suffix when using attributes):

```cs
[FooMatch]
public class MyTablePatcher
{
}
```

This isn't very DRY though. If matching by file name is something you're doing
a lot, you would abstract this:

```cs
public class FileNameMatchAttribute : TableMatchAttribute
{
	private readonly string _fileName;

	public NameMatchAttribute(string fileName)
	{
		_fileName = fileName;
	}

	public override bool Matches(Engine.VPT.Table.Table table, string fileName)
	{
		return fileName == _fileName;
	}
}
```

And use it like so:

```cs
[FileNameMatch("foo.vpx")]
public class MyTablePatcher
{
}
```

### Identify the Game Item

Now we know how to filter the table, let's look at how to identify the
table element to patch. We use the same principle as above, but by extending
`ItemMatchAttribute` instead of `TableMatchAttribute`.

You've noticed that the table matcher classes get only the parsed table and the
file name of the table to decide whether to match or not. Since we're dealing
with table items, we get a lot more input here, namely:

- the parsed table (maybe you want to exclude some tables)
- the table item (this holds the name of the element)
- the render object (maybe the match is based on the mesh itself)
- the Unity game item (maybe the match is based on the converted object)

So, here's an example:

```cs
public class NameMatchAttribute : ItemMatchAttribute
{
	public bool IgnoreCase = true;

	private readonly string _name;

	public NameMatchAttribute(string name)
	{
		_name = name;
	}

	public override bool Matches(Engine.VPT.Table.Table table, IRenderable item, RenderObject ro, GameObject obj)
	{
		return IgnoreCase
			? string.Equals(item.Name, _name, StringComparison.CurrentCultureIgnoreCase)
			: item.Name == _name;
	}
}
```

Let's look at how to apply this in the next section.

## Patcher

Now we know how to match an item, let's create a patch. It's as easy as
writing a `void` method in your patch class that takes in Unity's `GameObject`
and decorate it with an item matcher.

Let's take the name matcher from the example above and use it to hide the
flipper shadows:

```cs
[NameMatch("LeftFlipperSh")]
[NameMatch("RightFlipperSh")]
public void RemoveFlipperShadow(GameObject gameObject)
{
	gameObject.SetActive(false);
}
```

Putting several attributes on a method means that it is matched if *at least 
one* of the matchers matches (the same applies to the table matchers, by the 
way). 

## Summary

For a new table:

1. Create a patch class. Preferably at `Patcher.Tables` package. Name it 
   however you want.
2. Put a class matcher attribute on it.
3. For every element to patch, create a new method and add an item matcher 
   attribute on it.

All classes are automatically loaded, there is no need to register them 
anywhere.
