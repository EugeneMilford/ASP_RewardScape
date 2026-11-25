using Microsoft.AspNetCore.Mvc;
using SpendWell.Api.DTOs;
using SpendWell.Api.Services;

namespace SpendWell.Api.Controllers;

/// <summary>The online shop catalogue. Adding and editing products is under /api/admin/products.</summary>
[ApiController]
[Route("api/products")]
[Produces("application/json")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _products;

    public ProductsController(IProductService products)
    {
        _products = products;
    }

    /// <summary>Search the shop: ?search=milk&amp;category=Groceries&amp;maxPrice=50&amp;inStockOnly=true</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<ProductDto>>> Search([FromQuery] ProductQueryDto query)
    {
        return Ok(await _products.SearchAsync(query));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDto>> Get(int id)
    {
        return Ok(await _products.GetAsync(id));
    }
}
