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
        await context.OrderItems.ExecuteDeleteAsync();
        await context.Orders.ExecuteDeleteAsync();
        await context.CartItems.ExecuteDeleteAsync();
        await context.Carts.ExecuteDeleteAsync();
        await context.ProductImages.ExecuteDeleteAsync();
        await context.ProductVariants.ExecuteDeleteAsync();
        await context.Products.IgnoreQueryFilters().ExecuteDeleteAsync();
        await context.Categories.ExecuteDeleteAsync();
        await context.AdminAuditLogs.ExecuteDeleteAsync();
        await context.AdminUsers.ExecuteDeleteAsync();
    }
}
