# Bölüm 4 — ERP Destek Senaryosu

> **Bildirim:** "100 adetlik satınalma siparişi oluşturduk. Ürün teslim alındı ancak stok ekranında yalnızca 60 adet görünüyor."

## 0. Önce: Sorunu netleştirmek

Tahmin yürütmeden önce kullanıcıdan şu bilgileri alırım. Destek taleplerinin önemli bir kısmı bu aşamada çözülür ya da kapsamı daralır:

| Soru | Neden önemli? |
|---|---|
| Sipariş numarası ve ürün kodu nedir? | Tüm sorgular bu iki bilgiyle başlar |
| "Teslim alındı" bilgisi nereden geliyor? Sistemde mal kabul yapıldı mı, yoksa fiziksel olarak mı geldi? | "Geldi" ile "sisteme girildi" farklı şeylerdir |
| Tek seferde mi geldi, birden fazla sevkiyatta mı? Kaç irsaliye var? | Kısmi teslimat ihtimali |
| Stok ekranında hangi filtreler seçili? (depo, tarih, ürün) | En sık görülen "hata" ekran filtresidir |
| Ne zaman fark edildi? Ondan önce doğru görünüyor muydu? | Sonradan bir çıkış olmuş olabilir |
| Ekran görüntüsü alınabilir mi? | Kullanıcının gördüğünü birebir görmek için |

---

## 1. Olası nedenler

Nedenleri **kaynağına göre** gruplarım. Bu gruplama sonradan kök nedeni sınıflandırırken de işe yarar.

### A) Süreç / kullanıcı kaynaklı
| # | Neden | Açıklama |
|---|---|---|
| A1 | **Kısmi teslimat** | Tedarikçi gerçekte 60 adet getirdi, kalan 40 henüz gelmedi. Sistem doğru, beklenti yanlış |
| A2 | **Mal kabul onaylanmamış** | İkinci teslimatın (40) mal kabulü **taslakta** kalmış; onaylanmadığı için stoğa işlenmemiş |
| A3 | **Yanlış depoya giriş** | 40 adet başka bir depoya (örn. mamul deposu) kaydedilmiş; kullanıcı stok ekranında tek depoyu görüyor |
| A4 | **Yanlış ürün kartı** | Mal kabulde benzer kodlu başka bir ürün seçilmiş (örn. `HM-PRF-050` yerine `HM-PRF-500`) |
| A5 | **Yanlış miktar girişi** | Depocu 100 yerine 60 yazmış (sayım/yazım hatası) |
| A6 | **Yanlış siparişe bağlama** | Mal kabul aynı tedarikçinin başka bir siparişine bağlanmış |

### B) Stok hareketi kaynaklı (mal kabul doğru, sonra azalmış)
| # | Neden | Açıklama |
|---|---|---|
| B1 | **Üretime çıkış** | Mal kabulden sonra 40 adet üretime verilmiş. Stok gerçekten 60 |
| B2 | **Sayım farkı** | Sayım sonrası 40 adet eksik kaydı atılmış |
| B3 | **Mal kabul iptali / iade** | Kalite kontrolde reddedilip ters kayıt atılmış |

### C) Ekran / yetki kaynaklı
| # | Neden | Açıklama |
|---|---|---|
| C1 | **Ekran filtresi** | Tarih aralığı, depo veya "sadece aktif" filtresi |
| C2 | **Depo yetkisi** | Kullanıcı sadece yetkili olduğu depoları görüyor |
| C3 | **Önbellek / rapor gecikmesi** | Stok ekranı anlık değil, periyodik güncellenen bir özet tablodan besleniyor |

### D) Teknik / yazılım kaynaklı
| # | Neden | Açıklama |
|---|---|---|
| D1 | **Stok hareketi oluşmamış** | Mal kabul onaylandı ama stok hareketi yazılamadı (transaction kullanılmamış, yarım kalmış işlem, entegrasyon/job hatası) |
| D2 | **Birim dönüşüm hatası** | Sipariş koli, stok adet ile tutuluyor ve dönüşüm katsayısı yanlış |
| D3 | **Eşzamanlılık hatası** | Aynı anda yapılan iki işlemden biri diğerinin kaydını ezmiş |

> Bizim tasarımımızda D1 ve D3 **yapısal olarak engellenmiştir**: Mal kabul onayı ve stok hareketi aynı transaction içindedir (ya ikisi birden kaydedilir ya hiçbiri), sipariş satırları `UPDLOCK` ile kilitlenir ve aynı mal kabul satırının iki kez stoğa işlenmesi filtered unique index ile imkansızdır. D2 ise varsayım gereği kapsam dışıdır (tek birim). Yine de başka bir sistemde/sürümde bu nedenler kontrol listesinde kalmalıdır.

---

## 2. Kontrol edeceğim ekranlar (sırasıyla)

Sıralama, veri akışını takip eder: **Sipariş → Mal Kabul → Stok Hareketi → Stok Bakiyesi**

| # | Ekran | Neye bakarım? | Elenen nedenler |
|---|---|---|---|
| 1 | **Satınalma siparişi detayı** | Sipariş miktarı, teslim alınan, kalan miktar, sipariş durumu | A1, A6 |
| 2 | **Mal kabul listesi** (sipariş bazında, **taslaklar dahil**) | Kaç mal kabul var, durumları, irsaliye numaraları | A1, A2 |
| 3 | **Mal kabul detayı** | Satırlardaki ürün, depo, miktar | A3, A4, A5 |
| 4 | **Stok ekranı** — filtresiz, **depo kırılımlı** | Toplam ve depo bazında miktar | A3, C1, C2 |
| 5 | **Ürün hareket dökümü (stok ekstresi)** | Mal kabulden sonraki tüm giriş/çıkışlar | B1, B2, B3 |
| 6 | **Kullanıcı yetkileri** | Kullanıcının depo/ekran yetkileri | C2 |
| 7 | **Uygulama logları / audit log** | Hata kaydı, işlemi kimin ne zaman yaptığı | D1, D3 |

Ekranlarda net cevap çıkmazsa veya ekranlar birbirini tutmuyorsa (örn. mal kabul "onaylı" ama ekstrede hareket yok) **veritabanına inerim**: Bu, sorunun kullanıcı değil sistem kaynaklı olduğuna dair güçlü bir işarettir.

---

## 3. Kontrol edeceğim tablolar

| Tablo | Kontrol |
|---|---|
| `PurchaseOrder` | Sipariş durumu (Açık / Kısmi / Tamamlandı / İptal) |
| `PurchaseOrderLine` | Sipariş miktarı, ürün, beklenen teslim tarihi |
| `GoodsReceipt` | Durum (**Taslak / Onaylandı**), onay zamanı, irsaliye no |
| `GoodsReceiptLine` | Miktar, **depo**, hangi sipariş satırına bağlı |
| `StockTransaction` | Mal kabul satırına karşılık gelen giriş hareketi var mı? Sonrasında çıkış var mı? |
| `StockTransactionType` | Hareketlerin tipi ve yönü |
| `Item` / `Unit` | Doğru ürün kartı mı? Birim ne? |
| `Warehouse` | Hangi depo, aktif mi? |
| *(Gerçek sistemde)* Audit / log tabloları | Kim, ne zaman, hangi değeri değiştirdi |

---

## 4. Çalıştıracağım SQL sorguları

Tüm sorgular `database/04_support_diagnostics.sql` dosyasındadır. Hepsi **salt okunurdur**. Canlı sistemde teşhis aşamasında asla `UPDATE`/`DELETE` çalıştırılmaz.

| Adım | Sorgu | Cevapladığı soru |
|---|---|---|
| 1 | **Sipariş → tüm mal kabuller** (taslaklar dahil) | Kaç mal kabul var, hangi durumda, hangi depoda? |
| 2 | **Mal kabul satırı ↔ stok hareketi eşleşmesi** | Her mal kabul stoğa doğru işlenmiş mi? Otomatik teşhis sütunu: `TASLAK`, `Onaylı ama stok hareketi YOK`, `Miktar farkı`, `Depo farkı`, `İptal edilmiş`, `OK` |
| 3 | **Depo bazında stok** | Eksik miktar başka bir depoda mı? |
| 4 | **Hareket dökümü + yürüyen bakiye** (window function) | Mal kabulden sonra çıkış olmuş mu? Bakiye hangi hareketle 60'a düştü? |
| 5 | **Benzer ürün kartları** | Mal yanlış karta girilmiş olabilir mi? |
| Etki A | **Onaylı ama stok hareketi olmayan tüm mal kabuller** | Sorun sistem geneli mi? (Sonuç dönerse yazılım hatası) |
| Etki B | **Uzun süredir taslakta bekleyen mal kabuller** | Başka bekleyen taslak var mı? |
| Etki C | **Durumu miktarlarla tutarsız siparişler** | Sipariş durumları doğru güncelleniyor mu? |

Örnek: 2. adımdaki teşhis sorgusu
```sql
CASE
    WHEN gr.StatusId = 1                     THEN N'TASLAK: Onaylanmamış, stoğa işlenmedi'
    WHEN stIn.StockTransactionId IS NULL     THEN N'HATA: Onaylı ama stok hareketi YOK'
    WHEN stIn.Quantity <> grl.Quantity       THEN N'HATA: Miktar farkı'
    WHEN stIn.WarehouseId <> grl.WarehouseId THEN N'HATA: Depo farkı'
    WHEN stRev.StockTransactionId IS NOT NULL THEN N'BİLGİ: Mal kabul iptal edilmiş (ters kayıt)'
    ELSE N'OK'
END AS Diagnosis
```

Örnek: 4. adımdaki yürüyen bakiye
```sql
SUM(st.Quantity) OVER (PARTITION BY st.WarehouseId
                       ORDER BY st.TransactionDate, st.StockTransactionId
                       ROWS UNBOUNDED PRECEDING) AS RunningBalance
```

---

## 5. Kök neden analizi metodolojisi

### Yaklaşım: Veri zincirini takip et, ilk kopukluğu bul

```
 Sipariş (100)  →  Mal Kabul (60 + 40?)  →  Stok Hareketi (+60, +40?)  →  Stok Ekranı (60)
      ①                    ②                          ③                          ④
```
Her halkada beklenen ile gerçekleşeni karşılaştırırım. Değerlerin ilk ayrıştığı halka sorunun **nerede** olduğunu gösterir:
- **① ≠ ②** → Mal kabul eksik: kısmi teslimat (A1) veya yanlış miktar/sipariş (A5, A6)
- **② ≠ ③** → Mal kabul var ama stoğa işlenmemiş: taslak (A2) veya yazılım hatası (D1)
- **③ ≠ ④** → Hareketler doğru ama ekran farklı: filtre/yetki/depo (A3, C1, C2) veya sonradan çıkış (B1-B3)

### Adımlar

1. **Netleştir** — Bölüm 0'daki sorular. Sorunu tek cümleyle tanımla: *"PO-X, ürün Y: Sipariş 100, stok ekranında 60."*
2. **Yeniden üret** — Kullanıcının ekranını aynı filtrelerle aç. Sorun benim ekranımda da görünüyor mu?
3. **Veriyi takip et** — Yukarıdaki zinciri ekranlar ve SQL sorgularıyla halka halka izle.
4. **Hipotezleri ele** — Olası nedenler listesini kanıtla tek tek kapat. Bir neden ancak veriyle doğrulanınca "bulundu" sayılır.
5. **5 Neden (5 Whys)** — Görünen sebepte durma, altındaki süreç veya sistem eksikliğine in.
6. **Sınıflandır** — Kök neden: Kullanıcı hatası mı, süreç eksikliği mi, veri hatası mı, yazılım hatası mı, konfigürasyon mu?
7. **Etki analizi** — Aynı sorun başka sipariş veya ürünlerde de var mı? (Etki sorguları A, B, C)
8. **Düzelt — üç seviyede:**
   - **Anlık düzeltme:** Veri, sistemin kendi akışıyla düzeltilir: taslak onaylanır, yanlış giriş ters kayıtla iptal edilip doğru kayıt girilir. **Doğrudan `UPDATE` ile stok değiştirilmez**; izlenebilirlik kaybolur ve stok defteri bozulur.
   - **Kalıcı düzeltme:** Kök nedene göre yazılım düzeltmesi, ek doğrulama veya kullanıcı eğitimi.
   - **Önleyici aksiyon:** Aynı sorunun tekrarını erken yakalayacak izleme (alarm, rapor, dashboard uyarısı).
9. **Doğrula** — Düzeltmeden sonra stok ekranı, sipariş durumu ve etki sorgularını tekrar kontrol et.
10. **Dokümante et ve bildir** — Kullanıcıya açık bir dille sonucu ilet. Bilgi bankasına kayıt aç (benzer bir talep geldiğinde hızlı çözüm için).

### Örnek: Bu sistemde canlı olarak gösterilebilen vaka

Örnek veride (`02_seed_data.sql`) bu senaryonun birebir benzeri kurgulanmıştır: **PO-2026-000002**, 10.000 adet preform.

**Bulgular:**
- ① Sipariş: 10.000
- ② Mal kabuller: GR-2026-000002 → 6.000 (**Onaylandı**), GR-2026-000004 → 4.000 (**Taslak**)
- ③ Stok hareketleri: sadece +6.000. Teşhis sorgusu: `TASLAK: Onaylanmamış, stoğa işlenmedi`
- İlk kopukluk ② ile ③ arasında → **A2**

**5 Neden:**
1. Stokta neden 4.000 eksik? → İkinci mal kabul stoğa işlenmemiş.
2. Neden işlenmemiş? → Mal kabul taslakta kalmış, onaylanmamış.
3. Neden onaylanmamış? → Depocu sayımı girdikten sonra onay adımını tamamlamamış.
4. Neden fark edilmedi? → Bekleyen taslaklar için bir uyarı veya rapor yok.
5. Neden yok? → Süreçte "taslak mal kabullerin günlük kontrolü" adımı tanımlanmamış.

**Kök neden:** Süreç ve izleme eksikliği (yazılım hatası değil).

**Aksiyonlar:**
| Seviye | Aksiyon |
|---|---|
| Anlık | `POST /api/goods-receipts/4/approve` ile taslak onaylanır → stok 4.000 artar, sipariş durumu "Tamamlandı" olur *(sistemde test edildi)* |
| Kalıcı | Depo personeline onay adımı hakkında bilgilendirme / eğitim |
| Önleyici | "24 saatten uzun süredir taslakta bekleyen mal kabuller" raporu (Etki sorgusu B) dashboard'a uyarı olarak eklenir |

---

## 6. Sistem tasarımının bu tür sorunları nasıl önlediği

| Risk | Önlem |
|---|---|
| Mal kabul onaylandı ama stok oluşmadı | Onay + stok hareketi + sipariş durumu **tek transaction** |
| Aynı mal kabul iki kez stoğa işlendi | `UX_StockTransaction_ReceiptLine_Type` filtered unique index |
| İki depocu aynı anda kalan miktarı aştı | Sipariş satırlarında `UPDLOCK` + kilit altında kural kontrolü |
| Aynı irsaliye iki kez girildi | `UQ_GoodsReceipt_DeliveryNote` + `HOLDLOCK` ön kontrolü |
| Stok bakiyesi ile hareketler tutarsız | Ayrı bakiye tutulmaz; stok **her zaman** hareketlerden hesaplanır |
| Stok elle değiştirildi, iz kalmadı | Hareketler silinmez/güncellenmez; düzeltme **ters kayıtla** yapılır |
