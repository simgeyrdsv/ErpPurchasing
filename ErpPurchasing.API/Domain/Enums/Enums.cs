namespace ErpPurchasing.API.Domain.Enums;

public enum ItemType : byte
{
    RawMaterial = 1,   // Hammadde
    FinishedGood = 2    // Mamul
}

public enum PurchaseOrderStatus : byte
{
    Open = 1,   // Açık
    PartiallyReceived = 2,   // Kısmi Teslim
    Completed = 3,   // Tamamlandı
    Cancelled = 4,   // İptal
    Closed = 5    // Kapatıldı (kısa kapama)
}

public enum GoodsReceiptStatus : byte
{
    Draft = 1,   // Taslak
    Approved = 2    // Onaylandı
}

public enum StockTransactionType : byte
{
    GoodsReceipt = 1,   // Mal Kabul Girişi
    ProductionIssue = 2,   // Üretime Çıkış
    CountSurplus = 3,   // Sayım Fazlası
    CountShortage = 4,   // Sayım Eksiği
    GoodsReceiptReversal = 5    // Mal Kabul İptali
}