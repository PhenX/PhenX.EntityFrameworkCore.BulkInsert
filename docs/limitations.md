# Known limitations

For now this library does not support the following features:

* **Navigation properties**: ✅ Supported via the `IncludeGraph` option (see [Graph Insert documentation](./graph-insert.md)).
* **Change tracking**: The library does not track changes to the entities being inserted. This means that you cannot use the `DbContext.ChangeTracker` to track changes to the entities after they have been inserted.
* **Inheritance**: The library does not support inserting entities with inheritance (TPT, TPH, TPC). You can only insert entities of a single type.

Please vote for the features you would like to see in the [GitHub issues](https://github.com/PhenX/PhenX.EntityFrameworkCore.BulkInsert/issues).
