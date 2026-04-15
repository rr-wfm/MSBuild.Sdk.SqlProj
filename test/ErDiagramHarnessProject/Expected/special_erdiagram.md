```mermaid
erDiagram
  "dbo.Customer" {
    CustomerId int PK
    CustomerName nvarchar(100) 
    IsBlue bit(NULL) 
    IsRed bit(NULL) 
    AnyColor computed(NULL) 
    IsColorSelected computed(NULL) 
  }
  "sales.LineItem" {
    LineItemId int PK
    InvoiceId int FK
    ExternalCode nvarchar(50)(NULL) 
    DisplayValue computed(NULL) 
  }
  "sales.LineItem" }o--|| "sales.Invoice" : FK_sales_LineItem_sales_Invoice
  "sales.Invoice" {
  }
```
