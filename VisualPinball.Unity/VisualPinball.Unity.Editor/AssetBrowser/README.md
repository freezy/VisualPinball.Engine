# Asset Browser

VPE ships with an asset browser that offers quick access to VPE's curated asset library. This library not only includes ready-to-use prefabs and materials, but also a meta data system that allows browsing by category and other attributes such as part number, author, manufacturer or era.

The asset browser is accessible through the *Visual Pinball* menu under *Asset Browser*. 

## Overview

*Assets* are objects that you can also browse through Unity's *Project* window. It can be anything from prefabs, textures, sounds, or materials. A *library* is a collection of assets under a given folder, and the asset browser supports multiple libraries.

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

You can edit the meta data in details panel when the asset is selected. Attributes are edited by right-clicking on them and selecting *Edit*, or by double-clicking on the attribute name. Deleting is equally done by right-clicking on the attribute name and choosing *Delete*.

When editing the attribute name, a list of already existing names is proposed, but you can also use your own. The same goes for the attribute values: If an attribute is found in the database, the values of that attribute are proposed as you type. This also works for comma-separated values (e.g. it only proposes to autocomplete the current value, not the entire string).

When creating a new category, you'll need to specify in which library it should be created. There is a drop-down next to the *Add* button in the left panel under the category listing. You can also delete categories, but they must be empty. In order to move an asset to another category, you drag and drop it from the asset panel into the new category in the left panel. If the category doesn't exist in the asset's library, it'll be created.

## Locking

When a library is locked, assets of that library are in read-only mode. Since the categories in the left panel are grouped together from multiple libraries, we indicate how you can edit them:

- If a lock is displayed next to the category, all of the libraries with that category are locked.
- When at least one library is locked but others aren't, a small note is displayed which libraries the change will apply to.
- When all libraries of a category are unlocked, no note is displayed and the change is applied to all libraries.

All other manipulations are marked as failed when trying to edit locked assets. For example, you'll see a red bar in the asset panel with a message when trying to drop a new asset from a location under a locked library. You equally won't be able to move assets into a new category when it's locked.

Controls that cannot be used are usually hidden. For example, if all your libraries are locked, the *Add* button for categories is not shown.

## Import and Export

You import assets into your scene by dragging them from the asset panel into the scene. When exporting, the imported assets are bundled with your build. That means if at a later point the asset is updated in the package, you explicitly have to pull down the latest version of the package, and re-export your build.
