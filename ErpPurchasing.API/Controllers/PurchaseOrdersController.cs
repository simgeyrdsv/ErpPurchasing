using ErpPurchasing.API.Application.DTOs;
using ErpPurchasing.API.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ErpPurchasing.API.Controllers;

[ApiController]
[Route("api/purchase-orders")]
[Produces("application/json")]
public sealed class PurchaseOrdersController : ControllerBase
{
    private readonly IPurchaseOrderService _service;

    public PurchaseOrdersController(IPurchaseOrderService service)
    {
        _service = service;
    }

    /// Yeni satınalma siparişi oluşturur (başlık + satırlar tek işlemde)
    [HttpPost]
    [ProducesResponseType(typeof(PurchaseOrderDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create([FromBody] CreatePurchaseOrderRequest request)
    {
        var order = await _service.CreateAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = order.PurchaseOrderId }, order);
    }

    /// Siparişi satırları ve teslimat durumuyla birlikte getirir
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(PurchaseOrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
        => Ok(await _service.GetByIdAsync(id));
}
