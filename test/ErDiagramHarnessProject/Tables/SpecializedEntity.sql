CREATE TABLE dbo.SpecializedEntity
(
    SpecializedEntityId INT NOT NULL IDENTITY (1, 1),
    EntityId INT NOT NULL,
    EntityTypeCode AS (CONVERT(TINYINT, 8)) PERSISTED NOT NULL,
    CONSTRAINT PK_dbo_SpecializedEntity PRIMARY KEY CLUSTERED (SpecializedEntityId),
    CONSTRAINT FK_dbo_SpecializedEntity_dbo_EntityTypeLookup FOREIGN KEY (EntityTypeCode) REFERENCES dbo.EntityTypeLookup (EntityTypeCode),
    CONSTRAINT FK_dbo_SpecializedEntity_EntityId_EntityTypeCode_dbo_Entity FOREIGN KEY (EntityId, EntityTypeCode) REFERENCES dbo.Entity (EntityId, EntityTypeCode)
);
