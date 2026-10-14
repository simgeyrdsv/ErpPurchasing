using ErpPurchasing.API.Application.DTOs;
using ErpPurchasing.API.Application.Exceptions;
using ErpPurchasing.API.Application.Interfaces;

namespace ErpPurchasing.API.Application.Services;

public sealed class ItemService : IItemService
{
    private readonly IItemRepository _itemRepository;

    public ItemService(IItemRepository itemRepository)
    {
        _itemRepository = itemRepository;
    }

    public Task<IEnumerable<ItemDto>> GetAllAsync() => _itemRepository.GetAllAsync();

    public async Task<ItemDto> GetByIdAsync(int itemId)
    {
        return await _itemRepository.GetByIdAsync(itemId)
            ?? throw new NotFoundException($"Id'si {itemId} olan ürün bulunamadı.");
    }

    public async Task<ItemDto> CreateAsync(CreateItemRequest request)
    {
        // İş kuralı: Birim tanımlı olmalı
        if (!await _itemRepository.UnitExistsAsync(request.UnitId))
            throw new BusinessRuleException($"Id'si {request.UnitId} olan birim tanımlı değil.");

        var newId = await _itemRepository.CreateAsync(request);
        return await GetByIdAsync(newId);
    }
}