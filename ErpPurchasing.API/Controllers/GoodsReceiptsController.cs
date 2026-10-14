using ErpPurchasing.API.Application.DTOs;
using ErpPurchasing.API.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ErpPurchasing.API.Controllers;

[ApiController]
[Route("api/goods-receipts")]
[Produces("application/json")]
public sealed class GoodsReceiptsController : ControllerBase
{
    private readonly IGoodsReceiptService _service;

    public GoodsReceiptsController(IGoodsReceiptService service)
    {
        _service = service;
    }

    /// Mal kabul oluşturur. approve=true ise onaylar, stoğa işler ve sipariş durumunu günceller.
    [HttpPost]
    [ProducesResponseType(typeof(GoodsReceiptDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create([FromBody] CreateGoodsReceiptRequest request)
    {
        var receipt = await _service.CreateAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = receipt.GoodsReceiptId }, receipt);
    }

    /// Mal kabulü satırlarıyla birlikte getirir
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(GoodsReceiptDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
        => Ok(await _service.GetByIdAsync(id));

    /// Taslak mal kabulü onaylar ve stoğa işler
    [HttpPost("{id:int}/approve")]
    [ProducesResponseType(typeof(GoodsReceiptDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Approve(int id)
        => Ok(await _service.ApproveAsync(id));
}