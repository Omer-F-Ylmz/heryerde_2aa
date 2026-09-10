using HerYerde.Entities.Concrete;
using Microsoft.EntityFrameworkCore;

namespace HerYerde.DataAccess.Concrete.EntityFramework.Contexts;

public class HerYerdeContext : DbContext
{
    public HerYerdeContext(DbContextOptions<HerYerdeContext> options) : base(options)
    {
    }

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Category>(e =>
        {
            e.ToTable("category");
            e.HasKey(c => c.Id);
            e.Property(c => c.Id).HasColumnName("id");
            e.Property(c => c.Name).HasColumnName("name").HasMaxLength(120).IsRequired();
            e.Property(c => c.Slug).HasColumnName("slug").HasMaxLength(140).IsRequired();
            e.Property(c => c.ParentId).HasColumnName("parent_id");
            e.Property(c => c.SortOrder).HasColumnName("sort_order");
            e.Property(c => c.IsActive).HasColumnName("is_active");
            e.HasIndex(c => c.Slug).IsUnique().HasDatabaseName("ux_category_slug");
            e.HasOne<Category>().WithMany().HasForeignKey(c => c.ParentId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Product>(e =>
        {
            e.ToTable("product", t => t.HasCheckConstraint(
                "ck_product_campaign_price",
                "[campaign_price] IS NULL OR [campaign_price] < [price]"));
            e.HasKey(p => p.Id);
            e.Property(p => p.Id).HasColumnName("id");
            e.Property(p => p.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
            e.Property(p => p.Slug).HasColumnName("slug").HasMaxLength(200).IsRequired();
            e.Property(p => p.Description).HasColumnName("description").HasColumnType("nvarchar(max)").IsRequired();
            e.Property(p => p.CategoryId).HasColumnName("category_id");
            e.Property(p => p.Price).HasColumnName("price").HasPrecision(18, 2);
            e.Property(p => p.CampaignPrice).HasColumnName("campaign_price").HasPrecision(18, 2);
            e.Property(p => p.CampaignLabel).HasColumnName("campaign_label").HasMaxLength(40);
            e.Property(p => p.CampaignEndsAt).HasColumnName("campaign_ends_at");
            e.Property(p => p.IsActive).HasColumnName("is_active");
            e.Property(p => p.DeletedAt).HasColumnName("deleted_at");
            e.Property(p => p.CreatedAt).HasColumnName("created_at");
            e.Property(p => p.UpdatedAt).HasColumnName("updated_at");
            e.HasIndex(p => p.Slug).IsUnique().HasDatabaseName("ux_product_slug");
            e.HasQueryFilter(p => p.DeletedAt == null);
            e.HasOne<Category>().WithMany().HasForeignKey(p => p.CategoryId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ProductVariant>(e =>
        {
            e.ToTable("product_variant", t => t.HasCheckConstraint("ck_product_variant_stock", "[stock] >= 0"));
            e.HasKey(v => v.Id);
            e.Property(v => v.Id).HasColumnName("id");
            e.Property(v => v.ProductId).HasColumnName("product_id");
            e.Property(v => v.Size).HasColumnName("size").HasMaxLength(16);
            e.Property(v => v.Color).HasColumnName("color").HasMaxLength(60);
            e.Property(v => v.Sku).HasColumnName("sku").HasMaxLength(60).IsRequired();
            e.Property(v => v.Stock).HasColumnName("stock");
            e.HasIndex(v => v.Sku).IsUnique().HasDatabaseName("ux_product_variant_sku");
            e.HasOne<Product>().WithMany().HasForeignKey(v => v.ProductId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProductImage>(e =>
        {
            e.ToTable("product_image");
            e.HasKey(i => i.Id);
            e.Property(i => i.Id).HasColumnName("id");
            e.Property(i => i.ProductId).HasColumnName("product_id");
            e.Property(i => i.Url).HasColumnName("url").HasMaxLength(500).IsRequired();
            e.Property(i => i.Alt).HasColumnName("alt").HasMaxLength(200).IsRequired();
            e.Property(i => i.SortOrder).HasColumnName("sort_order");
            e.Property(i => i.IsPrimary).HasColumnName("is_primary");
            e.HasOne<Product>().WithMany().HasForeignKey(i => i.ProductId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AdminUser>(e =>
        {
            e.ToTable("admin_user");
            e.HasKey(a => a.Id);
            e.Property(a => a.Id).HasColumnName("id");
            e.Property(a => a.Email).HasColumnName("email").HasMaxLength(200).IsRequired();
            e.Property(a => a.PasswordHash).HasColumnName("password_hash").HasMaxLength(400).IsRequired();
            e.Property(a => a.FailedAttempts).HasColumnName("failed_attempts");
            e.Property(a => a.LockedUntil).HasColumnName("locked_until");
            e.HasIndex(a => a.Email).IsUnique().HasDatabaseName("ux_admin_user_email");
        });

        modelBuilder.Entity<Cart>(e =>
        {
            e.ToTable("cart");
            e.HasKey(c => c.Id);
            e.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();
            e.Property(c => c.CreatedAt).HasColumnName("created_at");
        });

        modelBuilder.Entity<CartItem>(e =>
        {
            e.ToTable("cart_item", t => t.HasCheckConstraint("ck_cart_item_quantity", "[quantity] >= 1"));
            e.HasKey(i => i.Id);
            e.Property(i => i.Id).HasColumnName("id");
            e.Property(i => i.CartId).HasColumnName("cart_id");
            e.Property(i => i.ProductId).HasColumnName("product_id");
            e.Property(i => i.VariantId).HasColumnName("variant_id");
            e.Property(i => i.Quantity).HasColumnName("quantity");
            e.Property(i => i.UnitPrice).HasColumnName("unit_price").HasPrecision(18, 2);
            e.HasOne<Cart>().WithMany().HasForeignKey(i => i.CartId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Product>().WithMany().HasForeignKey(i => i.ProductId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<ProductVariant>().WithMany().HasForeignKey(i => i.VariantId).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<Order>(e =>
        {
            e.ToTable("order");
            e.HasKey(o => o.Id);
            e.Property(o => o.Id).HasColumnName("id");
            e.Property(o => o.OrderNo).HasColumnName("order_no").HasMaxLength(20).IsRequired();
            e.Property(o => o.AccessToken).HasColumnName("access_token");
            e.Property(o => o.Status).HasColumnName("status").HasConversion<int>();
            e.Property(o => o.PaymentMethod).HasColumnName("payment_method").HasConversion<int>();
            e.Property(o => o.Subtotal).HasColumnName("subtotal").HasPrecision(18, 2);
            e.Property(o => o.ShippingFee).HasColumnName("shipping_fee").HasPrecision(18, 2);
            e.Property(o => o.Total).HasColumnName("total").HasPrecision(18, 2);
            e.Property(o => o.FullName).HasColumnName("full_name").HasMaxLength(120).IsRequired();
            e.Property(o => o.Phone).HasColumnName("phone").HasMaxLength(11).IsRequired();
            e.Property(o => o.Email).HasColumnName("email").HasMaxLength(200);
            e.Property(o => o.Address).HasColumnName("address").HasMaxLength(500).IsRequired();
            e.Property(o => o.City).HasColumnName("city").HasMaxLength(60).IsRequired();
            e.Property(o => o.District).HasColumnName("district").HasMaxLength(60).IsRequired();
            e.Property(o => o.Note).HasColumnName("note").HasMaxLength(500);
            e.Property(o => o.CreatedAt).HasColumnName("created_at");
            e.HasIndex(o => o.OrderNo).IsUnique().HasDatabaseName("ux_order_order_no");
            e.HasIndex(o => o.AccessToken).IsUnique().HasDatabaseName("ux_order_access_token");
        });

        modelBuilder.Entity<OrderItem>(e =>
        {
            e.ToTable("order_item");
            e.HasKey(i => i.Id);
            e.Property(i => i.Id).HasColumnName("id");
            e.Property(i => i.OrderId).HasColumnName("order_id");
            e.Property(i => i.ProductName).HasColumnName("product_name").HasMaxLength(200).IsRequired();
            e.Property(i => i.Sku).HasColumnName("sku").HasMaxLength(60).IsRequired();
            e.Property(i => i.Quantity).HasColumnName("quantity");
            e.Property(i => i.UnitPrice).HasColumnName("unit_price").HasPrecision(18, 2);
            e.HasOne<Order>().WithMany().HasForeignKey(i => i.OrderId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
