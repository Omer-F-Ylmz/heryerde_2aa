using HerYerde.DataAccess.Concrete.EntityFramework;
using HerYerde.DataAccess.Concrete.EntityFramework.Contexts;
using HerYerde.Entities.Concrete;

namespace HerYerde.Tests;

public static class TestData
{
    public static async Task<int> AddRootCategoryAsync(HerYerdeContext context, string name = "Giyim", string slug = "giyim")
    {
        var category = new Category { Name = name, Slug = slug, SortOrder = 1, IsActive = true };
        await new EfCategoryDal(context).AddAsync(category);
        await new EfUnitOfWork(context).SaveChangesAsync();
        return category.Id;
    }

    public static async Task<int> AddProductAsync(HerYerdeContext context, int categoryId, string name, string slug)
    {
        var product = NewProduct(categoryId, name, slug);
        await new EfProductDal(context).AddAsync(product);
        await new EfUnitOfWork(context).SaveChangesAsync();
        return product.Id;
    }

    public static Product NewProduct(int categoryId, string name, string slug) => new()
    {
        Name = name,
        Slug = slug,
        Description = "Pamuklu, beli lastikli.",
        CategoryId = categoryId,
        Price = 450m,
        IsActive = true,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };
}
