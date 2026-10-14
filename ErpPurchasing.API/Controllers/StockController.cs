using ErpPurchasing.API.Application.DTOs;
using ErpPurchasing.API.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ErpPurchasing.API.Controllers;

[ApiController]
[Route("api/stock")]
[Produces("application/json")]
public sealed class StockController : ControllerBase
{
    private readonly IStockService _service;

    public StockController(IStockService service)
    {
        _service = service;
    }

    /// Ürün ve depo bazında güncel stok. İsteğe bağlı filtreler: itemId, warehouseId
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<StockDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get([FromQuery] int? itemId, [FromQuery] int? warehouseId)
        => Ok(await _service.GetCurrentStockAsync(itemId, warehouseId));
}