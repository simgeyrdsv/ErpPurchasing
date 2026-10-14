using ErpPurchasing.API.Application.DTOs;

namespace ErpPurchasing.API.Application.Interfaces;

public interface IPurchaseOrderService
{
    Task<PurchaseOrderDto> CreateAsync(CreatePurchaseOrderRequest request);
    Task<PurchaseOrderDto> GetByIdAsync(int purchaseOrderId);
}
