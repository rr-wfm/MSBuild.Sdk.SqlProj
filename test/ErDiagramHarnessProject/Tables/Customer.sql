CREATE TABLE dbo.Customer
(
    CustomerId INT NOT NULL,
    CustomerName NVARCHAR(100) NOT NULL,
    IsBlue BIT NULL,
    IsRed BIT NULL,
    AnyColor AS (CASE WHEN ISNULL(IsBlue, 0) = 1 OR ISNULL(IsRed, 0) = 1 THEN 1 ELSE 0 END),
    IsColorSelected AS (CONVERT(BIT, AnyColor)),
    CONSTRAINT PK_dbo_Customer PRIMARY KEY CLUSTERED (CustomerId)
);
