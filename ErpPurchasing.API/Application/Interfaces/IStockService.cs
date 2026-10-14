using ErpPurchasing.API.Application.DTOs;

namespace ErpPurchasing.API.Application.Interfaces;

public interface IStockService
{
    Task<IEnumerable<StockDto>> GetCurrentStockAsync(int? itemId, int? warehouseId);
}