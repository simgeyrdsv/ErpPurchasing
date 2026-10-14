using System.ComponentModel.DataAnnotations;

namespace ErpPurchasing.API.Application.DTOs;

// ===================== REQUEST =====================
public sealed class CreatePurchaseOrderRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Geçerli bir tedarikçi seçilmelidir.")]
    public int SupplierId { get; init; }

    public DateTime? OrderDate { get; init; }          // Boş gelirse bugün kabul edilir

    [StringLength(500)]
    public string? Description { get; init; }

    [Required]
    [MinLength(1, ErrorMessage = "Sipariş en az bir satır içermelidir.")]
    public List<CreatePurchaseOrderLineRequest> Lines { get; init; } = new();
}

public sealed class CreatePurchaseOrderLineRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Geçerli bir ürün seçilmelidir.")]
    public int ItemId { get; init; }

    [Range(0.001, 999_999_999, ErrorMessage = "Miktar sıfırdan büyük olmalıdır.")]
    public decimal Quantity { get; init; }

    [Range(0, 999_999_999, ErrorMessage = "Birim fiyat negatif olamaz.")]
    public decimal UnitPrice { get; init; }

    public DateTime? ExpectedDeliveryDate { get; init; }
}

// ===================== RESPONSE =====================
public sealed class PurchaseOrderDto
{
    public int PurchaseOrderId { get; init; }
    public string OrderNumber { get; init; } = string.Empty;
    public int SupplierId { get; init; }
    public string SupplierName { get; init; } = string.Empty;
    public DateTime OrderDate { get; init; }
    public byte StatusId { get; init; }
    public string StatusName { get; init; } = string.Empty;
    public string? Description { get; init; }
    public decimal TotalAmount { get; init; }   // Saklanmıyor, SATIRLARDAN HESAPLANIYOR
    public DateTime CreatedAt { get; init; }
    public List<PurchaseOrderLineDto> Lines { get; init; } = new();
}

public sealed class PurchaseOrderLineDto
{
    public int PurchaseOrderLineId { get; init; }
    public short LineNumber { get; init; }
    public int ItemId { get; init; }
    public string ItemCode { get; init; } = string.Empty;
    public string ItemName { get; init; } = string.Empty;
    public string UnitCode { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public decimal UnitPrice { get; init; }
    public decimal LineAmount { get; init; }
    public DateTime? ExpectedDeliveryDate { get; init; }
    public decimal ReceivedQuantity { get; init; }   // Onaylı mal kabullerden
    public decimal RemainingQuantity { get; init; }
}