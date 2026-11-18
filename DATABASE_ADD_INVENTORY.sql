/*
  MIGRATION BO SUNG TON KHO - BE4 / MyStore
  Sao luu database truoc khi chay.
  Chay trong SQL Server Management Studio tren database MyStore.
  Sau khi chay: Visual Studio > EDMX > Update Model from Database > Refresh Product.
  Kiem tra Product.cs co QuantityInStock va QuantitySold sau khi cap nhat model.
*/
IF COL_LENGTH('dbo.Product', 'QuantityInStock') IS NULL
    ALTER TABLE dbo.Product ADD QuantityInStock INT NOT NULL
        CONSTRAINT DF_Product_QuantityInStock DEFAULT (0);
GO
IF COL_LENGTH('dbo.Product', 'QuantitySold') IS NULL
    ALTER TABLE dbo.Product ADD QuantitySold INT NOT NULL
        CONSTRAINT DF_Product_QuantitySold DEFAULT (0);
GO
IF EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE name = 'CK_Product_Quantity_NonNegative'
)
    ALTER TABLE dbo.Product DROP CONSTRAINT CK_Product_Quantity_NonNegative;
GO
ALTER TABLE dbo.Product WITH CHECK ADD CONSTRAINT CK_Product_Quantity_NonNegative
CHECK (QuantityInStock >= 0 AND QuantitySold >= 0);
GO
