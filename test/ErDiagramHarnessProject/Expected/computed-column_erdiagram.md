```mermaid
erDiagram
  "dbo.Entity" {
    EntityId int PK
    EntityTypeCode tinyint FK
  }
  "dbo.Entity" }o--|| "dbo.EntityTypeLookup" : FK_dbo_Entity_EntityTypeLookup
  "dbo.EntityTypeLookup" {
    EntityTypeCode tinyint PK
  }
  "dbo.SpecializedEntity" {
    SpecializedEntityId int PK
    EntityId int FK
    EntityTypeCode computed(NULL) FK
  }
  "dbo.SpecializedEntity" }o--o| "dbo.EntityTypeLookup" : FK_dbo_SpecializedEntity_dbo_EntityTypeLookup
  "dbo.SpecializedEntity" }o--o| "dbo.Entity" : FK_dbo_SpecializedEntity_EntityId_EntityTypeCode_dbo_Entity
```
