# Bölüm 5 — Yetkilendirme ve Güvenlik

## 1. Hangi rol hangi işlemleri yapabilmelidir?

**Temel ilkeler:**
- **En az yetki (Least Privilege):** Her rol sadece işi için gereken yetkiye sahiptir.
- **Görevler ayrılığı (Segregation of Duties — SoD):** Bir satınalma sürecinin tamamı tek bir kişi tarafından yürütülemez. Siparişi açan kişi aynı malın kabulünü yapamaz. Aksi halde sahte tedarikçi → sahte sipariş → sahte mal kabul zinciriyle **suiistimal** mümkün olur.

### Rol — yetki matrisi

✅ Tam yetki · 👁️ Sadece görüntüleme · ❌ Yetki yok

| İşlem | Satınalma Uzmanı | Depo Personeli | Üretim Planlama | Sistem Yöneticisi |
|---|:---:|:---:|:---:|:---:|
| **Ürün kartı** oluştur / düzenle | ✅ | ❌ | ❌ | ✅ |
| Ürün kartı görüntüle | 👁️ | 👁️ | 👁️ | 👁️ |
| **Tedarikçi** oluştur / düzenle | ✅ | ❌ | ❌ | ❌ ¹ |
| Tedarikçi görüntüle | 👁️ | 👁️ ² | ❌ | 👁️ |
| **Satınalma siparişi** oluştur / düzenle | ✅ | ❌ | ❌ | ❌ |
| Sipariş iptal / kısa kapama | ✅ | ❌ | ❌ | ❌ |
| Sipariş görüntüle | 👁️ | 👁️ ³ | 👁️ | 👁️ |
| **Fiyat ve tutar** bilgisi görüntüle | 👁️ | ❌ ³ | ❌ | 👁️ |
| **Mal kabul** oluştur (taslak) | ❌ ⁴ | ✅ | ❌ | ❌ |
| Mal kabul **onayla** | ❌ ⁴ | ✅ | ❌ | ❌ |
| Mal kabul görüntüle | 👁️ | 👁️ | 👁️ | 👁️ |
| **Stok** görüntüle | 👁️ | 👁️ ⁵ | 👁️ | 👁️ |
| Üretime çıkış hareketi | ❌ | ✅ | ✅ ⁶ | ❌ |
| Sayım farkı girişi | ❌ | ✅ ⁷ | ❌ | ❌ |
| Raporlar / dashboard | 👁️ | 👁️ (stok) | 👁️ (stok, açık sipariş) | 👁️ |
| **Kullanıcı ve rol yönetimi** | ❌ | ❌ | ❌ | ✅ |
| Sistem parametreleri, birim/depo tanımları | ❌ | ❌ | ❌ | ✅ |
| Audit log görüntüle | ❌ | ❌ | ❌ | 👁️ |

**Notlar**
1. Tedarikçi açma bir **iş** yetkisidir. Sistem yöneticisinin her şeye yetkisi olması SoD açısından risklidir; yönetici teknik yetkilerle sınırlandırılır.
2. Depo personeli irsaliyedeki tedarikçiyi doğrulayabilmek için görür.
3. Depo personeli siparişin **miktarlarını** görür ama **fiyat bilgisini görmez** (alan seviyesinde yetki). Ticari bilgi gereksiz yere açılmaz.
4. **SoD:** Siparişi oluşturan rol mal kabul yapamaz.
5. Depo personeli sadece **yetkili olduğu depoların** stoğunu görür (veri seviyesinde yetki).
6. Üretim planlama üretim emrine bağlı malzeme çıkışını başlatabilir; fiziksel çıkışı depo onaylar.
7. Belirli bir eşiği aşan sayım farkları ikinci bir onay gerektirebilir (örn. depo şefi).

---

## 2. Yetkilendirme modelinin tasarımı

### Yaklaşım: RBAC + izin (permission) tabanlı + veri seviyesinde kapsam

Üç katmanlı bir model öneriyorum:

| Katman | Soru | Örnek |
|---|---|---|
| **1. Fonksiyon yetkisi** | Bu işlemi yapabilir mi? | `GoodsReceipt.Approve` |
| **2. Veri kapsamı** | Hangi kayıtlar üzerinde? | Sadece `HM-DEPO` deposu |
| **3. Alan yetkisi** | Kaydın hangi alanlarını görebilir? | `UnitPrice` gizli |

**Neden doğrudan rol kontrolü değil de izin (permission) tabanlı?**
Kodda `if (user.Role == "Depo")` yazmak yerine `if (user.Has("GoodsReceipt.Approve"))` yazılır. Rollerin yetkileri değiştiğinde (örn. "depo şefi" adında yeni bir rol eklendiğinde) **kod değişmez**, sadece veritabanındaki eşleşme değişir. Bu, SOLID'deki **Open/Closed** ilkesinin yetkilendirmedeki karşılığıdır.

### Veri modeli

```sql
-- Kullanıcılar (kimlik doğrulama: kurumsal AD / SSO önerilir; şifre tutulacaksa sadece hash)
CREATE TABLE dbo.AppUser (
    UserId       INT IDENTITY(1,1) PRIMARY KEY,
    UserName     NVARCHAR(50)  NOT NULL UNIQUE,
    FullName     NVARCHAR(100) NOT NULL,
    Email        NVARCHAR(100) NOT NULL UNIQUE,
    IsActive     BIT           NOT NULL DEFAULT (1),
    CreatedAt    DATETIME2(0)  NOT NULL DEFAULT (SYSDATETIME())
);

CREATE TABLE dbo.Role (
    RoleId       INT IDENTITY(1,1) PRIMARY KEY,
    RoleName     NVARCHAR(50) NOT NULL UNIQUE      -- Satınalma Uzmanı, Depo Personeli...
);

CREATE TABLE dbo.Permission (
    PermissionId   INT IDENTITY(1,1) PRIMARY KEY,
    PermissionCode NVARCHAR(100) NOT NULL UNIQUE,  -- 'PurchaseOrder.Create', 'GoodsReceipt.Approve', 'Price.View'
    Description    NVARCHAR(250) NULL
);

-- Çoka çok ilişkiler (ara tablolar)
CREATE TABLE dbo.UserRole (
    UserId INT NOT NULL REFERENCES dbo.AppUser (UserId),
    RoleId INT NOT NULL REFERENCES dbo.Role (RoleId),
    PRIMARY KEY (UserId, RoleId)
);

CREATE TABLE dbo.RolePermission (
    RoleId       INT NOT NULL REFERENCES dbo.Role (RoleId),
    PermissionId INT NOT NULL REFERENCES dbo.Permission (PermissionId),
    PRIMARY KEY (RoleId, PermissionId)
);

-- Veri seviyesinde kapsam: Kullanıcı hangi depolarda işlem yapabilir?
CREATE TABLE dbo.UserWarehouse (
    UserId      INT NOT NULL REFERENCES dbo.AppUser (UserId),
    WarehouseId INT NOT NULL REFERENCES dbo.Warehouse (WarehouseId),
    PRIMARY KEY (UserId, WarehouseId)
);

-- Denetim izi: Mevcut belge tablolarına "kim" bilgisi eklenir
ALTER TABLE dbo.PurchaseOrder ADD CreatedBy  INT NULL REFERENCES dbo.AppUser (UserId);
ALTER TABLE dbo.GoodsReceipt  ADD CreatedBy  INT NULL REFERENCES dbo.AppUser (UserId),
                                  ApprovedBy INT NULL REFERENCES dbo.AppUser (UserId);

-- Genel denetim kaydı (kritik işlemler)
CREATE TABLE dbo.AuditLog (
    AuditLogId  BIGINT IDENTITY(1,1) PRIMARY KEY,
    UserId      INT           NOT NULL,
    Action      NVARCHAR(50)  NOT NULL,     -- 'GoodsReceipt.Approve'
    EntityName  NVARCHAR(50)  NOT NULL,     -- 'GoodsReceipt'
    EntityId    NVARCHAR(50)  NOT NULL,
    OldValues   NVARCHAR(MAX) NULL,         -- JSON
    NewValues   NVARCHAR(MAX) NULL,         -- JSON
    IpAddress   NVARCHAR(45)  NULL,
    CreatedAt   DATETIME2(0)  NOT NULL DEFAULT (SYSDATETIME())
);
```

### API tarafında uygulama (ASP.NET Core)

**Kimlik doğrulama:** JWT (kurumsal ortamda Azure AD / Active Directory ile SSO). Token içinde kullanıcının izinleri ve depo kapsamı claim olarak taşınır.

**Fonksiyon yetkisi — policy tabanlı:**
```csharp
// Program.cs
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("GoodsReceipt.Approve",
        p => p.RequireClaim("permission", "GoodsReceipt.Approve"));
});

// Controller
[HttpPost("{id:int}/approve")]
[Authorize(Policy = "GoodsReceipt.Approve")]
public async Task<IActionResult> Approve(int id) => ...
```

**Veri kapsamı — service katmanında:**
```csharp
// GoodsReceiptService.CreateAsync içinde
var allowed = _currentUser.WarehouseIds;
if (request.Lines.Any(l => !allowed.Contains(l.WarehouseId)))
    throw new ForbiddenException("Bu depoda işlem yetkiniz yok.");   // → 403
```

**Görevler ayrılığı (SoD) — service katmanında:**
```csharp
if (orderLines.Any(ol => ol.CreatedBy == _currentUser.UserId))
    throw new ForbiddenException("Kendi oluşturduğunuz siparişe mal kabul yapamazsınız.");
```

> Mevcut koddaki hazırlık: `AppException` tabanlı hata yapısı sayesinde yeni bir `ForbiddenException` (403) eklemek middleware'de **hiçbir değişiklik gerektirmez** (Open/Closed). `StockService` de depo kapsamı filtresinin ekleneceği yer olarak bırakılmıştır.

---

## 3. Dikkate alınması gereken güvenlik riskleri

OWASP Top 10 ile eşleştirilmiştir. **"Mevcut durum"** sütunu bu projede hâlihazırda uygulanan önlemleri gösterir.

| # | Risk | Senaryo | Önlem | Mevcut durum |
|---|---|---|---|---|
| 1 | **Yetki aşımı / IDOR** (OWASP A01) | Depo personeli URL'deki id'yi değiştirip başka deponun verisini görür | Fonksiyon + veri kapsamı kontrolü **sunucuda** (istemciye güvenilmez) | 📋 Tasarlandı |
| 2 | **Görevler ayrılığı ihlali / suiistimal** | Aynı kişi tedarikçi açar, sipariş verir, mal kabul yapar | SoD kuralları, `CreatedBy`/`ApprovedBy`, eşik üstü çift onay | 📋 Tasarlandı |
| 3 | **SQL Injection** (A03) | Kullanıcı girdisi SQL metnine eklenir | Tüm sorgular **parametreli** (Dapper `@Param`); string birleştirme yok | ✅ Uygulandı |
| 4 | **Hassas bilgi sızıntısı** (A04/A05) | Hata mesajında tablo adı, stack trace görünür | Global middleware: beklenmeyen hatada **genel mesaj**, detay sadece loga | ✅ Uygulandı |
| 5 | **Mass assignment / aşırı veri kabulü** | İstekte `statusId: 3` gönderip siparişi "Tamamlandı" yapmak | DTO'lar sadece izin verilen alanları içerir; durum sunucuda belirlenir | ✅ Uygulandı |
| 6 | **Geçersiz veri** | Negatif miktar, 0 fiyat, aşırı uzun metin | İki katmanlı doğrulama: DTO attribute'ları + DB `CHECK` kısıtları | ✅ Uygulandı |
| 7 | **Yarış durumu (race condition)** | İki eşzamanlı mal kabulle sipariş miktarını aşmak | Transaction + `UPDLOCK`, `UNIQUE` kısıtlar, `SEQUENCE` | ✅ Uygulandı |
| 8 | **Denetim izi eksikliği** (A09) | Stoğu kimin değiştirdiği bilinmez | Hareketler silinmez, ters kayıt; `AuditLog`, `CreatedBy`/`ApprovedBy` | 🟡 Kısmen (ledger ✅, kullanıcı bilgisi 📋) |
| 9 | **Zayıf kimlik doğrulama** (A07) | Paylaşılan hesaplar, zayıf şifre, kaba kuvvet saldırısı | SSO / AD, MFA, hesap kilitleme, kişiye özel hesap | 📋 Tasarlandı |
| 10 | **Gizli bilgilerin yönetimi** (A02) | Bağlantı cümlesi / şifre kaynak kodda | Windows Authentication (şifre yok); prod'da Key Vault / ortam değişkeni | ✅ Lokalde şifresiz |
| 11 | **Aşırı yetkili veritabanı kullanıcısı** | Uygulama `db_owner`/`sa` ile bağlanır; açık bulunursa DB tamamen ele geçer | Uygulamaya özel DB kullanıcısı: sadece gerekli tablolarda SELECT/INSERT/UPDATE, **DELETE yok**, DDL yok | 📋 Tasarlandı |
| 12 | **Aktarım güvenliği** | Ağda veri dinlenir | HTTPS zorunlu (`UseHttpsRedirection`), prod'da HSTS | ✅ Uygulandı |
| 13 | **Kötüye kullanım / DoS** | API'ye yoğun istek | Rate limiting (ASP.NET Core `AddRateLimiter`), sayfalama | 📋 Önerildi |
| 14 | **Pasif kullanıcı erişimi** | İşten ayrılan personelin hesabı açık kalır | `IsActive` kontrolü, periyodik yetki gözden geçirme | 📋 Tasarlandı |
| 15 | **Veri kaybı** | Yanlışlıkla silme, donanım arızası | Soft delete (`IsActive`), `ON DELETE CASCADE` yok, düzenli yedekleme | ✅ Kısmen uygulandı |

### Uygulamaya özel veritabanı kullanıcısı örneği
```sql
CREATE LOGIN erp_api_login WITH PASSWORD = '<güçlü-parola>';
CREATE USER  erp_api_user  FOR LOGIN erp_api_login;

GRANT SELECT ON SCHEMA::dbo TO erp_api_user;
GRANT INSERT, UPDATE ON dbo.Item, dbo.PurchaseOrder, dbo.PurchaseOrderLine,
                        dbo.GoodsReceipt, dbo.GoodsReceiptLine TO erp_api_user;
GRANT INSERT ON dbo.StockTransaction TO erp_api_user;           -- Stok hareketi: sadece INSERT
GRANT UPDATE ON SEQUENCE::dbo.PurchaseOrderNumberSeq TO erp_api_user;
GRANT UPDATE ON SEQUENCE::dbo.GoodsReceiptNumberSeq  TO erp_api_user;
-- DELETE ve DDL yetkisi VERİLMEZ. StockTransaction'da UPDATE de yok: defter değiştirilemez.
```
