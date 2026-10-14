using ErpPurchasing.API.Application.DTOs;
using ErpPurchasing.API.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ErpPurchasing.API.Controllers;

[ApiController]
[Route("api/items")]
[Produces("application/json")]
public sealed class ItemsController : ControllerBase
{
    private readonly IItemService _itemService;

    public ItemsController(IItemService itemService)
    {
        _itemService = itemService;
    }

    /// Tüm ürünleri listeler
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
        => Ok(await _itemService.GetAllAsync());

    /// Id'ye göre tek bir ürün getirir
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
        => Ok(await _itemService.GetByIdAsync(id));

    /// Yeni ürün kartı oluşturur
    [HttpPost]
    [ProducesResponseType(typeof(ItemDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create([FromBody] CreateItemRequest request)
    {
        var item = await _itemService.CreateAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = item.ItemId }, item);
    }
}