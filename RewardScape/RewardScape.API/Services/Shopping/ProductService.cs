using Microsoft.EntityFrameworkCore;
using SpendWell.Api.Common;
using SpendWell.Api.Data;
using SpendWell.Api.DTOs;
using SpendWell.Api.Models;

namespace SpendWell.Api.Services;

public interface IProductService
{
    Task<PagedResult<ProductDto>> SearchAsync(ProductQueryDto query);
    Task<ProductDto> GetAsync(int productId);
    Task<ProductDto> CreateAsync(CreateProductDto dto);
    Task<ProductDto> UpdateAsync(int productId, UpdateProductDto dto);
    Task<ProductDto> RestockAsync(int productId, RestockDto dto);
}

public class ProductService : IProductService
{
    private readonly SpendWellDbContext _db;

    public ProductService(SpendWellDbContext db)
    {
        _db = db;
    }

    /// <summary>Shop search. Only active products are shown to members.</summary>
    public async Task<PagedResult<ProductDto>> SearchAsync(ProductQueryDto query)
    {
        int page = Math.Max(query.Page, 1);
        int pageSize = Math.Clamp(query.PageSize, 1, 100);

        var products = _db.Products.Where(p => p.IsActive);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            string term = query.Search.Trim();
            products = products.Where(p => p.Name.Contains(term) || p.Sku.Contains(term));
        }

        if (query.Category.HasValue)
        {
            products = products.Where(p => p.Category == query.Category.Value);
        }

        if (query.MinPrice.HasValue)
        {
            products = products.Where(p => p.Price >= query.MinPrice.Value);
        }

        if (query.MaxPrice.HasValue)
        {
            products = products.Where(p => p.Price <= query.MaxPrice.Value);
        }

        if (query.InStockOnly)
        {
            products = products.Where(p => p.StockQuantity > 0);
        }

        int total = await products.CountAsync();

        var list = await products
            .OrderBy(p => p.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<ProductDto>(list.Select(p => p.ToDto()).ToList(), page, pageSize, total);
    }

    public async Task<ProductDto> GetAsync(int productId)
    {
        var product = await FindAsync(productId);
        return product.ToDto();
    }

    public async Task<ProductDto> CreateAsync(CreateProductDto dto)
    {
        string sku = dto.Sku.Trim().ToUpperInvariant();

        bool skuTaken = await _db.Products.AnyAsync(p => p.Sku == sku);
        if (skuTaken)
        {
            throw new ConflictException(string.Format("A product with SKU {0} already exists.", sku));
        }

        var product = new Product
        {
            Sku = sku,
            Name = dto.Name.Trim(),
            Description = dto.Description,
            Category = dto.Category,
            Price = dto.Price,
            StockQuantity = dto.StockQuantity,
            ImageUrl = dto.ImageUrl,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        return product.ToDto();
    }

    /// <summary>Price changes apply to carts immediately; existing orders keep the price they paid.</summary>
    public async Task<ProductDto> UpdateAsync(int productId, UpdateProductDto dto)
    {
        var product = await FindAsync(productId);

        product.Name = dto.Name.Trim();
        product.Description = dto.Description;
        product.Category = dto.Category;
        product.Price = dto.Price;
        product.ImageUrl = dto.ImageUrl;
        product.IsActive = dto.IsActive;

        await _db.SaveChangesAsync();
        return product.ToDto();
    }

    public async Task<ProductDto> RestockAsync(int productId, RestockDto dto)
    {
        var product = await FindAsync(productId);

        product.StockQuantity += dto.Quantity;
        await _db.SaveChangesAsync();

        return product.ToDto();
    }

    private async Task<Product> FindAsync(int productId)
    {
        return await _db.Products.FirstOrDefaultAsync(p => p.ProductId == productId)
            ?? throw NotFoundException.For("Product", productId);
    }
}
