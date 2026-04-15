CREATE TABLE sales.LineItem
(
    LineItemId INT NOT NULL,
    InvoiceId INT NOT NULL,
    ExternalCode NVARCHAR(50) NULL,
    DisplayValue AS ExternalCode,
    CONSTRAINT PK_sales_LineItem PRIMARY KEY CLUSTERED (LineItemId),
    CONSTRAINT FK_sales_LineItem_sales_Invoice FOREIGN KEY (InvoiceId) REFERENCES sales.Invoice(InvoiceId)
);
