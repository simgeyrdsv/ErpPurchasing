# Bölüm 3 — API Tasarımı

**Teknoloji:** ASP.NET Core 8 Web API · Dapper · SQL Server 2022
**Base URL (lokal):** `https://localhost:7005`
**Format:** Tüm istek ve cevaplar `application/json`. Tarihler ISO 8601 (`2026-10-14`).
**Canlı dokümantasyon:** Uygulama çalışırken `/swagger`

## Ortak kurallar

### HTTP durum kodları

| Kod | Anlamı | Örnek |
|---|---|---|
| **200** OK | Başarılı okuma / işlem | Ürün listesi |
| **201** Created | Kayıt oluşturuldu. `Location` başlığında yeni kaydın adresi döner | Yeni sipariş |
| **400** Bad Request | İstek **formatı** geçersiz (zorunlu alan boş, negatif miktar) | `itemName: ""` |
| **404** Not Found | İstenen kayıt yok | `/api/items/999` |
| **409** Conflict | Mevcut veriyle **çakışma** | Aynı ürün kodu, aynı irsaliye no |
| **422** Unprocessable Entity | Format doğru ama **iş kuralı** izin vermiyor | Fazla teslimat, iptal edilmiş sipariş |
| **500** Internal Server Error | Beklenmeyen hata. Detay sadece loga yazılır, kullanıcıya gösterilmez | — |

### Hata formatı (RFC 7807 — ProblemDetails)

Tüm hatalar aynı yapıda döner. Bu sayede istemci tarafı tek bir hata işleyiciyle çalışabilir.

```json
{
  "title": "İş kuralı ihlali",
  "status": 422,
  "detail": "PO-2026-000006 / Preform 0,5 L: Kalan miktar 2.000, kabul edilmek istenen 3.000. Fazla teslimat kabul edilmez.",
  "instance": "/api/goods-receipts"
}
```

Doğrulama (400) hatalarında alan bazlı liste döner:
```json
{
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": { "ItemName": ["Ürün adı zorunludur."] }
}
```

---

## 1. Ürünler

### `GET /api/items`
**Amaç:** Tüm ürün kartlarını tip ve birim bilgisiyle listeler.

**Request:** Gövde yok.

**Response — 200**
```json
[
  {
    "itemId": 1,
    "itemCode": "HM-PRF-050",
    "itemName": "Preform 0,5 L",
    "itemTypeId": 1,
    "itemTypeName": "Hammadde",
    "unitCode": "ADET",
    "criticalStockLevel": 20000,
    "isActive": true,
    "createdAt": "2026-10-14T02:26:59"
  }
]
```

**Hata durumları:** Yalnızca 500 (beklenmeyen hata).

---

### `GET /api/items/{id}`
**Amaç:** Tek bir ürün kartını getirir.

**Request:** `GET /api/items/1`

**Response — 200:** Yukarıdaki ürün nesnesi (tek kayıt).

**Hata durumları**
| Kod | Durum | Mesaj |
|---|---|---|
| 404 | Ürün yok | `Id'si 999 olan ürün bulunamadı.` |

---

### `POST /api/items`
**Amaç:** Yeni ürün kartı oluşturur.

**Request**
```json
{
  "itemCode": "HM-ETK-500",
  "itemName": "Etiket 5 L",
  "itemTypeId": 1,
  "unitId": 4,
  "criticalStockLevel": 5
}
```

| Alan | Kural |
|---|---|
| `itemCode` | Zorunlu, en fazla 30 karakter, **benzersiz** |
| `itemName` | Zorunlu, en fazla 150 karakter |
| `itemTypeId` | 1 (Hammadde) veya 2 (Mamul) |
| `unitId` | Tanımlı bir birim olmalı |
| `criticalStockLevel` | İsteğe bağlı, negatif olamaz |

**Response — 201** · `Location: /api/items/9`
```json
{
  "itemId": 9,
  "itemCode": "HM-ETK-500",
  "itemName": "Etiket 5 L",
  "itemTypeId": 1,
  "itemTypeName": "Hammadde",
  "unitCode": "RULO",
  "criticalStockLevel": 5,
  "isActive": true,
  "createdAt": "2026-10-14T03:26:35"
}
```

**Hata durumları**
| Kod | Durum | Mesaj |
|---|---|---|
| 400 | Zorunlu alan boş / format hatası | `Ürün adı zorunludur.` |
| 409 | Ürün kodu zaten var | `'HM-ETK-500' kodlu bir ürün zaten mevcut.` |
| 422 | Birim tanımlı değil | `Id'si 99 olan birim tanımlı değil.` |

> **Tasarım notu:** Kod tekrarı önceden `SELECT` ile kontrol edilmez. Eşzamanlı iki istekte ikisi de "kod yok" görebilir (race condition). Doğruluğun tek kaynağı veritabanındaki `UNIQUE` kısıtıdır; SQL hata kodları 2627/2601 yakalanıp 409'a çevrilir.

---

## 2. Satınalma Siparişi

### `POST /api/purchase-orders`
**Amaç:** Başlık ve satırlarıyla yeni bir satınalma siparişi oluşturur. Başlık ve satırlar **tek transaction** içinde kaydedilir; herhangi bir satırda hata olursa hiçbir şey kaydedilmez. Sipariş numarası SQL `SEQUENCE` ile otomatik üretilir.

**Request**
```json
{
  "supplierId": 1,
  "orderDate": "2026-10-14",
  "description": "Kasım ayı preform ve kapak ihtiyacı",
  "lines": [
    { "itemId": 1, "quantity": 5000, "unitPrice": 0.45, "expectedDeliveryDate": "2026-10-25" },
    { "itemId": 2, "quantity": 5000, "unitPrice": 0.12, "expectedDeliveryDate": "2026-10-25" }
  ]
}
```

| Alan | Kural |
|---|---|
| `supplierId` | Var ve aktif olmalı |
| `orderDate` | İsteğe bağlı; boşsa bugün |
| `lines` | En az 1 satır |
| `lines[].itemId` | Var ve aktif olmalı. Aynı ürün birden fazla satırda olabilir (planlı bölünmüş teslimat) |
| `lines[].quantity` | > 0 |
| `lines[].unitPrice` | ≥ 0 |
| `lines[].expectedDeliveryDate` | İsteğe bağlı; sipariş tarihinden önce olamaz |

**Response — 201** · `Location: /api/purchase-orders/6`
```json
{
  "purchaseOrderId": 6,
  "orderNumber": "PO-2026-000006",
  "supplierId": 1,
  "supplierName": "Plastik Ambalaj A.Ş.",
  "orderDate": "2026-10-14T00:00:00",
  "statusId": 1,
  "statusName": "Açık",
  "description": "Kasım ayı preform ve kapak ihtiyacı",
  "totalAmount": 2850,
  "createdAt": "2026-10-14T03:41:44",
  "lines": [
    {
      "purchaseOrderLineId": 9,
      "lineNumber": 1,
      "itemId": 1,
      "itemCode": "HM-PRF-050",
      "itemName": "Preform 0,5 L",
      "unitCode": "ADET",
      "quantity": 5000,
      "unitPrice": 0.45,
      "lineAmount": 2250,
      "expectedDeliveryDate": "2026-10-25T00:00:00",
      "receivedQuantity": 0,
      "remainingQuantity": 5000
    }
  ]
}
```

**Hata durumları**
| Kod | Durum | Mesaj |
|---|---|---|
| 400 | Satır yok / miktar ≤ 0 | `Sipariş en az bir satır içermelidir.` |
| 422 | Tedarikçi yok veya pasif | `Id'si 99 olan tedarikçi bulunamadı veya pasif.` |
| 422 | Ürün yok veya pasif | `Şu ürünler bulunamadı veya pasif: 99` |
| 422 | Teslim tarihi sipariş tarihinden önce | `Beklenen teslim tarihi sipariş tarihinden önce olamaz.` |

> **Tasarım notu:** `totalAmount`, `lineAmount`, `receivedQuantity` ve `remainingQuantity` veritabanında saklanmaz; sorgu anında hesaplanır (normalizasyon — türetilmiş veri saklanmaz).

---

### `GET /api/purchase-orders/{id}`
**Amaç:** Siparişi satırlarıyla ve **teslimat durumuyla** (teslim alınan / kalan) birlikte getirir. Teslim alınan miktara sadece **onaylı** mal kabuller dahildir.

**Request:** `GET /api/purchase-orders/2`

**Response — 200:** Yukarıdaki formatta. Örnek: PO2'nin preform satırı → `"receivedQuantity": 6000, "remainingQuantity": 4000` (taslaktaki 4.000'lik mal kabul sayılmaz).

**Hata durumları**
| Kod | Durum | Mesaj |
|---|---|---|
| 404 | Sipariş yok | `Id'si 99 olan sipariş bulunamadı.` |

---

## 3. Mal Kabul

### `POST /api/goods-receipts`
**Amaç:** Tedarikçiden gelen malların kabulünü yapar. `approve: true` ise (varsayılan) aynı işlem içinde:
1. Mal kabul **onaylanır**,
2. Her satır için **stok giriş hareketi** oluşturulur,
3. İlgili siparişlerin durumu güncellenir (**Kısmi Teslim** / **Tamamlandı**).

`approve: false` ile **taslak** olarak kaydedilir; stoğa işlenmez.

**İşlem akışı (tek transaction):**
```
1. Aynı irsaliye daha önce girilmiş mi?             → 409
2. Sipariş satırlarını KİLİTLE (UPDLOCK)            ← eşzamanlılık koruması
3. Güncel sipariş/teslim miktarlarını oku
4. İş kuralları (GoodsReceiptValidator)             → 422
5. Başlık + satırları yaz
6. (approve) Onay + stok hareketi + sipariş durumu
7. COMMIT  (herhangi bir hatada ROLLBACK)
```

**Request**
```json
{
  "supplierId": 1,
  "deliveryNoteNumber": "IRS-PA-2001",
  "receiptDate": "2026-10-14",
  "description": "Sabah sevkiyatı",
  "approve": true,
  "lines": [
    { "purchaseOrderLineId": 9,  "warehouseId": 1, "quantity": 3000 },
    { "purchaseOrderLineId": 10, "warehouseId": 1, "quantity": 5000 }
  ]
}
```

| Alan | Kural |
|---|---|
| `supplierId` | Var ve aktif olmalı; satırlardaki siparişler **bu tedarikçiye** ait olmalı |
| `deliveryNoteNumber` | Zorunlu; aynı tedarikçi için **benzersiz** |
| `receiptDate` | İsteğe bağlı; boşsa bugün; ileri tarih olamaz |
| `approve` | Varsayılan `true` |
| `lines[].purchaseOrderLineId` | Siparişi **Açık** veya **Kısmi Teslim** durumunda olmalı |
| `lines[].warehouseId` | Var ve aktif olmalı. Depo **satır** bazındadır |
| `lines[].quantity` | > 0 ve **kalan sipariş miktarını aşamaz** (fazla teslimat kabul edilmez) |

**Response — 201** · `Location: /api/goods-receipts/5`
```json
{
  "goodsReceiptId": 5,
  "receiptNumber": "GR-2026-000005",
  "supplierId": 1,
  "supplierName": "Plastik Ambalaj A.Ş.",
  "deliveryNoteNumber": "IRS-PA-2001",
  "receiptDate": "2026-10-14T00:00:00",
  "statusId": 2,
  "statusName": "Onaylandı",
  "approvedAt": "2026-10-14T03:53:01",
  "description": null,
  "createdAt": "2026-10-14T03:53:01",
  "lines": [
    {
      "goodsReceiptLineId": 7,
      "lineNumber": 1,
      "purchaseOrderLineId": 9,
      "orderNumber": "PO-2026-000006",
      "itemCode": "HM-PRF-050",
      "itemName": "Preform 0,5 L",
      "unitCode": "ADET",
      "warehouseCode": "HM-DEPO",
      "quantity": 3000
    }
  ]
}
```

**Hata durumları**
| Kod | Durum | Mesaj |
|---|---|---|
| 400 | İrsaliye no boş / satır yok / miktar ≤ 0 | `İrsaliye numarası zorunludur.` |
| 409 | Aynı irsaliye daha önce girilmiş | `'IRS-PA-2001' numaralı irsaliye bu tedarikçi için daha önce girilmiş.` |
| 422 | Fazla teslimat | `PO-2026-000006 / Preform 0,5 L: Kalan miktar 2.000, kabul edilmek istenen 3.000. Fazla teslimat kabul edilmez.` |
| 422 | Sipariş iptal / kapatılmış / tamamlanmış | `PO-2026-000005 siparişi 'İptal' durumunda, mal kabul yapılamaz.` |
| 422 | Sipariş başka tedarikçiye ait | `PO-2026-000006 siparişi bu tedarikçiye ait değil.` |
| 422 | Tedarikçi / depo yok veya pasif | `Şu depolar bulunamadı veya pasif: 9` |
| 422 | İleri tarihli mal kabul | `Mal kabul tarihi ileri bir tarih olamaz.` |

> Birden fazla kural ihlali varsa tümü **tek mesajda** (`|` ile ayrılmış) döner; kullanıcı hepsini tek seferde düzeltebilir.

---

### `GET /api/goods-receipts/{id}` *(ek endpoint)*
**Amaç:** Mal kabulü satırlarıyla getirir. `POST` cevabındaki `Location` başlığının işaret ettiği kaynaktır.

**Hata durumları:** 404 — `Id'si 99 olan mal kabul bulunamadı.`

---

### `POST /api/goods-receipts/{id}/approve` *(ek endpoint)*
**Amaç:** **Taslak** durumdaki mal kabulü onaylar ve stoğa işler. Onay anında tüm iş kuralları (fazla teslimat, sipariş durumu) **yeniden** ve kilit altında kontrol edilir; taslak oluşturulduktan sonra durum değişmiş olabilir.

> Bölüm 4'teki destek senaryosunun ("stokta 60 görünüyor") olası nedenlerinden biri taslakta kalmış bir mal kabuldür. Bu endpoint o durumun çözümüdür.

**Request:** `POST /api/goods-receipts/4/approve` (gövde yok)

**Response — 200:** Onaylanmış mal kabul (`"statusName": "Onaylandı"`).

**Hata durumları**
| Kod | Durum | Mesaj |
|---|---|---|
| 404 | Mal kabul yok | `Id'si 99 olan mal kabul bulunamadı.` |
| 409 | Zaten onaylı | `Bu mal kabul zaten onaylanmış.` |
| 422 | Onay anında kural ihlali | Fazla teslimat / sipariş durumu mesajları |

---

## 4. Stok

### `GET /api/stock`
**Amaç:** Ürün **ve depo** bazında güncel stok miktarını döner. Stok, hareketlerin (`StockTransaction`) toplamıdır; ayrı bir bakiye tablosunda tutulmaz.

**Query parametreleri (isteğe bağlı)**
| Parametre | Açıklama |
|---|---|
| `itemId` | Sadece bu ürün |
| `warehouseId` | Sadece bu depo |

**Request:** `GET /api/stock?warehouseId=2`

**Response — 200**
```json
[
  {
    "itemId": 5,
    "itemCode": "HM-KOL-024",
    "itemName": "Koli (24'lü)",
    "unitCode": "ADET",
    "warehouseId": 2,
    "warehouseCode": "MM-DEPO",
    "warehouseName": "Mamul Deposu",
    "quantity": 5000
  }
]
```
Bakiyesi sıfır olan ürün-depo kombinasyonları listelenmez.

**Hata durumları:** Yalnızca 500. Filtreye uyan kayıt yoksa **200 + boş liste** döner (404 değil; boş sonuç bir hata değildir).

> **Performans notu:** İsteğe bağlı filtreler `(@X IS NULL OR kolon = @X)` kalıbıyla yazıldı ve `OPTION (RECOMPILE)` eklendi. Böylece her çağrı kendi parametrelerine uygun bir çalışma planı alır (parameter sniffing problemi önlenir).

---

## Endpoint özeti

| Metot | Endpoint | Amaç | Başarı |
|---|---|---|---|
| GET | `/api/items` | Ürün listesi | 200 |
| GET | `/api/items/{id}` | Ürün detayı | 200 |
| POST | `/api/items` | Ürün oluştur | 201 |
| POST | `/api/purchase-orders` | Sipariş oluştur | 201 |
| GET | `/api/purchase-orders/{id}` | Sipariş + teslimat durumu | 200 |
| POST | `/api/goods-receipts` | Mal kabul (+ onay + stok) | 201 |
| GET | `/api/goods-receipts/{id}` | Mal kabul detayı *(ek)* | 200 |
| POST | `/api/goods-receipts/{id}/approve` | Taslak onayı *(ek)* | 200 |
| GET | `/api/stock` | Güncel stok | 200 |
