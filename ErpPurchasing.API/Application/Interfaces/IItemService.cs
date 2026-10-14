using ErpPurchasing.API.Application.DTOs;

namespace ErpPurchasing.API.Application.Interfaces;

// İş mantığı sözleşmesi: Controller SADECE bunu tanır
public interface IItemService
{
    Task<IEnumerable<ItemDto>> GetAllAsync();
    Task<ItemDto> GetByIdAsync(int itemId);
    Task<ItemDto> CreateAsync(CreateItemRequest request);
}