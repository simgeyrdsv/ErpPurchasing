/* =====================================================================
   04_support_diagnostics.sql — Bölüm 4: Destek senaryosu tanı sorguları

   Bildirim: "100 adetlik satınalma siparişi oluşturduk. Ürün teslim alındı
              ancak stok ekranında yalnızca 60 adet görünüyor."

   Yöntem: Veriyi zincir boyunca halka halka takip et ve İLK KOPUKLUĞU bul:
           Sipariş → Mal Kabul → Stok Hareketi → Stok Bakiyesi (ekran)
   Tüm sorgular salt okunurdur (SELECT). Canlı sistemde veri DÜZELTMESİ
   asla doğrudan UPDATE/DELETE ile yapılmaz; onay veya ters kayıt ile yapılır.
   ===================================================================== */
USE ErpPurchasing;
GO

-- Kullanıcıdan alınan bilgiler (örnek: seed verideki benzer vaka)
DECLARE @OrderNumber NVARCHAR(20) = N'PO-2026-000002';
DECLARE @ItemCode    NVARCHAR(30) = N'HM-PRF-050';

/* ---------------------------------------------------------------------
   ADIM 1 — Sipariş satırı ve ona bağlı TÜM mal kabuller (taslaklar DAHİL)
   Soru: Sipariş kaç? Kaç mal kabul var? Her biri hangi durumda, hangi depoda?
   --------------------------------------------------------------------- */
SELECT  po.OrderNumber,
        pos.StatusName          AS OrderStatus,
        pol.PurchaseOrderLineId,
        pol.LineNumber,
        i.ItemCode,
        i.ItemName,
        u.UnitCode,
        pol.Quantity            AS OrderedQty,
        gr.ReceiptNumber,
        gr.DeliveryNoteNumber,
        gr.ReceiptDate,
        grs.StatusName          AS ReceiptStatus,
        w.WarehouseCode,
        grl.GoodsReceiptLineId,
        grl.Quantity            AS ReceiptQty
FROM dbo.PurchaseOrder po
JOIN dbo.PurchaseOrderStatus pos    ON pos.StatusId = po.StatusId
JOIN dbo.PurchaseOrderLine pol      ON pol.PurchaseOrderId = po.PurchaseOrderId
JOIN dbo.Item i                     ON i.ItemId = pol.ItemId
JOIN dbo.Unit u                     ON u.UnitId = i.UnitId
LEFT JOIN dbo.GoodsReceiptLine grl  ON grl.PurchaseOrderLineId = pol.PurchaseOrderLineId
LEFT JOIN dbo.GoodsReceipt gr       ON gr.GoodsReceiptId = grl.GoodsReceiptId
LEFT JOIN dbo.GoodsReceiptStatus grs ON grs.StatusId = gr.StatusId
LEFT JOIN dbo.Warehouse w           ON w.WarehouseId = grl.WarehouseId
WHERE po.OrderNumber = @OrderNumber
  AND i.ItemCode = @ItemCode
ORDER BY gr.ReceiptDate, gr.ReceiptNumber;

/* ---------------------------------------------------------------------
   ADIM 2 — Mal kabul satırı ↔ stok hareketi eşleşmesi (otomatik teşhis)
   Her mal kabul satırının stoğa doğru işlenip işlenmediğini kontrol eder.
   --------------------------------------------------------------------- */
SELECT  gr.ReceiptNumber,
        grs.StatusName              AS ReceiptStatus,
        grl.GoodsReceiptLineId,
        grl.Quantity                AS ReceiptQty,
        wr.WarehouseCode            AS ReceiptWarehouse,
        stIn.StockTransactionId     AS InTransactionId,
        stIn.Quantity               AS StockInQty,
        ws.WarehouseCode            AS StockWarehouse,
        stRev.Quantity              AS ReversalQty,
        CASE
            WHEN gr.StatusId = 1                         THEN N'TASLAK: Onaylanmamış, stoğa işlenmedi'
            WHEN stIn.StockTransactionId IS NULL         THEN N'HATA: Onaylı ama stok hareketi YOK'
            WHEN stIn.Quantity <> grl.Quantity           THEN N'HATA: Miktar farkı'
            WHEN stIn.WarehouseId <> grl.WarehouseId     THEN N'HATA: Depo farkı'
            WHEN stRev.StockTransactionId IS NOT NULL    THEN N'BİLGİ: Mal kabul iptal edilmiş (ters kayıt)'
            ELSE N'OK'
        END                         AS Diagnosis
FROM dbo.PurchaseOrder po
JOIN dbo.PurchaseOrderLine pol      ON pol.PurchaseOrderId = po.PurchaseOrderId
JOIN dbo.Item i                     ON i.ItemId = pol.ItemId
JOIN dbo.GoodsReceiptLine grl       ON grl.PurchaseOrderLineId = pol.PurchaseOrderLineId
JOIN dbo.GoodsReceipt gr            ON gr.GoodsReceiptId = grl.GoodsReceiptId
JOIN dbo.GoodsReceiptStatus grs     ON grs.StatusId = gr.StatusId
JOIN dbo.Warehouse wr               ON wr.WarehouseId = grl.WarehouseId
LEFT JOIN dbo.StockTransaction stIn ON stIn.GoodsReceiptLineId = grl.GoodsReceiptLineId
                                   AND stIn.TransactionTypeId = 1
LEFT JOIN dbo.Warehouse ws          ON ws.WarehouseId = stIn.WarehouseId
LEFT JOIN dbo.StockTransaction stRev ON stRev.GoodsReceiptLineId = grl.GoodsReceiptLineId
                                    AND stRev.TransactionTypeId = 5
WHERE po.OrderNumber = @OrderNumber
  AND i.ItemCode = @ItemCode
ORDER BY gr.ReceiptNumber;

/* ---------------------------------------------------------------------
   ADIM 3 — Ürünün DEPO bazında stoğu
   Soru: Eksik miktar başka bir depoda mı? (Kullanıcının ekranı depo filtreli olabilir.)
   --------------------------------------------------------------------- */
SELECT  w.WarehouseCode,
        w.WarehouseName,
        SUM(st.Quantity) AS Quantity
FROM dbo.StockTransaction st
JOIN dbo.Item i      ON i.ItemId = st.ItemId
JOIN dbo.Warehouse w ON w.WarehouseId = st.WarehouseId
WHERE i.ItemCode = @ItemCode
GROUP BY w.WarehouseCode, w.WarehouseName;

/* ---------------------------------------------------------------------
   ADIM 4 — Ürün hareket dökümü (ekstre) + yürüyen bakiye
   Soru: Mal kabulden sonra stoktan bir ÇIKIŞ olmuş mu? (Üretim, sayım, iptal)
   Window function ile her hareketten sonraki bakiye hesaplanır.
   --------------------------------------------------------------------- */
SELECT  w.WarehouseCode,
        st.TransactionDate,
        stt.TransactionTypeName,
        st.Quantity,
        SUM(st.Quantity) OVER (PARTITION BY st.WarehouseId
                               ORDER BY st.TransactionDate, st.StockTransactionId
                               ROWS UNBOUNDED PRECEDING) AS RunningBalance,
        st.Description,
        st.CreatedAt
FROM dbo.StockTransaction st
JOIN dbo.Item i                  ON i.ItemId = st.ItemId
JOIN dbo.Warehouse w             ON w.WarehouseId = st.WarehouseId
JOIN dbo.StockTransactionType stt ON stt.TransactionTypeId = st.TransactionTypeId
WHERE i.ItemCode = @ItemCode
ORDER BY w.WarehouseCode, st.TransactionDate, st.StockTransactionId;

/* ---------------------------------------------------------------------
   ADIM 5 — Benzer ürün kartı kontrolü
   Soru: Mal yanlışlıkla benzer kodlu/isimli BAŞKA bir ürün kartına mı girildi?
   --------------------------------------------------------------------- */
SELECT  i.ItemCode, i.ItemName, u.UnitCode, i.IsActive,
        ISNULL((SELECT SUM(Quantity) FROM dbo.StockTransaction WHERE ItemId = i.ItemId), 0) AS CurrentStock
FROM dbo.Item i
JOIN dbo.Unit u ON u.UnitId = i.UnitId
WHERE i.ItemName LIKE N'%' + (SELECT TOP (1) LEFT(ItemName, 7) FROM dbo.Item WHERE ItemCode = @ItemCode) + N'%';
GO

/* =====================================================================
   ETKİ ANALİZİ — Sorun tek sipariş mi, sistem geneli mi?
   ===================================================================== */

-- A) Sistem genelinde: ONAYLI olduğu halde stok hareketi OLMAYAN mal kabul satırları
--    (Sonuç dönerse bu bir yazılım/entegrasyon hatasıdır, kullanıcı hatası değil.)
SELECT  gr.ReceiptNumber, gr.ApprovedAt, grl.GoodsReceiptLineId, grl.Quantity
FROM dbo.GoodsReceiptLine grl
JOIN dbo.GoodsReceipt gr ON gr.GoodsReceiptId = grl.GoodsReceiptId
WHERE gr.StatusId = 2
  AND NOT EXISTS (SELECT 1 FROM dbo.StockTransaction st
                  WHERE st.GoodsReceiptLineId = grl.GoodsReceiptLineId
                    AND st.TransactionTypeId = 1);

-- B) Uzun süredir TASLAKTA bekleyen mal kabuller (süreç / izleme sorunu)
SELECT  gr.ReceiptNumber, s.SupplierName, gr.DeliveryNoteNumber, gr.ReceiptDate,
        DATEDIFF(DAY, gr.ReceiptDate, CAST(GETDATE() AS DATE)) AS WaitingDays
FROM dbo.GoodsReceipt gr
JOIN dbo.Supplier s ON s.SupplierId = gr.SupplierId
WHERE gr.StatusId = 1
  AND gr.ReceiptDate < CAST(GETDATE() AS DATE)
ORDER BY gr.ReceiptDate;

-- C) Sipariş durumu ile gerçek teslimat miktarları tutarsız olan siparişler
WITH Received AS (
    SELECT grl.PurchaseOrderLineId, SUM(grl.Quantity) AS Qty
    FROM dbo.GoodsReceiptLine grl
    JOIN dbo.GoodsReceipt gr ON gr.GoodsReceiptId = grl.GoodsReceiptId
    WHERE gr.StatusId = 2
    GROUP BY grl.PurchaseOrderLineId
), OrderState AS (
    SELECT  po.PurchaseOrderId, po.OrderNumber, po.StatusId,
            SUM(CASE WHEN pol.Quantity > ISNULL(r.Qty, 0) THEN 1 ELSE 0 END) AS OpenLines,
            SUM(ISNULL(r.Qty, 0)) AS TotalReceived
    FROM dbo.PurchaseOrder po
    JOIN dbo.PurchaseOrderLine pol ON pol.PurchaseOrderId = po.PurchaseOrderId
    LEFT JOIN Received r           ON r.PurchaseOrderLineId = pol.PurchaseOrderLineId
    GROUP BY po.PurchaseOrderId, po.OrderNumber, po.StatusId
)
SELECT *
FROM OrderState
WHERE (StatusId = 3 AND OpenLines > 0)                      -- "Tamamlandı" ama eksik satır var
   OR (StatusId IN (1, 2) AND OpenLines = 0)                -- Açık görünüyor ama her şey gelmiş
   OR (StatusId = 1 AND TotalReceived > 0);                 -- "Açık" ama teslimat başlamış
GO
