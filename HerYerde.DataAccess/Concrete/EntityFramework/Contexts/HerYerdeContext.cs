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
            e.Property(i => i.SortOrder).HasColumnName("sort_order");
            e.Property(i => i.IsPrimary).HasColumnName("is_primary");
            e.HasOne<Product>().WithMany().HasForeignKey(i => i.ProductId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
