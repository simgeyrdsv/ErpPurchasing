# Bonus — ERP Yönetim Dashboard'u

**Hedef kullanıcı:** Satınalma müdürü, depo şefi, fabrika yönetimi
**Veri kaynağı:** `database/05_dashboard_views.sql` içindeki view'lar. Power BI doğrudan bu view'lara bağlanır; iş mantığı veritabanında **tek yerde** durur, rapor aracına dağılmaz.
**Yenileme:** Stok ve açık sipariş kartları anlık (DirectQuery), trend grafikleri günlük.

## Wireframe

```
┌───────────────────────────────────────────────────────────────────────────────────┐
│  SATINALMA & STOK YÖNETİM PANELİ          Depo: [Tümü ▼]  Dönem: [Son 30 gün ▼]  │
├────────────────────┬────────────────────┬────────────────────┬────────────────────┤
│  TOPLAM STOK DEĞERİ│  KRİTİK STOK       │  AÇIK SİPARİŞ      │  GECİKEN SİPARİŞ   │
│                    │                    │                    │                    │
│    ₺ 42.235        │    ⚠ 6 ürün        │   2 sipariş        │    🔴 0            │
│  (ağırlıklı ort.   │  (eşik altında)    │   ₺ 28.900 açık    │                    │
│   maliyet)         │                    │                    │                    │
├────────────────────┴────────────────────┴────────────────────┴────────────────────┤
│  SON 12 AYLIK SATINALMA TRENDİ (₺)                                    [KPI 6]     │
│                                                                                   │
│  60k ┤                                                                    ██      │
│  40k ┤                                                                    ██      │
│  20k ┤                                                                    ██      │
│    0 ┼──────────────────────────────────────────▄▄──────────────────▄▄────██──    │
│       Kas   Ara   Oca   Şub   Mar   Nis   May   Haz   Tem   Ağu   Eyl   Eki       │
├─────────────────────────────────────────┬─────────────────────────────────────────┤
│  KRİTİK STOK ÜRÜNLERİ        [KPI 2]    │  EN ÇOK ALIM YAPILAN TEDARİKÇİLER [KPI 5]│
│                                         │                                         │
│  Ürün            Mevcut   Eşik   Eksik  │  Karton Kutu          ████████  ₺33.750 │
│  Preform 0,5 L    11.000  20.000  9.000 │  Etiket Matbaacılık   ███████   ₺28.000 │
│  Kapak            13.000  20.000  7.000 │  Plastik Ambalaj      ███       ₺12.150 │
│  0,5 L Su            0      500    500  │                                         │
│  Etiket 0,5 L        0       10     10  │  (son 12 ay, iptal hariç)               │
│  [Tümünü gör →]                         │                                         │
├─────────────────────────────────────────┼─────────────────────────────────────────┤
│  GÜNLÜK STOK HAREKETLERİ     [KPI 3]    │  AÇIK SİPARİŞLER             [KPI 4]    │
│                                         │                                         │
│   ▓ Giriş  ░ Çıkış                      │  Sipariş         Tedarikçi   Açık ₺  ⏰  │
│   ▓▓      ▓                ▓▓           │  PO-2026-000004  Etiket Mat. 28.000  ✓  │
│   ▓▓ ░░   ▓ ░          ░   ▓▓           │  PO-2026-000006  Plastik A.     900  ✓  │
│  ─────────────────────────────          │                                         │
│   -15g    -10g    -8g   -2g   bugün     │  ⏰ = beklenen tarih geçti mi?          │
├─────────────────────────────────────────┴─────────────────────────────────────────┤
│  ⚠ UYARILAR:  1 mal kabul 24 saatten uzun süredir TASLAKTA bekliyor  [İncele →]  │
└───────────────────────────────────────────────────────────────────────────────────┘
```
*Rakamlar örnek veri + Swagger testlerinden sonraki duruma göre view'lardan hesaplanmıştır. Uyarı bandı, testten önceki durumu (GR-4 taslakta) örneklemektedir.*

## KPI tanımları

| # | KPI | Kaynak view | Hesaplama | Neden önemli? |
|---|---|---|---|---|
| 1 | **Toplam stok değeri** | `vw_StockValue` | Σ (stok miktarı × ağırlıklı ortalama alış maliyeti) | Depoda bağlı duran sermaye |
| 2 | **Kritik stok ürünleri** | `vw_CriticalStock` | Stok < `CriticalStockLevel` | Üretimin durma riski → acil sipariş ihtiyacı |
| 3 | **Günlük stok hareketleri** | `vw_DailyStockMovements` | Gün × hareket tipi bazında **hareket sayısı** | Depo iş yükü, olağandışı günler |
| 4 | **Açık satınalma siparişleri** | `vw_OpenPurchaseOrders` | Durumu Açık/Kısmi olanlar; açık tutar = Σ kalan miktar × fiyat; gecikme bayrağı | Beklenen teslimatlar ve tedarikçi performansı |
| 5 | **En çok alım yapılan tedarikçiler** | `vw_TopSuppliers12M` | Son 12 ay sipariş tutarı (iptal hariç) | Tedarikçi bağımlılığı, pazarlık gücü |
| 6 | **Son 12 aylık satınalma trendi** | `vw_PurchaseTrend12M` | Ay bazında sipariş tutarı | Mevsimsellik, bütçe takibi |
| + | **Bekleyen taslak mal kabuller** | `04_support_diagnostics.sql` Etki-B | Taslakta 24 saatten uzun bekleyenler | Bölüm 4'teki sorunun **önleyici** kontrolü |

## Tasarım kararları
- **Hareket sayısı (KPI 3):** Stok hareketlerinde farklı birimler (adet, kg, rulo) bir arada olduğu için miktarlar toplanamaz; anlamlı ve birimden bağımsız ölçü **hareket sayısıdır**.
- **Değerleme yöntemi (KPI 1):** Ağırlıklı ortalama maliyet (varsayım). FIFO gibi yöntemler muhasebe politikasına göre seçilir.
- **Uyarı bandı:** Dashboard sadece rakam göstermez, **aksiyon alınması gereken** durumları (taslak mal kabul, geciken sipariş, kritik stok) öne çıkarır.
- **Etkileşim:** Depo ve dönem filtreleri tüm kartlara uygulanır; her tablo detay ekranına (sipariş detayı, ürün hareket dökümü) bağlanır.
