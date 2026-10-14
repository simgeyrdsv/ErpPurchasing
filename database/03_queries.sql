/* =====================================================================
   03_queries.sql — Bölüm 2: SQL Soruları
   ===================================================================== */
USE ErpPurchasing;
GO

/* ---------------------------------------------------------------------
   SORU 1: Ürün bazında güncel stok miktarı
   - Miktarlar işaretli tutulduğu için stok = hareketlerin toplamı.
   - LEFT JOIN: Hiç hareket görmemiş ürünler de 0 olarak listelenir.
   - Covering index: IX_StockTransaction_Item_Warehouse (ItemId, WarehouseId) INCLUDE (Quantity)
   --------------------------------------------------------------------- */
SELECT  i.ItemCode,
        i.ItemName,
        u.UnitCode,
        ISNULL(SUM(st.Quantity), 0) AS CurrentStock
FROM dbo.Item i
JOIN dbo.Unit u                   ON u.UnitId = i.UnitId
LEFT JOIN dbo.StockTransaction st ON st.ItemId = i.ItemId
WHERE i.IsActive = 1
GROUP BY i.ItemId, i.ItemCode, i.ItemName, u.UnitCode
ORDER BY i.ItemCode;
GO

/* ---------------------------------------------------------------------
   SORU 2: Son 30 gün içinde en fazla satın alınan ilk 10 ürün
   - Varsayım: "Satın alınan" = sipariş edilen (iptal edilenler hariç).
   - Sıralama TUTARA göre: ürünlerin birimleri farklı (adet, kg, rulo);
     miktarları birbiriyle karşılaştırmak anlamsız. Miktar da bilgi olarak gösterilir.
   - SARGable filtre: Sütun fonksiyona sokulmaz, OrderDate index'i kullanılabilir.
   --------------------------------------------------------------------- */
SELECT TOP (10)
        i.ItemCode,
        i.ItemName,
        u.UnitCode,
        SUM(pol.Quantity)                 AS TotalQuantity,
        SUM(pol.Quantity * pol.UnitPrice) AS TotalAmount
FROM dbo.PurchaseOrder po
JOIN dbo.PurchaseOrderLine pol ON pol.PurchaseOrderId = po.PurchaseOrderId
JOIN dbo.Item i                ON i.ItemId = pol.ItemId
JOIN dbo.Unit u                ON u.UnitId = i.UnitId
WHERE po.OrderDate >= DATEADD(DAY, -30, CAST(GETDATE() AS DATE))
  AND po.StatusId <> 4                     -- İptal hariç
GROUP BY i.ItemId, i.ItemCode, i.ItemName, u.UnitCode
ORDER BY TotalAmount DESC;
GO

/* ---------------------------------------------------------------------
   SORU 3: Henüz tamamen teslim edilmemiş satınalma siparişleri
   - Önce CTE'de sipariş satırı bazında teslim alınan TOPLANIR, sonra JOIN edilir.
     Doğrudan JOIN, bir satırın birden fazla mal kabulü olduğunda satırları
     çoğaltır (fan-out) ve sipariş miktarını iki kez sayar.
   - Sadece ONAYLI mal kabuller sayılır (taslak henüz stoğa girmemiştir).
   - İptal (4) ve Kapatıldı (5) siparişler hariç tutulur.
   --------------------------------------------------------------------- */
WITH Received AS (
    SELECT  grl.PurchaseOrderLineId,
            SUM(grl.Quantity) AS ReceivedQty
    FROM dbo.GoodsReceiptLine grl
    JOIN dbo.GoodsReceipt gr ON gr.GoodsReceiptId = grl.GoodsReceiptId
    WHERE gr.StatusId = 2
    GROUP BY grl.PurchaseOrderLineId
)
SELECT  po.OrderNumber,
        s.SupplierName,
        pol.LineNumber,
        i.ItemName,
        pol.Quantity                              AS OrderedQty,
        ISNULL(r.ReceivedQty, 0)                  AS ReceivedQty,
        pol.Quantity - ISNULL(r.ReceivedQty, 0)   AS RemainingQty,
        pol.ExpectedDeliveryDate,
        CASE WHEN pol.ExpectedDeliveryDate < CAST(GETDATE() AS DATE)
             THEN 1 ELSE 0 END                    AS IsOverdue      -- Gecikmiş mi?
FROM dbo.PurchaseOrder po
JOIN dbo.Supplier s            ON s.SupplierId = po.SupplierId
JOIN dbo.PurchaseOrderLine pol ON pol.PurchaseOrderId = po.PurchaseOrderId
JOIN dbo.Item i                ON i.ItemId = pol.ItemId
LEFT JOIN Received r           ON r.PurchaseOrderLineId = pol.PurchaseOrderLineId
WHERE po.StatusId NOT IN (4, 5)
  AND pol.Quantity > ISNULL(r.ReceivedQty, 0)
ORDER BY po.OrderNumber, pol.LineNumber;
GO

/* ---------------------------------------------------------------------
   SORU 4: Son 90 gün içinde hiç hareket görmeyen ürünler
   - Her ürünün SON hareket tarihine bakılır:
     NULL (hiç hareket yok) veya 90 günden eski → hareketsiz.
   - MAX() bir toplama fonksiyonu olduğu için koşul HAVING içindedir.
   --------------------------------------------------------------------- */
SELECT  i.ItemCode,
        i.ItemName,
        it.ItemTypeName,
        MAX(st.TransactionDate) AS LastMovementDate   -- NULL = hiç hareket görmemiş
FROM dbo.Item i
JOIN dbo.ItemType it              ON it.ItemTypeId = i.ItemTypeId
LEFT JOIN dbo.StockTransaction st ON st.ItemId = i.ItemId
WHERE i.IsActive = 1
GROUP BY i.ItemId, i.ItemCode, i.ItemName, it.ItemTypeName
HAVING MAX(st.TransactionDate) IS NULL
    OR MAX(st.TransactionDate) < DATEADD(DAY, -90, CAST(GETDATE() AS DATE))
ORDER BY LastMovementDate;
GO
