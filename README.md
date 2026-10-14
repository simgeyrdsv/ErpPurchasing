# ERP Satınalma & Stok Modülü — Teknik Değerlendirme Ödevi

Bir üretim şirketi için **satınalma ve stok yönetimi** modülü: veri modeli, SQL sorguları, REST API, destek senaryosu analizi, yetkilendirme tasarımı ve yönetim dashboard'u.

**Teknolojiler:** SQL Server 2022 · ASP.NET Core 8 Web API · Dapper · Swagger

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

**Gereksinimler:** SQL Server 2019+ (Express yeterli), .NET 8 SDK, Visual Studio 2022 (17.8+) veya VS Code

1. **Veritabanı** — SSMS'te sırasıyla çalıştırın:
   ```
   database/01_create_tables.sql   → Şema
   database/02_seed_data.sql       → Örnek veri (bir kez)
   database/05_dashboard_views.sql → Dashboard view'ları (isteğe bağlı)
   ```
2. **Bağlantı** — `ErpPurchasing.API/appsettings.json` içindeki `ConnectionStrings:ErpDb` değerinde `Server=` kısmını kendi SQL Server adınızla değiştirin.
3. **Çalıştırma** — Visual Studio'da `F5` veya:
   ```
   cd ErpPurchasing.API
   dotnet run
   ```
4. **Test** — Tarayıcıda `https://localhost:<port>/swagger`

---

## 🏭 Senaryo ve varsayımlar

Sakarya'da bir **su dolum fabrikası**. Hammadde olarak preform, kapak, etiket, streç film ve koli satın alıyor; mamul olarak 0,5 L ve 5 L şişe su üretiyor.

| # | Varsayım | Gerekçe |
|---|---|---|
| 1 | **Üretim modülü (reçete, iş emri) kapsam dışıdır.** Üretime malzeme çıkışı yalnızca bir stok hareket tipi olarak tutulur | Ödevin kapsamı satınalma ve stok |
| 2 | **Çoklu depo** vardır: Hammadde Deposu ve Mamul Deposu | Gerçek fabrika yapısı; destek senaryosunda "başka depoya girmiş olabilir" ihtimali |
| 3 | **Kısmi teslimat** desteklenir: Bir sipariş satırı birden fazla mal kabulle karşılanabilir | SQL Soru 3'ün ön koşulu |
| 4 | Aynı ürün bir siparişte **birden fazla satırda** yer alabilir | Planlı bölünmüş teslimat (farklı teslim tarihleri) |
| 5 | Her ürün **tek bir birimle** takip edilir (Adet, Koli, Kg, Rulo). **Birim dönüşümü kapsam dışıdır** | Basitlik; ürün ve birim kavramları ayrıştırıldı ("24'lü koli" bir üründür, birimi "Koli"dir) |
| 6 | **Fazla teslimat kabul edilmez** (tolerans %0). Fazlası iade edilir | Tolerans yönetimi ileride eklenebilir |
| 7 | Mal kabulün **Taslak → Onaylandı** akışı vardır. **Stok hareketi sadece onayda oluşur** | Depocu sayımı bitirmeden stoğa işlenmez |
| 8 | Sipariş durumları: Açık · Kısmi Teslim · Tamamlandı · İptal · **Kapatıldı** (kısa kapama) | Durum her zaman miktarlardan türetilemez (örn. tedarikçi kalan 10 adedi göndermeyecek) |
| 9 | Para birimi **TL**; çoklu döviz kapsam dışıdır | — |
| 10 | Stok değerlemesi (dashboard) **ağırlıklı ortalama maliyet** ile yapılır | — |

---

## 🧱 Temel tasarım kararları

### Veri modeli
| Karar | Gerekçe |
|---|---|
| **Stok miktarı bir sütunda tutulmaz**, `StockTransaction` hareketlerinin toplamıdır (ledger) | Türetilmiş veri saklanmaz (normalizasyon). Bakiye ile hareketler arasında tutarsızlık **oluşamaz**. Her rakamın kaynak belgesi izlenebilir |
| Hareketler **silinmez/güncellenmez**, ters kayıtla düzeltilir | Muhasebe mantığı; denetim izi korunur |
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

### SOLID
| İlke | Uygulama |
|---|---|
| **S** — Single Responsibility | Controller sadece HTTP, Service iş kuralı, Repository SQL. Mal kabul kuralları ayrı bir `GoodsReceiptValidator` sınıfında |
| **O** — Open/Closed | Hata yönetimi: Yeni bir hata türü (örn. 403) için sadece yeni bir `AppException` alt sınıfı yazılır, middleware değişmez |
| **L** — Liskov Substitution | Tüm `AppException` alt sınıfları middleware'de atalarının yerine sorunsuz kullanılır; repository'ler arayüzleri üzerinden değiştirilebilir |
| **I** — Interface Segregation | Tek bir dev `IRepository` yerine modül bazlı küçük arayüzler (`IItemRepository`, `IStockRepository`...) |
| **D** — Dependency Inversion | Üst katmanlar somut sınıfları değil arayüzleri tanır (`IDbConnectionFactory`, `IGoodsReceiptValidator`). Bağımlılıkları DI container verir |

### Mal kabulde transaction ve eşzamanlılık
Mal kabul, modülün en kritik işlemidir. Tek transaction içinde:
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

## 🔭 İleride yapılabilecek iyileştirmeler

- **Kimlik doğrulama ve yetkilendirme:** JWT / SSO, policy tabanlı yetki, depo kapsamı, `CreatedBy`/`ApprovedBy` alanları ([`docs/security.md`](docs/security.md))
- **Mal kabul iptali** endpoint'i (ters kayıt: hareket tipi 5 şemada hazır)
- **Fazla teslimat toleransı** (ürün bazında yüzde)
- **Birim dönüşümü** (alış birimi ≠ stok birimi)
- **Unit testler:** `GoodsReceiptValidator` veritabanından bağımsız olduğu için doğrudan test edilebilir; repository'ler için Testcontainers ile entegrasyon testleri
- **Performans:** Hareket sayısı çok büyüdüğünde ürün × depo **bakiye özet tablosu** (sadece önbellek; ana kaynak yine hareketler) veya indexed view
- Listeleme endpoint'lerinde **sayfalama** ve filtreleme
- **Katmanların ayrı projelere bölünmesi**, Docker ile paketleme, CI pipeline
- Sipariş **kısa kapama** ve **iptal** endpoint'leri

---

## 🤖 Yapay zeka kullanım beyanı

> ⚠️ *Teslimden önce bu bölümü kendi kullanımınıza göre gözden geçirip düzenleyin.*

Bu çalışmada **Anthropic'in Claude** asistanını bir **mentor ve eşli programlama (pair programming) aracı** olarak kullandım:

- **Tasarım tartışması:** Varsayımlar (çoklu depo, kısmi teslimat, birim yapısı, durum yönetimi) ve normalizasyon kararları soru-cevap şeklinde tartışılarak alındı. Örneğin teslim tarihinin başlık yerine satırda olması gerektiği, "aynı ürün iki satırda olabilir mi?" tartışması sırasında ortaya çıktı ve şema buna göre düzeltildi.
- **Kod üretimi:** SQL script'leri ve C# katmanlarının (DTO, repository, service, controller, middleware) ilk taslakları asistan tarafından üretildi; her adımın gerekçesi açıklandı.
- **Test ve doğrulama:** Tüm script'leri kendi ortamımda (SQL Server 2022 Express, Visual Studio 2022) çalıştırdım, her endpoint'i Swagger üzerinden başarılı ve hatalı senaryolarla test ettim. Testlerde fark edilen bir sorun (aynı irsaliyenin tekrar girilmesinde 409 yerine 422 dönmesi) düzeltildi.
- **Dokümantasyon:** API dokümantasyonu, destek senaryosu analizi, güvenlik ve dashboard dokümanlarının taslakları asistanla birlikte hazırlandı.

Tüm tasarım kararlarının gerekçelerini anlayarak ve tartışarak ilerledim; çalışmadaki kararların sorumluluğu bana aittir.
