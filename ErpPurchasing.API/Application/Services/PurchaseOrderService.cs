using ErpPurchasing.API.Application.DTOs;
using ErpPurchasing.API.Application.Exceptions;
using ErpPurchasing.API.Application.Interfaces;

namespace ErpPurchasing.API.Application.Services;

public sealed class PurchaseOrderService : IPurchaseOrderService
{
    private readonly IPurchaseOrderRepository _repository;

    public PurchaseOrderService(IPurchaseOrderRepository repository)
    {
        _repository = repository;
    }

    public async Task<PurchaseOrderDto> GetByIdAsync(int purchaseOrderId)
    {
        return await _repository.GetByIdAsync(purchaseOrderId)
            ?? throw new NotFoundException($"Id'si {purchaseOrderId} olan sipariş bulunamadı.");
    }

    public async Task<PurchaseOrderDto> CreateAsync(CreatePurchaseOrderRequest request)
    {
        var orderDate = (request.OrderDate ?? DateTime.Today).Date;

        // Kural 1: Tedarikçi var ve aktif olmalı
        if (!await _repository.IsSupplierActiveAsync(request.SupplierId))
            throw new BusinessRuleException($"Id'si {request.SupplierId} olan tedarikçi bulunamadı veya pasif.");

        // Kural 2: Tüm ürünler var ve aktif olmalı
        var requestedIds = request.Lines.Select(l => l.ItemId).Distinct().ToList();
        var activeIds = await _repository.GetActiveItemIdsAsync(requestedIds);
        var invalidIds = requestedIds.Except(activeIds).ToList();
        if (invalidIds.Count > 0)
            throw new BusinessRuleException($"Şu ürünler bulunamadı veya pasif: {string.Join(", ", invalidIds)}");

        // Kural 3: Teslim tarihi sipariş tarihinden önce olamaz
        if (request.Lines.Any(l => l.ExpectedDeliveryDate.HasValue && l.ExpectedDeliveryDate.Value.Date < orderDate))
            throw new BusinessRuleException("Beklenen teslim tarihi sipariş tarihinden önce olamaz.");

        var newId = await _repository.CreateAsync(request, orderDate);
        return await GetByIdAsync(newId);
    }
}