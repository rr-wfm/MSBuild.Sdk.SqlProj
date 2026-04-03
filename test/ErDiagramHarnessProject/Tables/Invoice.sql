CREATE TABLE [sales].[Invoice]
(
    InvoiceId INT NOT NULL,
    CustomerId INT NOT NULL,
    OwnerEmployeeId INT NULL,
    CONSTRAINT PK_sales_Invoice PRIMARY KEY CLUSTERED (InvoiceId),
    CONSTRAINT FK_sales_Invoice_dbo_Customer FOREIGN KEY (CustomerId) REFERENCES dbo.Customer(CustomerId),
    CONSTRAINT FK_sales_Invoice_dbo_Employee FOREIGN KEY (OwnerEmployeeId) REFERENCES hr.Employee(EmployeeId)
);
