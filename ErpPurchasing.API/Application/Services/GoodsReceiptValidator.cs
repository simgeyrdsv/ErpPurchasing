using ErpPurchasing.API.Application.DTOs;
using ErpPurchasing.API.Application.Exceptions;
using ErpPurchasing.API.Application.Interfaces;
using ErpPurchasing.API.Domain.Enums;

namespace ErpPurchasing.API.Application.Services;

// Saf iş kuralları: Veritabanını tanımaz, sadece veriye bakıp karar verir
public sealed class GoodsReceiptValidator : IGoodsReceiptValidator
{
    private static readonly PurchaseOrderStatus[] ReceivableStatuses =
        { PurchaseOrderStatus.Open, PurchaseOrderStatus.PartiallyReceived };

    public void Validate(int supplierId,
                         IReadOnlyCollection<ReceiptLineInput> lines,
                         IReadOnlyDictionary<int, OrderLineSnapshot> orderLines)
    {
        var errors = new List<string>();

        // Aynı sipariş satırı mal kabulde birden fazla kez geçebilir (farklı depolara) → grupla
        foreach (var group in lines.GroupBy(l => l.PurchaseOrderLineId))
        {
            if (!orderLines.TryGetValue(group.Key, out var orderLine))
            {
                errors.Add($"Sipariş satırı bulunamadı: {group.Key}.");
                continue;
            }

            // Kural 1: Sipariş bu tedarikçiye ait olmalı
            if (orderLine.SupplierId != supplierId)
                errors.Add($"{orderLine.OrderNumber} siparişi bu tedarikçiye ait değil.");

            // Kural 2: Sipariş mal kabul yapılabilir durumda olmalı
            if (!ReceivableStatuses.Contains((PurchaseOrderStatus)orderLine.OrderStatusId))
                errors.Add($"{orderLine.OrderNumber} siparişi '{orderLine.OrderStatusName}' durumunda, mal kabul yapılamaz.");

            // Kural 3: Fazla teslimat yasak
            var incoming = group.Sum(l => l.Quantity);
            var remaining = orderLine.OrderedQuantity - orderLine.ReceivedQuantity;
            if (incoming > remaining)
                errors.Add($"{orderLine.OrderNumber} / {orderLine.ItemName}: Kalan miktar {remaining:N0}, " +
                           $"kabul edilmek istenen {incoming:N0}. Fazla teslimat kabul edilmez.");
        }

        if (errors.Count > 0)
            throw new BusinessRuleException(string.Join(" | ", errors));
    }
}