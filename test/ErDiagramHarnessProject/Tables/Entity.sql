CREATE TABLE dbo.Entity
(
    EntityId INT NOT NULL IDENTITY (1, 1),
    EntityTypeCode TINYINT NOT NULL,
    CONSTRAINT PK_dbo_Entity PRIMARY KEY CLUSTERED (EntityId),
    CONSTRAINT FK_dbo_Entity_EntityTypeLookup FOREIGN KEY (EntityTypeCode) REFERENCES dbo.EntityTypeLookup (EntityTypeCode)
);
GO

CREATE UNIQUE NONCLUSTERED INDEX UQ_dbo_Entity_EntityId_EntityTypeCode
    ON dbo.Entity (EntityId, EntityTypeCode);
