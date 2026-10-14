using System.ComponentModel.DataAnnotations;

namespace ErpPurchasing.API.Application.DTOs;

// Cevap (Response): API'nin döndürdüğü ürün bilgisi
public sealed class ItemDto
{
    public int ItemId { get; init; }
    public string ItemCode { get; init; } = string.Empty;
    public string ItemName { get; init; } = string.Empty;
    public byte ItemTypeId { get; init; }
    public string ItemTypeName { get; init; } = string.Empty;
    public string UnitCode { get; init; } = string.Empty;
    public decimal? CriticalStockLevel { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedAt { get; init; }
}

// İstek (Request): Yeni ürün oluştururken gönderilen veri + doğrulama kuralları
public sealed class CreateItemRequest
{
    [Required(ErrorMessage = "Ürün kodu zorunludur.")]
    [StringLength(30, ErrorMessage = "Ürün kodu en fazla 30 karakter olabilir.")]
    public string ItemCode { get; init; } = string.Empty;

    [Required(ErrorMessage = "Ürün adı zorunludur.")]
    [StringLength(150, ErrorMessage = "Ürün adı en fazla 150 karakter olabilir.")]
    public string ItemName { get; init; } = string.Empty;

    [Range(1, 2, ErrorMessage = "Ürün tipi 1 (Hammadde) veya 2 (Mamul) olmalıdır.")]
    public byte ItemTypeId { get; init; }

    [Range(1, int.MaxValue, ErrorMessage = "Geçerli bir birim seçilmelidir.")]
    public int UnitId { get; init; }

    [Range(0, 999_999_999, ErrorMessage = "Kritik stok seviyesi negatif olamaz.")]
    public decimal? CriticalStockLevel { get; init; }
}