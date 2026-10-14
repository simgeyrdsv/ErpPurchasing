/* =====================================================================
   02_seed_data.sql — Örnek veri (Sakarya'da bir su dolum fabrikası)

   Veriler her SQL sorusunu test edecek şekilde kurgulanmıştır:
   - PO1: 5 L preform, ~115 gün önce teslim → Soru 4 (90 gün hareketsiz)
   - PO2: 10.000 preformun 6.000'i geldi, kalan 4.000'in mal kabulü TASLAK
          → Soru 3 + Bölüm 4 destek senaryosunun birebir benzeri
   - PO3: Koli MAMUL deposuna, streç film HAMMADDE deposuna → satır bazlı depo
   - PO4: Aynı ürün (etiket) iki satırda, iki farklı teslim tarihi
   - PO5: İptal edilmiş sipariş → Soru 3 bunu listelememeli
   - Mamul ürünler hiç hareket görmemiş → Soru 4 "hiç hareket yok"

   Tarihler GETDATE()'e göre hesaplanır; script hangi gün çalışırsa
   çalışsın "son 30 / 90 gün" senaryoları geçerli kalır.
   Script BİR KEZ, 01_create_tables.sql'den hemen sonra çalıştırılmalıdır.
   ===================================================================== */
USE ErpPurchasing;
GO
SET XACT_ABORT ON;   -- Hata olursa tüm işlem geri alınır (Atomicity)
BEGIN TRANSACTION;

DECLARE @t DATE = CAST(GETDATE() AS DATE);

/* ---------- Birimler: 1=ADET, 2=KOLI, 3=KG, 4=RULO ---------- */
INSERT INTO dbo.Unit (UnitCode, UnitName) VALUES
(N'ADET', N'Adet'), (N'KOLI', N'Koli'), (N'KG', N'Kilogram'), (N'RULO', N'Rulo');

/* ---------- Depolar: 1=Hammadde, 2=Mamul ---------- */
INSERT INTO dbo.Warehouse (WarehouseCode, WarehouseName) VALUES
(N'HM-DEPO', N'Hammadde Deposu'), (N'MM-DEPO', N'Mamul Deposu');

/* ---------- Ürünler ---------- */
INSERT INTO dbo.Item (ItemCode, ItemName, ItemTypeId, UnitId, CriticalStockLevel) VALUES
(N'HM-PRF-050', N'Preform 0,5 L',        1, 1, 20000),   -- 1
(N'HM-KPK-001', N'Kapak',                1, 1, 20000),   -- 2
(N'HM-ETK-050', N'Etiket 0,5 L',         1, 4, 10),      -- 3
(N'HM-STR-001', N'Streç Film',           1, 3, 100),     -- 4
(N'HM-KOL-024', N'Koli (24''lü)',        1, 1, 1000),    -- 5
(N'HM-PRF-500', N'Preform 5 L',          1, 1, 2000),    -- 6
(N'MM-SU-050',  N'0,5 L Su (24''lü)',    2, 2, 500),     -- 7
(N'MM-SU-500',  N'5 L Su',               2, 1, 300);     -- 8

/* ---------- Tedarikçiler ---------- */
INSERT INTO dbo.Supplier (SupplierCode, SupplierName, TaxNumber, Phone, Email) VALUES
(N'TED-001', N'Plastik Ambalaj A.Ş.',    N'1234567890', N'0264 111 11 11', N'satis@plastikambalaj.com'),
(N'TED-002', N'Etiket Matbaacılık Ltd.', N'2345678901', N'0264 222 22 22', N'info@etiketmatbaa.com'),
(N'TED-003', N'Karton Kutu San. Tic.',   N'3456789012', N'0216 333 33 33', N'siparis@kartonkutu.com');

/* ---------- Satınalma siparişleri ---------- */
INSERT INTO dbo.PurchaseOrder (OrderNumber, SupplierId, OrderDate, StatusId, Description) VALUES
(N'PO-2026-000001', 1, DATEADD(DAY,-120,@t), 3, N'5 L preform alımı'),
(N'PO-2026-000002', 1, DATEADD(DAY, -20,@t), 2, N'0,5 L preform + kapak'),
(N'PO-2026-000003', 3, DATEADD(DAY, -10,@t), 3, N'Koli + streç film'),
(N'PO-2026-000004', 2, DATEADD(DAY,  -5,@t), 1, N'Etiket - iki parti teslimat'),
(N'PO-2026-000005', 1, DATEADD(DAY,  -3,@t), 4, N'Tedarikçi üretemedi, iptal');

INSERT INTO dbo.PurchaseOrderLine (PurchaseOrderId, LineNumber, ItemId, Quantity, UnitPrice, ExpectedDeliveryDate) VALUES
(1, 1, 6,  3000, 1.2000,   DATEADD(DAY,-115,@t)),   -- POL 1
(2, 1, 1, 10000, 0.4500,   DATEADD(DAY, -15,@t)),   -- POL 2
(2, 2, 2, 10000, 0.1200,   DATEADD(DAY, -15,@t)),   -- POL 3
(3, 1, 5,  5000, 2.5000,   DATEADD(DAY,  -8,@t)),   -- POL 4
(3, 2, 4,   250, 85.0000,  DATEADD(DAY,  -8,@t)),   -- POL 5
(4, 1, 3,    40, 350.0000, DATEADD(DAY,   5,@t)),   -- POL 6
(4, 2, 3,    40, 350.0000, DATEADD(DAY,  20,@t)),   -- POL 7 (aynı ürün, farklı tarih)
(5, 1, 2,  5000, 0.1200,   DATEADD(DAY,   7,@t));   -- POL 8 (iptal edilen sipariş)

/* ---------- Mal kabuller ---------- */
INSERT INTO dbo.GoodsReceipt (ReceiptNumber, SupplierId, DeliveryNoteNumber, ReceiptDate, StatusId, ApprovedAt) VALUES
(N'GR-2026-000001', 1, N'IRS-PA-1001', DATEADD(DAY,-115,@t), 2, DATEADD(DAY,-115,CAST(@t AS DATETIME2))),
(N'GR-2026-000002', 1, N'IRS-PA-1050', DATEADD(DAY, -15,@t), 2, DATEADD(DAY, -15,CAST(@t AS DATETIME2))),
(N'GR-2026-000003', 3, N'IRS-KK-0777', DATEADD(DAY,  -8,@t), 2, DATEADD(DAY,  -8,CAST(@t AS DATETIME2))),
(N'GR-2026-000004', 1, N'IRS-PA-1062', DATEADD(DAY,  -1,@t), 1, NULL);   -- TASLAK

INSERT INTO dbo.GoodsReceiptLine (GoodsReceiptId, LineNumber, PurchaseOrderLineId, WarehouseId, Quantity) VALUES
(1, 1, 1, 1,  3000),   -- GRL 1
(2, 1, 2, 1,  6000),   -- GRL 2: 10.000'in 6.000'i
(2, 2, 3, 1, 10000),   -- GRL 3
(3, 1, 4, 2,  5000),   -- GRL 4: Koli → MAMUL deposu
(3, 2, 5, 1,   250),   -- GRL 5
(4, 1, 2, 1,  4000);   -- GRL 6: Kalan 4.000 (TASLAK → stoğa girmedi)

/* ---------- Stok hareketleri (sadece ONAYLI mal kabuller + üretim/sayım) ---------- */
INSERT INTO dbo.StockTransaction (TransactionTypeId, ItemId, WarehouseId, Quantity, TransactionDate, GoodsReceiptLineId, Description) VALUES
(1, 6, 1,  3000, DATEADD(DAY,-115,CAST(@t AS DATETIME2)), 1,    N'GR-2026-000001'),
(2, 6, 1, -1000, DATEADD(DAY,-100,CAST(@t AS DATETIME2)), NULL, N'Üretime çıkış'),
(1, 1, 1,  6000, DATEADD(DAY, -15,CAST(@t AS DATETIME2)), 2,    N'GR-2026-000002'),
(1, 2, 1, 10000, DATEADD(DAY, -15,CAST(@t AS DATETIME2)), 3,    N'GR-2026-000002'),
(2, 1, 1, -2000, DATEADD(DAY, -10,CAST(@t AS DATETIME2)), NULL, N'Üretime çıkış'),
(2, 2, 1, -2000, DATEADD(DAY, -10,CAST(@t AS DATETIME2)), NULL, N'Üretime çıkış'),
(1, 5, 2,  5000, DATEADD(DAY,  -8,CAST(@t AS DATETIME2)), 4,    N'GR-2026-000003'),
(1, 4, 1,   250, DATEADD(DAY,  -8,CAST(@t AS DATETIME2)), 5,    N'GR-2026-000003'),
(4, 4, 1,    -5, DATEADD(DAY,  -2,CAST(@t AS DATETIME2)), NULL, N'Sayım eksiği');

/* ---------- Belge numarası sayaçlarını örnek verinin devamına ayarla ---------- */
ALTER SEQUENCE dbo.PurchaseOrderNumberSeq RESTART WITH 6;
ALTER SEQUENCE dbo.GoodsReceiptNumberSeq  RESTART WITH 5;

COMMIT TRANSACTION;
GO
