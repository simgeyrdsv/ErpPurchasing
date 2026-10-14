/* =====================================================================
   ERP Satınalma & Stok Modülü
   01_create_tables.sql — Veritabanı şeması (SQL Server 2022)

   Tasarım özeti:
   - 3NF'e uygun tasarım. Türetilebilen veriler (stok miktarı, sipariş
     toplamı) SAKLANMAZ, hesaplanır.
   - Stok, StockTransaction tablosundaki işaretli hareketlerin toplamıdır
     (ledger / defter mantığı). Hareketler silinmez, ters kayıtla düzeltilir.
   - Durum ve tip değerleri lookup tablolarında tutulur (FK ile korunur).
   - Kısmi teslimat: GoodsReceiptLine -> PurchaseOrderLine (1:N).
   - Teslim tarihi ve depo SATIR seviyesindedir.
   ===================================================================== */

CREATE DATABASE ErpPurchasing;
GO
USE ErpPurchasing;
GO

/* =====================================================================
   BÖLÜM 1: KART (MASTER) TABLOLARI
   ===================================================================== */

-- Ölçü birimleri (kullanıcı tanımlayabilir → IDENTITY)
CREATE TABLE dbo.Unit (
    UnitId      INT IDENTITY(1,1) NOT NULL,
    UnitCode    NVARCHAR(10)      NOT NULL,
    UnitName    NVARCHAR(50)      NOT NULL,
    CONSTRAINT PK_Unit          PRIMARY KEY (UnitId),
    CONSTRAINT UQ_Unit_UnitCode UNIQUE (UnitCode)
);

-- Ürün tipi (sistem sabiti → IDENTITY yok, C# enum ile eşleşir)
CREATE TABLE dbo.ItemType (
    ItemTypeId   TINYINT       NOT NULL,
    ItemTypeName NVARCHAR(50)  NOT NULL,
    CONSTRAINT PK_ItemType PRIMARY KEY (ItemTypeId)
);
INSERT INTO dbo.ItemType (ItemTypeId, ItemTypeName) VALUES
(1, N'Hammadde'),
(2, N'Mamul');

-- Depolar
CREATE TABLE dbo.Warehouse (
    WarehouseId    INT IDENTITY(1,1) NOT NULL,
    WarehouseCode  NVARCHAR(20)      NOT NULL,
    WarehouseName  NVARCHAR(100)     NOT NULL,
    IsActive       BIT               NOT NULL CONSTRAINT DF_Warehouse_IsActive DEFAULT (1),
    CONSTRAINT PK_Warehouse               PRIMARY KEY (WarehouseId),
    CONSTRAINT UQ_Warehouse_WarehouseCode UNIQUE (WarehouseCode)
);

-- Ürün kartı (stok miktarı sütunu YOK: hareketlerden hesaplanır)
CREATE TABLE dbo.Item (
    ItemId              INT IDENTITY(1,1) NOT NULL,
    ItemCode            NVARCHAR(30)      NOT NULL,
    ItemName            NVARCHAR(150)     NOT NULL,
    ItemTypeId          TINYINT           NOT NULL,
    UnitId              INT               NOT NULL,
    CriticalStockLevel  DECIMAL(18,3)     NULL,
    IsActive            BIT               NOT NULL CONSTRAINT DF_Item_IsActive  DEFAULT (1),
    CreatedAt           DATETIME2(0)      NOT NULL CONSTRAINT DF_Item_CreatedAt DEFAULT (SYSDATETIME()),
    CONSTRAINT PK_Item          PRIMARY KEY (ItemId),
    CONSTRAINT UQ_Item_ItemCode UNIQUE (ItemCode),
    CONSTRAINT FK_Item_ItemType FOREIGN KEY (ItemTypeId) REFERENCES dbo.ItemType (ItemTypeId),
    CONSTRAINT FK_Item_Unit     FOREIGN KEY (UnitId)     REFERENCES dbo.Unit (UnitId),
    CONSTRAINT CK_Item_CriticalStockLevel CHECK (CriticalStockLevel IS NULL OR CriticalStockLevel >= 0)
);

-- Tedarikçi kartı
CREATE TABLE dbo.Supplier (
    SupplierId    INT IDENTITY(1,1) NOT NULL,
    SupplierCode  NVARCHAR(20)      NOT NULL,
    SupplierName  NVARCHAR(150)     NOT NULL,
    TaxNumber     NVARCHAR(11)      NOT NULL,   -- VKN (10) veya TCKN (11)
    Phone         NVARCHAR(20)      NULL,
    Email         NVARCHAR(100)     NULL,
    Address       NVARCHAR(250)     NULL,
    IsActive      BIT               NOT NULL CONSTRAINT DF_Supplier_IsActive  DEFAULT (1),
    CreatedAt     DATETIME2(0)      NOT NULL CONSTRAINT DF_Supplier_CreatedAt DEFAULT (SYSDATETIME()),
    CONSTRAINT PK_Supplier              PRIMARY KEY (SupplierId),
    CONSTRAINT UQ_Supplier_SupplierCode UNIQUE (SupplierCode),
    CONSTRAINT UQ_Supplier_TaxNumber    UNIQUE (TaxNumber),
    CONSTRAINT CK_Supplier_TaxNumber    CHECK (LEN(TaxNumber) IN (10, 11))
);
GO

/* =====================================================================
   BÖLÜM 2: SATINALMA SİPARİŞİ
   ===================================================================== */

CREATE TABLE dbo.PurchaseOrderStatus (
    StatusId    TINYINT      NOT NULL,
    StatusName  NVARCHAR(50) NOT NULL,
    CONSTRAINT PK_PurchaseOrderStatus PRIMARY KEY (StatusId)
);
INSERT INTO dbo.PurchaseOrderStatus (StatusId, StatusName) VALUES
(1, N'Açık'),
(2, N'Kısmi Teslim'),
(3, N'Tamamlandı'),
(4, N'İptal'),
(5, N'Kapatıldı');     -- Kısa kapama: kalan miktar artık beklenmiyor

-- Sipariş BAŞLIĞI (toplam tutar YOK: satırlardan hesaplanır)
CREATE TABLE dbo.PurchaseOrder (
    PurchaseOrderId  INT IDENTITY(1,1) NOT NULL,
    OrderNumber      NVARCHAR(20)      NOT NULL,   -- PO-2026-000001
    SupplierId       INT               NOT NULL,
    OrderDate        DATE              NOT NULL,
    StatusId         TINYINT           NOT NULL CONSTRAINT DF_PurchaseOrder_StatusId  DEFAULT (1),
    Description      NVARCHAR(500)     NULL,
    CreatedAt        DATETIME2(0)      NOT NULL CONSTRAINT DF_PurchaseOrder_CreatedAt DEFAULT (SYSDATETIME()),
    CONSTRAINT PK_PurchaseOrder             PRIMARY KEY (PurchaseOrderId),
    CONSTRAINT UQ_PurchaseOrder_OrderNumber UNIQUE (OrderNumber),
    CONSTRAINT FK_PurchaseOrder_Supplier    FOREIGN KEY (SupplierId) REFERENCES dbo.Supplier (SupplierId),
    CONSTRAINT FK_PurchaseOrder_Status      FOREIGN KEY (StatusId)   REFERENCES dbo.PurchaseOrderStatus (StatusId)
);

-- Sipariş SATIRLARI
-- Birim YOK (ürün kartından gelir, 3NF). Birim fiyat VAR (siparişin o anki fiyatı).
-- Teslim tarihi satırda: aynı ürün farklı tarihlerde planlanabilir (planlı bölünmüş teslimat).
CREATE TABLE dbo.PurchaseOrderLine (
    PurchaseOrderLineId   INT IDENTITY(1,1) NOT NULL,
    PurchaseOrderId       INT               NOT NULL,
    LineNumber            SMALLINT          NOT NULL,
    ItemId                INT               NOT NULL,
    Quantity              DECIMAL(18,3)     NOT NULL,
    UnitPrice             DECIMAL(18,4)     NOT NULL,
    ExpectedDeliveryDate  DATE              NULL,
    CONSTRAINT PK_PurchaseOrderLine            PRIMARY KEY (PurchaseOrderLineId),
    CONSTRAINT UQ_PurchaseOrderLine_LineNumber UNIQUE (PurchaseOrderId, LineNumber),
    CONSTRAINT FK_PurchaseOrderLine_Order      FOREIGN KEY (PurchaseOrderId) REFERENCES dbo.PurchaseOrder (PurchaseOrderId),
    CONSTRAINT FK_PurchaseOrderLine_Item       FOREIGN KEY (ItemId)          REFERENCES dbo.Item (ItemId),
    CONSTRAINT CK_PurchaseOrderLine_Quantity   CHECK (Quantity > 0),
    CONSTRAINT CK_PurchaseOrderLine_UnitPrice  CHECK (UnitPrice >= 0)
);

CREATE INDEX IX_PurchaseOrder_SupplierId ON dbo.PurchaseOrder (SupplierId);
CREATE INDEX IX_PurchaseOrder_OrderDate  ON dbo.PurchaseOrder (OrderDate);
CREATE INDEX IX_PurchaseOrderLine_ItemId ON dbo.PurchaseOrderLine (ItemId);
GO

/* =====================================================================
   BÖLÜM 3: MAL KABUL & STOK
   ===================================================================== */

CREATE TABLE dbo.GoodsReceiptStatus (
    StatusId    TINYINT      NOT NULL,
    StatusName  NVARCHAR(50) NOT NULL,
    CONSTRAINT PK_GoodsReceiptStatus PRIMARY KEY (StatusId)
);
INSERT INTO dbo.GoodsReceiptStatus (StatusId, StatusName) VALUES
(1, N'Taslak'),
(2, N'Onaylandı');

-- Mal kabul BAŞLIĞI
-- PurchaseOrderId YOK: bir kamyon birden fazla siparişin malını getirebilir (bağ satırda).
-- SupplierId VAR: irsaliye tedarikçiye aittir; aynı irsaliye iki kez girilemez.
CREATE TABLE dbo.GoodsReceipt (
    GoodsReceiptId      INT IDENTITY(1,1) NOT NULL,
    ReceiptNumber       NVARCHAR(20)      NOT NULL,   -- GR-2026-000001
    SupplierId          INT               NOT NULL,
    DeliveryNoteNumber  NVARCHAR(30)      NOT NULL,   -- Tedarikçi irsaliye no
    ReceiptDate         DATE              NOT NULL,
    StatusId            TINYINT           NOT NULL CONSTRAINT DF_GoodsReceipt_StatusId  DEFAULT (1),
    ApprovedAt          DATETIME2(0)      NULL,
    Description         NVARCHAR(500)     NULL,
    CreatedAt           DATETIME2(0)      NOT NULL CONSTRAINT DF_GoodsReceipt_CreatedAt DEFAULT (SYSDATETIME()),
    CONSTRAINT PK_GoodsReceipt               PRIMARY KEY (GoodsReceiptId),
    CONSTRAINT UQ_GoodsReceipt_ReceiptNumber UNIQUE (ReceiptNumber),
    CONSTRAINT UQ_GoodsReceipt_DeliveryNote  UNIQUE (SupplierId, DeliveryNoteNumber),
    CONSTRAINT FK_GoodsReceipt_Supplier      FOREIGN KEY (SupplierId) REFERENCES dbo.Supplier (SupplierId),
    CONSTRAINT FK_GoodsReceipt_Status        FOREIGN KEY (StatusId)   REFERENCES dbo.GoodsReceiptStatus (StatusId),
    CONSTRAINT CK_GoodsReceipt_Approval CHECK (
        (StatusId = 1 AND ApprovedAt IS NULL) OR
        (StatusId = 2 AND ApprovedAt IS NOT NULL)
    )
);

-- Mal kabul SATIRLARI
-- ItemId YOK: PurchaseOrderLine üzerinden bulunur (3NF, geçişli bağımlılık yok).
-- WarehouseId satırda: aynı kamyondaki mallar farklı depolara girebilir.
CREATE TABLE dbo.GoodsReceiptLine (
    GoodsReceiptLineId   INT IDENTITY(1,1) NOT NULL,
    GoodsReceiptId       INT               NOT NULL,
    LineNumber           SMALLINT          NOT NULL,
    PurchaseOrderLineId  INT               NOT NULL,
    WarehouseId          INT               NOT NULL,
    Quantity             DECIMAL(18,3)     NOT NULL,
    CONSTRAINT PK_GoodsReceiptLine            PRIMARY KEY (GoodsReceiptLineId),
    CONSTRAINT UQ_GoodsReceiptLine_LineNumber UNIQUE (GoodsReceiptId, LineNumber),
    CONSTRAINT FK_GoodsReceiptLine_Receipt    FOREIGN KEY (GoodsReceiptId)      REFERENCES dbo.GoodsReceipt (GoodsReceiptId),
    CONSTRAINT FK_GoodsReceiptLine_OrderLine  FOREIGN KEY (PurchaseOrderLineId) REFERENCES dbo.PurchaseOrderLine (PurchaseOrderLineId),
    CONSTRAINT FK_GoodsReceiptLine_Warehouse  FOREIGN KEY (WarehouseId)         REFERENCES dbo.Warehouse (WarehouseId),
    CONSTRAINT CK_GoodsReceiptLine_Quantity   CHECK (Quantity > 0)
);

CREATE TABLE dbo.StockTransactionType (
    TransactionTypeId    TINYINT      NOT NULL,
    TransactionTypeName  NVARCHAR(50) NOT NULL,
    Direction            SMALLINT     NOT NULL,   -- +1 giriş, -1 çıkış
    CONSTRAINT PK_StockTransactionType           PRIMARY KEY (TransactionTypeId),
    CONSTRAINT CK_StockTransactionType_Direction CHECK (Direction IN (1, -1))
);
INSERT INTO dbo.StockTransactionType (TransactionTypeId, TransactionTypeName, Direction) VALUES
(1, N'Mal Kabul Girişi',  1),
(2, N'Üretime Çıkış',    -1),
(3, N'Sayım Fazlası',     1),
(4, N'Sayım Eksiği',     -1),
(5, N'Mal Kabul İptali', -1);   -- Ters kayıt

-- Stok hareketleri (LEDGER)
-- ItemId / WarehouseId burada BİLİNÇLİ olarak tekrar tutulur: üretim ve sayım
-- hareketlerinin bağlı olduğu bir mal kabul yoktur; defter kendi kendine yeterli olmalıdır.
CREATE TABLE dbo.StockTransaction (
    StockTransactionId  BIGINT IDENTITY(1,1) NOT NULL,
    TransactionTypeId   TINYINT              NOT NULL,
    ItemId              INT                  NOT NULL,
    WarehouseId         INT                  NOT NULL,
    Quantity            DECIMAL(18,3)        NOT NULL,   -- İşaretli: +giriş / -çıkış
    TransactionDate     DATETIME2(0)         NOT NULL,
    GoodsReceiptLineId  INT                  NULL,       -- Kaynak belge
    Description         NVARCHAR(500)        NULL,
    CreatedAt           DATETIME2(0)         NOT NULL CONSTRAINT DF_StockTransaction_CreatedAt DEFAULT (SYSDATETIME()),
    CONSTRAINT PK_StockTransaction             PRIMARY KEY (StockTransactionId),
    CONSTRAINT FK_StockTransaction_Type        FOREIGN KEY (TransactionTypeId)  REFERENCES dbo.StockTransactionType (TransactionTypeId),
    CONSTRAINT FK_StockTransaction_Item        FOREIGN KEY (ItemId)             REFERENCES dbo.Item (ItemId),
    CONSTRAINT FK_StockTransaction_Warehouse   FOREIGN KEY (WarehouseId)        REFERENCES dbo.Warehouse (WarehouseId),
    CONSTRAINT FK_StockTransaction_ReceiptLine FOREIGN KEY (GoodsReceiptLineId) REFERENCES dbo.GoodsReceiptLine (GoodsReceiptLineId),
    CONSTRAINT CK_StockTransaction_Quantity    CHECK (Quantity <> 0),
    CONSTRAINT CK_StockTransaction_Source CHECK (
        (TransactionTypeId IN (1, 5) AND GoodsReceiptLineId IS NOT NULL) OR
        (TransactionTypeId NOT IN (1, 5) AND GoodsReceiptLineId IS NULL)
    )
);

-- Aynı mal kabul satırı stoğa İKİ KEZ işlenemez (iptal kaydı ayrı tip olduğu için izinli)
CREATE UNIQUE INDEX UX_StockTransaction_ReceiptLine_Type
    ON dbo.StockTransaction (GoodsReceiptLineId, TransactionTypeId)
    WHERE GoodsReceiptLineId IS NOT NULL;

-- Güncel stok sorgusu (covering index)
CREATE INDEX IX_StockTransaction_Item_Warehouse
    ON dbo.StockTransaction (ItemId, WarehouseId) INCLUDE (Quantity);

-- Tarih filtreli sorgular (hareketsiz ürünler, günlük hareket KPI'ı)
CREATE INDEX IX_StockTransaction_Date
    ON dbo.StockTransaction (TransactionDate) INCLUDE (ItemId);

-- Sipariş satırına göre teslim alınan toplam (eksik teslimat sorgusu)
CREATE INDEX IX_GoodsReceiptLine_OrderLine
    ON dbo.GoodsReceiptLine (PurchaseOrderLineId) INCLUDE (Quantity);

CREATE INDEX IX_GoodsReceipt_ReceiptDate ON dbo.GoodsReceipt (ReceiptDate);
GO

/* =====================================================================
   BÖLÜM 4: BELGE NUMARASI SAYAÇLARI
   SEQUENCE eşzamanlı isteklerde de benzersiz numara üretir (MAX+1 yarış
   durumuna açıktır). Rollback durumunda numara boşluğu oluşabilir; iç
   belgeler için kabul edilebilir.
   ===================================================================== */
CREATE SEQUENCE dbo.PurchaseOrderNumberSeq AS INT START WITH 1 INCREMENT BY 1;
CREATE SEQUENCE dbo.GoodsReceiptNumberSeq  AS INT START WITH 1 INCREMENT BY 1;
GO
