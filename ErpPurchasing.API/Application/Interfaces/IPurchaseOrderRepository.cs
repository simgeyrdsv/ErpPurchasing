using ErpPurchasing.API.Application.DTOs;

namespace ErpPurchasing.API.Application.Interfaces;

public interface IPurchaseOrderRepository
{
    Task<bool> IsSupplierActiveAsync(int supplierId);
    Task<IReadOnlyCollection<int>> GetActiveItemIdsAsync(IEnumerable<int> itemIds);
    Task<int> CreateAsync(CreatePurchaseOrderRequest request, DateTime orderDate);
    Task<PurchaseOrderDto?> GetByIdAsync(int purchaseOrderId);
}
