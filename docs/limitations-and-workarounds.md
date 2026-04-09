---
id: limitations-and-workarounds
title: Limitations and Workarounds
---

# Limitations and Workarounds

## XML schema collections (`.xsd`)

`MSBuild.Sdk.SqlProj` does not currently process `.xsd` files added as build inputs the same way as classic `.sqlproj` projects.

If your existing project uses an entry like this:

```xml
<ItemGroup>
  <Build Include="XMLSchemaCollection1.xsd">
    <RelationalSchema>dbo</RelationalSchema>
    <XMLSchemaCollectionName>XMLSchemaCollection1</XMLSchemaCollectionName>
  </Build>
</ItemGroup>
```

use a `.sql` file instead that creates the XML schema collection directly:

```sql
CREATE XML SCHEMA COLLECTION [dbo].[XMLSchemaCollection1]
    AS N'<?xml version="1.0" encoding="utf-16"?>
<xs:schema id="XMLSchemaCollection1"
    xmlns:xs="http://www.w3.org/2001/XMLSchema">
</xs:schema>';
```

In practice, the workaround is to copy the contents of the `.xsd` file into the `N'...'` string of a `CREATE XML SCHEMA COLLECTION` statement and include that `.sql` file in your project instead of the `.xsd` file.
