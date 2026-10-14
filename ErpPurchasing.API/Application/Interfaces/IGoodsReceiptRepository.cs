using ErpPurchasing.API.Application.DTOs;

namespace ErpPurchasing.API.Application.Interfaces;

public interface IGoodsReceiptRepository
{
    Task<bool> IsSupplierActiveAsync(int supplierId);
    Task<IReadOnlyCollection<int>> GetActiveWarehouseIdsAsync(IEnumerable<int> warehouseIds);
    Task<int> CreateAsync(CreateGoodsReceiptRequest request, DateTime receiptDate);
    Task ApproveAsync(int goodsReceiptId);
    Task<GoodsReceiptDto?> GetByIdAsync(int goodsReceiptId);
}