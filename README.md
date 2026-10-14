# ERP Satınalma & Stok Modülü — Teknik Değerlendirme Ödevi

Bir su üretim şirketi için **satınalma ve stok yönetimi** modülü: veri modeli, SQL sorguları, REST API, destek senaryosu analizi, yetkilendirme tasarımı ve yönetim dashboard'u.

**Teknolojiler:** SQL Server 2022 · ASP.NET Core 8 Web API · Veritabanu bağlantısı için Dapper · REST API testleri için Swagger kullanıldı

---

## 📁 İçerik

| Bölüm | Konu | Dosya |
|---|---|---|
| 1 | Veri modeli | [`database/01_create_tables.sql`](database/01_create_tables.sql) · [`docs/erd.png`](docs/erd.png) |
| — | Örnek veri | [`database/02_seed_data.sql`](database/02_seed_data.sql) |
| 2 | SQL soruları | [`database/03_queries.sql`](database/03_queries.sql) |
| 3 | API tasarımı | [`docs/api.md`](docs/api.md) · [`ErpPurchasing.API`](ErpPurchasing.API) |
| 4 | Destek senaryosu | [`docs/support-analysis.md`](docs/support-analysis.md) · [`database/04_support_diagnostics.sql`](database/04_support_diagnostics.sql) |
| 5 | Yetkilendirme ve güvenlik | [`docs/security.md`](docs/security.md) |
| Bonus | Yönetim dashboard'u (Power BI) | [`docs/dashboard.png`](docs/dashboard.png) · [`docs/dashboard.md`](docs/dashboard.md) · [`database/05_dashboard_views.sql`](database/05_dashboard_views.sql) |

---

## 🚀 Kurulum

1. **Veritabanı** 
   ```
   database/01_create_tables.sql   → Şema
   database/02_seed_data.sql       → Örnek veri (bir kez)
   database/05_dashboard_views.sql → Dashboard view'ları (isteğe bağlı)
   ```
2. **Bağlantı** — `ErpPurchasing.API/appsettings.json` içindeki `ConnectionStrings:ErpDb` değerinde `Server=` kısmını kendi SQL Server adınızla değiştirin.
3. **Çalıştırma** — Visual Studio
4. **Test** — Tarayıcıda `https://localhost:<port>/swagger`

---

## 🏭 Senaryo ve varsayımlar

Bu çalışmada bir üründen yola çıkarak tasarıma karar verildi.Ürün olarak plastik şişe su örnek alındı. Hammadde olarak preform, kapak, etiket, streç film ve koli satın alıyor; mamul olarak 0,5 L ve 5 L şişe su üretiyor.

Aşağıdaki varsayımların her biri veritabanı tasarımını doğrudan etkiliyor.

### 1. Üretim modülü kapsam dışıdır
Case satınalma ve stok modülünü istiyor. Bu yüzden ürün reçetesi ("1 şişe su = 1 preform + 1 kapak + 1 etiket") ve iş emirleri tasarlanmadı. Ancak üretime verilen malzeme stoktan düştüğü için, bu işlem **"Üretime Çıkış"** adında bir stok hareket tipi olarak tutuluyor.
*Sistemdeki karşılığı:* `StockTransactionType` tablosunda 2 numaralı tip.

### 2. Birden fazla depo vardır
Gerçek fabrikalarda hammadde ve mamul ayrı depolarda tutulabilir. Bu yüzden iki depo tanımlandı: **Hammadde Deposu** ve **Mamul Deposu**. Aynı kamyondan gelen mallar farklı depolara girebileceği için depo bilgisi mal kabulün başlığında değil, **her satırında** tutuluyor.
*Sistemdeki karşılığı:* `Warehouse` tablosu, `GoodsReceiptLine.WarehouseId` ve `StockTransaction.WarehouseId` sütunları.
Destek senaryosunda (Bölüm 4) "eksik görünen miktar başka bir depoya girilmiş olabilir" ihtimali bu tasarım sayesinde incelenebiliyor.

### 3. Kısmi teslimat desteklenir
10.000 adet preform sipariş edildiğinde tedarikçi önce 6.000, bir hafta sonra kalan 4.000 adedi getirebilir. Yani **bir sipariş satırı birden fazla mal kabulle** karşılanabilir. Bunun için her mal kabul satırı, hangi sipariş satırının karşılığı olduğunu bilir.
*Sistemdeki karşılığı:* `GoodsReceiptLine.PurchaseOrderLineId` (sipariş satırı → mal kabul satırları: 1'e çok ilişki).
*Neden önemli:* SQL Soru 3 ("henüz tamamen teslim edilmemiş siparişler") ancak bu bağlantı sayesinde cevaplanabiliyor.

### 4. Aynı ürün bir siparişte birden fazla satırda yer alabilir
Kısmi teslimattan farklı olarak bu durum **önceden planlanır**: "Preformların 5.000'ini bu hafta, 5.000'ini gelecek ay getir." Bunun için her satırın kendi teslim tarihi olmalıdır. Bu nedenle beklenen teslim tarihi sipariş başlığında değil, **satırda** tutuluyor.
*Sistemdeki karşılığı:* `PurchaseOrderLine.ExpectedDeliveryDate`. Aynı siparişte aynı ürünü engelleyen bir kısıt **bilinçli olarak eklenmedi**.

### 5. Her ürün tek bir birimle takip edilir
Ürün ve birim farklı kavramlardır: **"0,5 L Su (24'lü)" bir üründür, birimi "Koli"dir.** "5 L Su" başka bir üründür, birimi "Adet"tir. Her ürünün tek bir birimi vardır; alış da stok da aynı birimle yapılır. "1 koli = 24 adet" gibi **birim dönüşümleri kapsam dışıdır**.
*Sistemdeki karşılığı:* `Unit` tablosu (Adet, Koli, Kg, Rulo) ve `Item.UnitId`. Sipariş satırında ayrıca birim tutulmuyor, çünkü birim ürün kartından geliyor.

### 6. Fazla teslimat kabul edilmez
10.000 adet sipariş edilen bir ürün için tedarikçi 10.200 getirirse sistem fazlasını kabul etmez; depocu 10.000 olarak mal kabul yapar, fazlası tedarikçiye iade edilir. Bazı ERP'lerde ürün bazında bir tolerans yüzdesi tanımlanır; bu, ileride eklenebilecek bir iyileştirme olarak bırakıldı.
*Sistemdeki karşılığı:* `GoodsReceiptValidator` sınıfı. Kontrol, eşzamanlı iki mal kabulün birlikte sınırı aşamaması için **kilit altında** (`UPDLOCK`) yapılıyor. Kural ihlalinde API **422** döndürüyor.

### 7. Mal kabul önce taslak, sonra onaylı olur
Depocu kamyondaki malları sayarken mal kabulü **Taslak** olarak kaydeder, sayım bitince **Onaylar**. **Stok hareketi sadece onay anında oluşur.** Böylece yarım kalmış bir sayım stoğu etkilemez.
*Sistemdeki karşılığı:* `GoodsReceiptStatus` tablosu (1 = Taslak, 2 = Onaylandı) ve `POST /api/goods-receipts/{id}/approve` endpoint'i.
*Neden önemli:* Bölüm 4'teki "100 sipariş, stokta 60 görünüyor" sorununun olası nedenlerinden biri, kalan 40 adedin mal kabulünün **taslakta kalmış** olmasıdır. Bu durum örnek veride canlı olarak kurgulandı.

### 8. Siparişin durumları vardır
Sipariş şu durumlardan birinde olur: **Açık → Kısmi Teslim → Tamamlandı**, ya da **İptal** / **Kapatıldı**.
"Kapatıldı" durumu **kısa kapama** içindir: 10.000 sipariş edildi, 9.990 geldi ve tedarikçi kalan 10 adedi göndermeyecek. Miktarlara bakıldığında 10 adet eksik görünse de iş açısından sipariş bitmiştir. Bu yüzden durum her zaman miktarlardan hesaplanamaz, **ayrı bir alanda saklanır**.
*Sistemdeki karşılığı:* `PurchaseOrderStatus` tablosu. Durumlar metin olarak değil, ayrı bir tabloda tutuluyor (normalizasyon: 'Açık' / 'Acik' gibi tutarsız girişler engelleniyor). İptal edilmiş veya kapatılmış siparişe mal kabul yapılamıyor.

### 9. Para birimi TL'dir
Tüm fiyat ve tutarlar Türk Lirası kabul edildi. Çoklu döviz ve kur yönetimi kapsam dışıdır.

### 10. Stok değeri ağırlıklı ortalama maliyetle hesaplanır
Dashboard'daki "Toplam stok değeri" için her ürünün **ortalama alış fiyatı** kullanıldı: Onaylı mal kabullerdeki miktarlar ve sipariş fiyatları üzerinden hesaplanıyor.
*Örnek:* 245 kg streç film × 85 TL = 20.825 TL.
*Sistemdeki karşılığı:* `vw_StockValue` view'ı. FIFO gibi farklı değerleme yöntemleri şirketin muhasebe politikasına göre seçilebilir.

---

## 🧱 Temel tasarım kararları

### Veri modeli
| Karar | Gerekçe |
|---|---|
| **Stok miktarı bir sütunda tutulmaz**, `StockTransaction` hareketlerinin toplamıdır (ledger) | Türetilmiş veri saklanmaz (normalizasyon). Bakiye ile hareketler arasında tutarsızlık **oluşamaz**. Her rakamın kaynak belgesi izlenebilir |
| Hareketler **silinmez/güncellenmez**, ters kayıtla düzeltilir böylece denetim izi korunur |
| Sipariş **toplam tutarı saklanmaz**, satırlardan hesaplanır | Normalizasyon |
| **Birim fiyat sipariş satırındadır**, ürün kartında değil | Fiyat zamanla değişir; siparişin verildiği anın bilgisidir |
| `GoodsReceiptLine` tablosunda **`ItemId` yoktur** | Ürün `PurchaseOrderLine` üzerinden bulunur. Tekrar yazmak 3NF ihlali (geçişli bağımlılık) olur |
| `StockTransaction` tablosunda `ItemId`/`WarehouseId` **vardır** (bilinçli tekrar) | Üretim/sayım hareketlerinin bağlı olduğu bir mal kabul yoktur; defter kendi kendine yeterli olmalıdır ve stok sorgusu birleştirme gerektirmemelidir |
| **Teslim tarihi ve depo satır seviyesindedir** | Aynı ürünün farklı tarihlerde teslimi; aynı kamyondaki malların farklı depolara girmesi |
| Mal kabul başlığında `PurchaseOrderId` **yok**, `SupplierId` **var** | Bir kamyon birden fazla siparişin malını getirebilir; irsaliye tedarikçiye aittir |
| Durumlar ve tipler **lookup tablolarında** (FK) | Metin olarak tutmak tutarsız veriye ('Acik' / 'Açık') açıktır. İsim tek yerde durur (3NF) |
| Surrogate key (`ItemId`) + benzersiz **iş anahtarı** (`ItemCode`) | Kullanıcı kodu değişebilir; ilişkiler sabit Id ile kurulur |
| `DECIMAL` miktar/fiyat, **`FLOAT` yok** | Kayan noktalı sayılarda yuvarlama hatası olur |
| `NVARCHAR` | Türkçe karakter desteği |
| `BIGINT` sadece `StockTransaction` için | En hızlı büyüyen tablo |
| Belge numaraları `SEQUENCE` ile | `MAX+1` eşzamanlı isteklerde aynı numarayı üretir |
| İsimlendirilmiş constraint'ler (`FK_Item_Unit`) | Hata mesajlarında sorunun yeri hemen anlaşılır |

### Veri bütünlüğü — çok katmanlı savunma
| Katman | Örnek |
|---|---|
| API (DTO doğrulama) | `[Required]`, `[Range]` → 400 |
| Service (iş kuralları) | Tedarikçi aktif mi, fazla teslimat var mı → 422 |
| Veritabanı (son savunma hattı) | `CHECK (Quantity > 0)`, `UNIQUE`, FK, filtered unique index |

### Uygulama mimarisi
```
Controllers  →  Application (Services, DTOs, Interfaces, Exceptions)  →  Infrastructure (Repositories, Data)
                              ↑ bağımlılıklar içe doğru ↑
```
- **Database-first** yaklaşım; veri erişiminde **Dapper**. SQL üzerinde tam kontrol (kilitler, transaction, performans).
- Tek proje içinde klasörlerle ayrılmış katmanlar. *Gerçek bir projede ayrı projelere (Domain / Application / Infrastructure / API) bölünür.*


### Mal kabulde transaction ve eşzamanlılık
Mal kabul,tek transaction içinde:
1. Aynı irsaliye daha önce girilmiş mi? (`UPDLOCK, HOLDLOCK`) → 409
2. Sipariş satırları **kilitlenir** (`UPDLOCK`)
3. Güncel teslim miktarları **kilit altında** okunur
4. İş kuralları doğrulanır (fazla teslimat, sipariş durumu, tedarikçi) → 422
5. Başlık + satırlar yazılır
6. Onay: Stok hareketleri oluşturulur, sipariş durumu güncellenir
7. COMMIT — herhangi bir hatada ROLLBACK

İki depocu aynı anda aynı sipariş satırına mal kabul yaparsa ikincisi birincinin işlemi bitene kadar bekler ve **güncel kalan miktarı** görür.

### Hata yönetimi
Global `ExceptionHandlingMiddleware`, tüm hataları **RFC 7807 ProblemDetails** formatına çevirir. Beklenmeyen hatalarda stack trace veya SQL detayı kullanıcıya **gösterilmez**, sadece loglanır.

---

## 🔌 API özeti

| Metot | Endpoint | Amaç |
|---|---|---|
| GET | `/api/items` | Ürün listesi |
| GET | `/api/items/{id}` | Ürün detayı |
| POST | `/api/items` | Ürün oluştur |
| POST | `/api/purchase-orders` | Sipariş oluştur |
| GET | `/api/purchase-orders/{id}` | Sipariş + teslimat durumu |
| POST | `/api/goods-receipts` | Mal kabul (+ onay + stok) |
| GET | `/api/goods-receipts/{id}` | Mal kabul detayı *(ek)* |
| POST | `/api/goods-receipts/{id}/approve` | Taslak mal kabul onayı *(ek)* |
| GET | `/api/stock?itemId=&warehouseId=` | Güncel stok (ürün × depo) |

Detaylar: [`docs/api.md`](docs/api.md)

---

## 🧪 Uçtan uca test senaryosu (Swagger)

| # | İstek | Beklenen |
|---|---|---|
| 1 | `GET /api/purchase-orders/2` | Preform: 10.000 sipariş, **6.000 teslim, 4.000 kalan** (taslak sayılmaz) |
| 2 | `POST /api/goods-receipts/4/approve` | 200 — Taslak onaylandı |
| 3 | `GET /api/purchase-orders/2` | Durum **Tamamlandı**, preform 10.000 / 10.000 |
| 4 | Aynı onay tekrar | 409 — Zaten onaylanmış |
| 5 | `POST /api/purchase-orders` (preform + kapak, 5.000'er) | 201 — `PO-2026-000006`, tutar 2.850 |
| 6 | `POST /api/goods-receipts` (PO6: preform 3.000, kapak 5.000) | 201 — PO6 **Kısmi Teslim** |
| 7 | Aynı irsaliye tekrar | 409 — İrsaliye daha önce girilmiş |
| 8 | PO6 preform için 3.000 daha (kalan 2.000) | 422 — Fazla teslimat |
| 9 | İptal edilmiş PO5'e mal kabul | 422 — Sipariş iptal durumunda |
| 10 | `GET /api/stock?itemId=1` | Preform 0,5 L: **11.000** |

> Adım 1-3, Bölüm 4'teki destek senaryosunun ("100 sipariş, stokta 60 görünüyor") sistem üzerinde canlı olarak gösterimidir.

---


## Yapay zeka kullanımı

Bu çalışmada Claude asistanını bir danışman ve programlama aracı olarak kullandım.

- **Tasarım:** Varsayımlar (çoklu depo, kısmi teslimat, birim yapısı, durum yönetimi) ve normalizasyon kararları soru-cevap şeklinde tartışılarak alındı. Örneğin teslim tarihinin başlık yerine satırda olması gerektiği, "aynı ürün bir siparişte iki satırda olabilir mi?" tartışması sırasında ortaya çıktı ve şema buna göre düzeltildi.
- **Kod üretimi:** SQL script'lerinin ve C# katmanlarının (DTO, repository, service, controller, middleware) taslakları asistan tarafından üretildi; her adımın gerekçesi açıklandı ve incelendi adım adım projeye eklendi.
- **Test ve doğrulama:** Tüm script'leri kendi ortamımda (SQL Server 2022 Express, Visual Studio 2022) çalıştırdım; her endpoint'i Swagger üzerinden başarılı ve hatalı senaryolarla test ettim. 
- **Destek senaryosu ve güvenlik (Bölüm 4-5):** Doküman taslakları asistan tarafından hazırlandı; ardından olası nedenler, kontrol adımları ve rol/yetki yapısı birlikte soru-cevap şeklinde gözden geçirildi.
- **Dashboard:** Power BI raporunu view'lara bağlanarak kendim oluşturdum.

Tasarım kararlarının gerekçelerini anlayarak ve tartışarak ilerledim; çalışmadaki kararların sorumluluğu bana aittir.
