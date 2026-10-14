using System.ComponentModel.DataAnnotations;

namespace ErpPurchasing.API.Application.DTOs;

// ===================== REQUEST =====================
public sealed class CreateGoodsReceiptRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Geçerli bir tedarikçi seçilmelidir.")]
    public int SupplierId { get; init; }

    [Required(ErrorMessage = "İrsaliye numarası zorunludur.")]
    [StringLength(30)]
    public string DeliveryNoteNumber { get; init; } = string.Empty;

    public DateTime? ReceiptDate { get; init; }      // Boşsa bugün

    [StringLength(500)]
    public string? Description { get; init; }

    public bool Approve { get; init; } = true;       // true: oluştur + onayla + stoğa işle

    [Required]
    [MinLength(1, ErrorMessage = "Mal kabul en az bir satır içermelidir.")]
    public List<CreateGoodsReceiptLineRequest> Lines { get; init; } = new();
}

public sealed class CreateGoodsReceiptLineRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Geçerli bir sipariş satırı seçilmelidir.")]
    public int PurchaseOrderLineId { get; init; }

    [Range(1, int.MaxValue, ErrorMessage = "Geçerli bir depo seçilmelidir.")]
    public int WarehouseId { get; init; }

    [Range(0.001, 999_999_999, ErrorMessage = "Miktar sıfırdan büyük olmalıdır.")]
    public decimal Quantity { get; init; }
}

// ===================== RESPONSE =====================
public sealed class GoodsReceiptDto
{
    public int GoodsReceiptId { get; init; }
    public string ReceiptNumber { get; init; } = string.Empty;
    public int SupplierId { get; init; }
    public string SupplierName { get; init; } = string.Empty;
    public string DeliveryNoteNumber { get; init; } = string.Empty;
    public DateTime ReceiptDate { get; init; }
    public byte StatusId { get; init; }
    public string StatusName { get; init; } = string.Empty;
    public DateTime? ApprovedAt { get; init; }
    public string? Description { get; init; }
    public DateTime CreatedAt { get; init; }
    public List<GoodsReceiptLineDto> Lines { get; init; } = new();
}

public sealed class GoodsReceiptLineDto
{
    public int GoodsReceiptLineId { get; init; }
    public short LineNumber { get; init; }
    public int PurchaseOrderLineId { get; init; }
    public string OrderNumber { get; init; } = string.Empty;
    public string ItemCode { get; init; } = string.Empty;
    public string ItemName { get; init; } = string.Empty;
    public string UnitCode { get; init; } = string.Empty;
    public string WarehouseCode { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
}

// ===================== İÇ MODELLER (API'ye açılmaz) =====================
// Doğrulama için bir mal kabul satırının özü
public sealed record ReceiptLineInput(int PurchaseOrderLineId, int WarehouseId, decimal Quantity);

// Kilit altında okunan sipariş satırının o anki durumu
public sealed class OrderLineSnapshot
{
    public int PurchaseOrderLineId { get; init; }
    public string OrderNumber { get; init; } = string.Empty;
    public int SupplierId { get; init; }
    public byte OrderStatusId { get; init; }
    public string OrderStatusName { get; init; } = string.Empty;
    public string ItemName { get; init; } = string.Empty;
    public decimal OrderedQuantity { get; init; }
    public decimal ReceivedQuantity { get; init; }   // Sadece ONAYLI mal kabuller
}