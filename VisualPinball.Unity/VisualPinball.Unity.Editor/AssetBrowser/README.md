# Asset Browser

VPE ships with an asset browser that offers quick access to VPE's curated asset database. This database not only includes ready-to-use prefabs and materials, but also a meta data system that allows browsing by category and other attributes such as part number, dimensions, manufacturer or era.

The asset browser is accessible through *Visual Pinball* menu under *Asset Browser*. 

## Data

Internally, we're using [LiteDB](https://www.litedb.org/), a serverless NoSQL database. The asset browser supports multiple databases at once. This allows for multiple external asset libraries to be accessed, as well also project-specific databases, should that be desired.

The browser view displays assets from all databases. Categories are merged by name. This means if two database contain a category with the same name, content from both libraries will be listed under that name. If the category is renamed, it's renamed in both databases. If you move an item from a category that exists only in another database, that category will be created.
