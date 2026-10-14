/* =====================================================================
   05_dashboard_views.sql — Bonus: Yönetim Dashboard'u KPI view'ları
   Power BI veya herhangi bir raporlama aracı bu view'lara doğrudan bağlanabilir.
   ===================================================================== */
USE ErpPurchasing;
GO

/* ---------------------------------------------------------------------
   KPI 1 — Toplam stok değeri
   Varsayım: Değerleme yöntemi = AĞIRLIKLI ORTALAMA alış maliyeti
   (onaylı mal kabullerin sipariş birim fiyatları üzerinden).
   --------------------------------------------------------------------- */
CREATE OR ALTER VIEW dbo.vw_StockValue AS
WITH AvgCost AS (
    SELECT  pol.ItemId,
            SUM(grl.Quantity * pol.UnitPrice) / NULLIF(SUM(grl.Quantity), 0) AS AvgUnitCost
    FROM dbo.GoodsReceiptLine grl
    JOIN dbo.GoodsReceipt gr       ON gr.GoodsReceiptId = grl.GoodsReceiptId AND gr.StatusId = 2
    JOIN dbo.PurchaseOrderLine pol ON pol.PurchaseOrderLineId = grl.PurchaseOrderLineId
    GROUP BY pol.ItemId
), Stock AS (
    SELECT ItemId, SUM(Quantity) AS Qty
    FROM dbo.StockTransaction
    GROUP BY ItemId
)
SELECT  i.ItemId, i.ItemCode, i.ItemName,
        s.Qty                                       AS CurrentStock,
        ISNULL(c.AvgUnitCost, 0)                    AS AvgUnitCost,
        s.Qty * ISNULL(c.AvgUnitCost, 0)            AS StockValue
FROM Stock s
JOIN dbo.Item i       ON i.ItemId = s.ItemId
LEFT JOIN AvgCost c   ON c.ItemId = s.ItemId
WHERE s.Qty <> 0;
GO
-- Kullanım: SELECT SUM(StockValue) AS TotalStockValue FROM dbo.vw_StockValue;

/* ---------------------------------------------------------------------
   KPI 2 — Kritik stok seviyesinin altındaki ürünler
   --------------------------------------------------------------------- */
CREATE OR ALTER VIEW dbo.vw_CriticalStock AS
SELECT  i.ItemId, i.ItemCode, i.ItemName, u.UnitCode,
        i.CriticalStockLevel,
        ISNULL(s.Qty, 0)                            AS CurrentStock,
        i.CriticalStockLevel - ISNULL(s.Qty, 0)     AS Shortage
FROM dbo.Item i
JOIN dbo.Unit u ON u.UnitId = i.UnitId
LEFT JOIN (SELECT ItemId, SUM(Quantity) AS Qty
           FROM dbo.StockTransaction GROUP BY ItemId) s ON s.ItemId = i.ItemId
WHERE i.IsActive = 1
  AND i.CriticalStockLevel IS NOT NULL
  AND ISNULL(s.Qty, 0) < i.CriticalStockLevel;
GO

/* ---------------------------------------------------------------------
   KPI 3 — Günlük stok hareketleri (son 30 gün, tip bazında)
   Not: Farklı birimler toplanamayacağı için ölçü HAREKET SAYISIDIR.
   --------------------------------------------------------------------- */
CREATE OR ALTER VIEW dbo.vw_DailyStockMovements AS
SELECT  CAST(st.TransactionDate AS DATE)    AS MovementDay,
        stt.TransactionTypeName,
        stt.Direction,
        COUNT(*)                            AS MovementCount
FROM dbo.StockTransaction st
JOIN dbo.StockTransactionType stt ON stt.TransactionTypeId = st.TransactionTypeId
WHERE st.TransactionDate >= DATEADD(DAY, -30, CAST(GETDATE() AS DATE))
GROUP BY CAST(st.TransactionDate AS DATE), stt.TransactionTypeName, stt.Direction;
GO

/* ---------------------------------------------------------------------
   KPI 4 — Açık satınalma siparişleri (açık tutar + gecikme bilgisi)
   --------------------------------------------------------------------- */
CREATE OR ALTER VIEW dbo.vw_OpenPurchaseOrders AS
WITH Received AS (
    SELECT grl.PurchaseOrderLineId, SUM(grl.Quantity) AS Qty
    FROM dbo.GoodsReceiptLine grl
    JOIN dbo.GoodsReceipt gr ON gr.GoodsReceiptId = grl.GoodsReceiptId AND gr.StatusId = 2
    GROUP BY grl.PurchaseOrderLineId
)
SELECT  po.PurchaseOrderId, po.OrderNumber, s.SupplierName, po.OrderDate, pos.StatusName,
        SUM((pol.Quantity - ISNULL(r.Qty, 0)) * pol.UnitPrice)              AS OpenAmount,
        MIN(CASE WHEN pol.Quantity > ISNULL(r.Qty, 0)
                 THEN pol.ExpectedDeliveryDate END)                         AS NextExpectedDate,
        CASE WHEN MIN(CASE WHEN pol.Quantity > ISNULL(r.Qty, 0)
                           THEN pol.ExpectedDeliveryDate END) < CAST(GETDATE() AS DATE)
             THEN 1 ELSE 0 END                                              AS IsOverdue
FROM dbo.PurchaseOrder po
JOIN dbo.Supplier s             ON s.SupplierId = po.SupplierId
JOIN dbo.PurchaseOrderStatus pos ON pos.StatusId = po.StatusId
JOIN dbo.PurchaseOrderLine pol  ON pol.PurchaseOrderId = po.PurchaseOrderId
LEFT JOIN Received r            ON r.PurchaseOrderLineId = pol.PurchaseOrderLineId
WHERE po.StatusId IN (1, 2)
GROUP BY po.PurchaseOrderId, po.OrderNumber, s.SupplierName, po.OrderDate, pos.StatusName;
GO

/* ---------------------------------------------------------------------
   KPI 5 — En çok alım yapılan tedarikçiler (son 12 ay, iptal hariç)
   --------------------------------------------------------------------- */
CREATE OR ALTER VIEW dbo.vw_TopSuppliers12M AS
SELECT  s.SupplierId, s.SupplierName,
        COUNT(DISTINCT po.PurchaseOrderId)      AS OrderCount,
        SUM(pol.Quantity * pol.UnitPrice)       AS TotalAmount
FROM dbo.PurchaseOrder po
JOIN dbo.Supplier s            ON s.SupplierId = po.SupplierId
JOIN dbo.PurchaseOrderLine pol ON pol.PurchaseOrderId = po.PurchaseOrderId
WHERE po.StatusId <> 4
  AND po.OrderDate >= DATEADD(MONTH, -12, CAST(GETDATE() AS DATE))
GROUP BY s.SupplierId, s.SupplierName;
GO

/* ---------------------------------------------------------------------
   KPI 6 — Son 12 aylık satınalma trendi (ay bazında)
   --------------------------------------------------------------------- */
CREATE OR ALTER VIEW dbo.vw_PurchaseTrend12M AS
SELECT  DATEFROMPARTS(YEAR(po.OrderDate), MONTH(po.OrderDate), 1)   AS MonthStart,
        COUNT(DISTINCT po.PurchaseOrderId)                          AS OrderCount,
        SUM(pol.Quantity * pol.UnitPrice)                           AS TotalAmount
FROM dbo.PurchaseOrder po
JOIN dbo.PurchaseOrderLine pol ON pol.PurchaseOrderId = po.PurchaseOrderId
WHERE po.StatusId <> 4
  AND po.OrderDate >= DATEADD(MONTH, -12, CAST(GETDATE() AS DATE))
GROUP BY DATEFROMPARTS(YEAR(po.OrderDate), MONTH(po.OrderDate), 1);
GO

/* ---------- Dashboard sorguları ---------- */
-- SELECT SUM(StockValue) AS TotalStockValue FROM dbo.vw_StockValue;
-- SELECT * FROM dbo.vw_CriticalStock ORDER BY Shortage DESC;
-- SELECT * FROM dbo.vw_DailyStockMovements ORDER BY MovementDay;
-- SELECT COUNT(*) AS OpenOrders, SUM(OpenAmount) AS OpenAmount, SUM(IsOverdue) AS OverdueOrders FROM dbo.vw_OpenPurchaseOrders;
-- SELECT TOP (5) * FROM dbo.vw_TopSuppliers12M ORDER BY TotalAmount DESC;
-- SELECT * FROM dbo.vw_PurchaseTrend12M ORDER BY MonthStart;
