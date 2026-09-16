using HerYerde.DataAccess.Concrete.EntityFramework.Contexts;
using Microsoft.EntityFrameworkCore;

namespace HerYerde.Tests;

public static class TestDb
{
    private const string LocalDbConnection =
        @"Server=(localdb)\MSSQLLocalDB;Database=HerYerdeTest;Trusted_Connection=True;TrustServerCertificate=True";

    public static string ConnectionString { get; } =
        Environment.GetEnvironmentVariable("ConnectionStrings__Default") ?? LocalDbConnection;

    public static DbContextOptions<HerYerdeContext> Options => new DbContextOptionsBuilder<HerYerdeContext>()
        .UseSqlServer(ConnectionString)
        .Options;

    public static HerYerdeContext NewContext() => new(Options);

    public static async Task ResetAsync()
    {
        await using var context = NewContext();
        await context.Database.MigrateAsync();
        await context.OutboxMessages.ExecuteDeleteAsync();
        await context.ContactMessages.ExecuteDeleteAsync();
        await context.SearchLogs.ExecuteDeleteAsync();
        await context.PageViews.ExecuteDeleteAsync();
        await context.AnalyticsDailies.ExecuteDeleteAsync();
        await context.ProductReviews.ExecuteDeleteAsync();
        await context.KvkkRequests.ExecuteDeleteAsync();
        await context.Announcements.ExecuteDeleteAsync();
        await context.Coupons.ExecuteDeleteAsync();
        await context.OrderNotes.ExecuteDeleteAsync();
        await context.ReturnRequestItems.ExecuteDeleteAsync();
        await context.ReturnRequests.ExecuteDeleteAsync();
        await context.Payments.ExecuteDeleteAsync();
        await context.OrderItems.ExecuteDeleteAsync();
        await context.Orders.ExecuteDeleteAsync();
        await context.CartItems.ExecuteDeleteAsync();
        await context.Carts.ExecuteDeleteAsync();
        await context.GiftRegistryItems.ExecuteDeleteAsync();
        await context.GiftRegistries.ExecuteDeleteAsync();
        await context.ProductImages.ExecuteDeleteAsync();
        await context.ProductVideos.ExecuteDeleteAsync();
        await context.ProductAttributes.ExecuteDeleteAsync();
        await context.CategoryAttributeTemplates.ExecuteDeleteAsync();
        await context.ProductVariants.ExecuteDeleteAsync();
        await context.Products.IgnoreQueryFilters().ExecuteDeleteAsync();
        await context.Brands.ExecuteDeleteAsync();
        await context.SlugHistories.ExecuteDeleteAsync();
        await context.Categories.ExecuteDeleteAsync();
        await context.AdminAuditLogs.ExecuteDeleteAsync();
        await context.AdminUsers.ExecuteDeleteAsync();
    }
}
