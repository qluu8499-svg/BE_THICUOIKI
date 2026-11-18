-- Chạy script này trên database BE4_LUUTHANHQUAN trước khi chạy ứng dụng.
IF COL_LENGTH('dbo.[Order]', 'ShippingMethod') IS NULL
    ALTER TABLE dbo.[Order] ADD ShippingMethod NVARCHAR(50) NOT NULL
        CONSTRAINT DF_Order_ShippingMethod DEFAULT N'Standard' WITH VALUES;
GO
IF COL_LENGTH('dbo.[Order]', 'ShippingFee') IS NULL
    ALTER TABLE dbo.[Order] ADD ShippingFee DECIMAL(18,2) NOT NULL
        CONSTRAINT DF_Order_ShippingFee DEFAULT (25000) WITH VALUES;
GO
