CREATE VIEW dbo.AccountOrder AS
    SELECT account.AccountId, customerOrder.OrderId
    FROM [ProducerA].[dbo].[Account] AS account
    INNER JOIN [ProducerB].[dbo].[Order] AS customerOrder ON account.AccountId = customerOrder.AccountId;
