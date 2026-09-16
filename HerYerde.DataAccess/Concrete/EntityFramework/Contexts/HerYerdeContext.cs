using HerYerde.Entities.Concrete;
using Microsoft.EntityFrameworkCore;

namespace HerYerde.DataAccess.Concrete.EntityFramework.Contexts;

public class HerYerdeContext : DbContext
{
    public HerYerdeContext(DbContextOptions<HerYerdeContext> options) : base(options)
    {
    }

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<ProductVideo> ProductVideos => Set<ProductVideo>();
    public DbSet<ProductAttribute> ProductAttributes => Set<ProductAttribute>();
    public DbSet<CategoryAttributeTemplate> CategoryAttributeTemplates => Set<CategoryAttributeTemplate>();
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<AdminAuditLog> AdminAuditLogs => Set<AdminAuditLog>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<ContactMessage> ContactMessages => Set<ContactMessage>();
    public DbSet<ProductReview> ProductReviews => Set<ProductReview>();
    public DbSet<SlugHistory> SlugHistories => Set<SlugHistory>();
    public DbSet<PaymentNotice> PaymentNotices => Set<PaymentNotice>();
    public DbSet<ReturnRequest> ReturnRequests => Set<ReturnRequest>();
    public DbSet<ReturnRequestItem> ReturnRequestItems => Set<ReturnRequestItem>();
    public DbSet<KvkkRequest> KvkkRequests => Set<KvkkRequest>();
    public DbSet<OrderNote> OrderNotes => Set<OrderNote>();
    public DbSet<Announcement> Announcements => Set<Announcement>();
    public DbSet<Coupon> Coupons => Set<Coupon>();
    public DbSet<GiftRegistry> GiftRegistries => Set<GiftRegistry>();
    public DbSet<GiftRegistryItem> GiftRegistryItems => Set<GiftRegistryItem>();
    public DbSet<SearchLog> SearchLogs => Set<SearchLog>();
    public DbSet<PageView> PageViews => Set<PageView>();
    public DbSet<AnalyticsDaily> AnalyticsDailies => Set<AnalyticsDaily>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Sipariş numarasının sırası: eşzamanlı siparişlerde tekrar üretmesin diye veritabanı SEQUENCE'ı.
        modelBuilder.HasSequence<long>("order_no_seq").StartsAt(1).IncrementsBy(1);

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
            e.Property(c => c.ImageUrl).HasColumnName("image_url").HasMaxLength(300);
            e.HasIndex(c => c.Slug).IsUnique().HasDatabaseName("ux_category_slug");
            e.HasOne<Category>().WithMany().HasForeignKey(c => c.ParentId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Brand>(e =>
        {
            e.ToTable("brand");
            e.HasKey(b => b.Id);
            e.Property(b => b.Id).HasColumnName("id");
            e.Property(b => b.Name).HasColumnName("name").HasMaxLength(60).IsRequired();
            e.Property(b => b.Slug).HasColumnName("slug").HasMaxLength(80).IsRequired();
            e.Property(b => b.LogoUrl).HasColumnName("logo_url").HasMaxLength(300);
            e.HasIndex(b => b.Name).IsUnique().HasDatabaseName("ux_brand_name");
            e.HasIndex(b => b.Slug).IsUnique().HasDatabaseName("ux_brand_slug");
        });

        modelBuilder.Entity<Product>(e =>
        {
            e.ToTable("product", t =>
            {
                t.HasCheckConstraint(
                    "ck_product_campaign_price",
                    "[campaign_price] IS NULL OR [campaign_price] < [price]");
                t.HasCheckConstraint("ck_product_stock", "[stock] IS NULL OR [stock] >= 0");
                // Hediye başka bir ürünse hedefi zorunlu, değilse boş kalır.
                t.HasCheckConstraint(
                    "ck_product_gift",
                    "([gift_mode] = 2 AND [gift_product_id] IS NOT NULL) OR ([gift_mode] <> 2 AND [gift_product_id] IS NULL)");
                t.HasCheckConstraint("ck_product_gift_qty", "[gift_qty] >= 1");
            });
            e.HasKey(p => p.Id);
            e.Property(p => p.Id).HasColumnName("id");
            e.Property(p => p.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
            e.Property(p => p.Slug).HasColumnName("slug").HasMaxLength(200).IsRequired();
            e.Property(p => p.Description).HasColumnName("description").HasColumnType("nvarchar(max)").IsRequired();
            e.Property(p => p.CategoryId).HasColumnName("category_id");
            e.Property(p => p.BrandId).HasColumnName("brand_id");
            e.Property(p => p.Price).HasColumnName("price").HasPrecision(18, 2);
            e.Property(p => p.CampaignPrice).HasColumnName("campaign_price").HasPrecision(18, 2);
            e.Property(p => p.CampaignLabel).HasColumnName("campaign_label").HasMaxLength(40);
            e.Property(p => p.Dimensions).HasColumnName("dimensions").HasMaxLength(60);
            e.Property(p => p.VariantAxis1Label).HasColumnName("variant_axis1_label").HasMaxLength(30);
            e.Property(p => p.VariantAxis2Label).HasColumnName("variant_axis2_label").HasMaxLength(30);
            e.Property(p => p.CampaignEndsAt).HasColumnName("campaign_ends_at");
            e.Property(p => p.GiftMode).HasColumnName("gift_mode").HasConversion<int>();
            e.Property(p => p.GiftProductId).HasColumnName("gift_product_id");
            e.Property(p => p.GiftQty).HasColumnName("gift_qty").HasDefaultValue(1);
            e.Property(p => p.Stock).HasColumnName("stock");
            e.Property(p => p.IsActive).HasColumnName("is_active");
            e.Property(p => p.IsFeatured).HasColumnName("is_featured");
            e.Property(p => p.FeaturedOrder).HasColumnName("featured_order");
            e.Property(p => p.DeletedAt).HasColumnName("deleted_at");
            e.Property(p => p.CreatedAt).HasColumnName("created_at");
            e.Property(p => p.UpdatedAt).HasColumnName("updated_at");
            e.HasIndex(p => p.Slug).IsUnique().HasDatabaseName("ux_product_slug");
            // Vitrin listesi: kategori + yayın süzgeci, en yeni önce.
            e.HasIndex(p => new { p.CategoryId, p.IsActive, p.CreatedAt })
                .IsDescending(false, false, true)
                .HasDatabaseName("ix_product_category_id_is_active_created_at");
            e.HasQueryFilter(p => p.DeletedAt == null);
            e.HasOne<Category>().WithMany().HasForeignKey(p => p.CategoryId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Product>().WithMany().HasForeignKey(p => p.GiftProductId).OnDelete(DeleteBehavior.Restrict);
            // Marka sayfası ve marka süzgeci.
            e.HasIndex(p => p.BrandId).HasDatabaseName("ix_product_brand_id");
            e.HasOne<Brand>().WithMany().HasForeignKey(p => p.BrandId).OnDelete(DeleteBehavior.Restrict);
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
            e.Property(v => v.RowVersion).HasColumnName("row_version").IsRowVersion();
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

        modelBuilder.Entity<ProductVideo>(e =>
        {
            e.ToTable("product_video", t => t.HasCheckConstraint("ck_product_video_duration", "[duration] BETWEEN 1 AND 90"));
            e.HasKey(v => v.Id);
            e.Property(v => v.Id).HasColumnName("id");
            e.Property(v => v.ProductId).HasColumnName("product_id");
            e.Property(v => v.Url).HasColumnName("url").HasMaxLength(500).IsRequired();
            e.Property(v => v.PosterUrl).HasColumnName("poster_url").HasMaxLength(500).IsRequired();
            e.Property(v => v.PreviewUrl).HasColumnName("preview_url").HasMaxLength(500).IsRequired();
            e.Property(v => v.Duration).HasColumnName("duration");
            e.Property(v => v.SortOrder).HasColumnName("sort_order");
            e.Property(v => v.CreatedAt).HasColumnName("created_at");
            e.HasIndex(v => v.ProductId).HasDatabaseName("ix_product_video_product_id");
            e.HasOne<Product>().WithMany().HasForeignKey(v => v.ProductId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProductAttribute>(e =>
        {
            e.ToTable("product_attribute");
            e.HasKey(a => a.Id);
            e.Property(a => a.Id).HasColumnName("id");
            e.Property(a => a.ProductId).HasColumnName("product_id");
            e.Property(a => a.Name).HasColumnName("name").HasMaxLength(60).IsRequired();
            e.Property(a => a.Value).HasColumnName("value").HasMaxLength(120).IsRequired();
            e.Property(a => a.SortOrder).HasColumnName("sort_order");
            e.HasIndex(a => new { a.ProductId, a.Name }).IsUnique().HasDatabaseName("ux_product_attribute_product_id_name");
            // Süzgeç: ad + değer ile ürün arama.
            e.HasIndex(a => new { a.Name, a.Value }).HasDatabaseName("ix_product_attribute_name_value");
            e.HasOne<Product>().WithMany().HasForeignKey(a => a.ProductId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CategoryAttributeTemplate>(e =>
        {
            e.ToTable("category_attribute_template");
            e.HasKey(t => t.Id);
            e.Property(t => t.Id).HasColumnName("id");
            e.Property(t => t.CategoryId).HasColumnName("category_id");
            e.Property(t => t.Name).HasColumnName("name").HasMaxLength(60).IsRequired();
            e.Property(t => t.SortOrder).HasColumnName("sort_order");
            e.HasIndex(t => new { t.CategoryId, t.Name }).IsUnique().HasDatabaseName("ux_category_attribute_template_category_id_name");
            e.HasOne<Category>().WithMany().HasForeignKey(t => t.CategoryId).OnDelete(DeleteBehavior.Cascade);
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
            e.Property(a => a.PasswordChangedAt).HasColumnName("password_changed_at");
            e.Property(a => a.MustChangePassword).HasColumnName("must_change_password");
            e.Property(a => a.ResetTokenHash).HasColumnName("reset_token_hash").HasMaxLength(64);
            e.Property(a => a.ResetTokenExpiresAt).HasColumnName("reset_token_expires_at");
            e.Property(a => a.TotpSecret).HasColumnName("totp_secret").HasMaxLength(64);
            e.Property(a => a.TotpEnabled).HasColumnName("totp_enabled");
            e.Property(a => a.TotpLastStep).HasColumnName("totp_last_step");
            e.Property(a => a.RecoveryCodeHashes).HasColumnName("recovery_code_hashes").HasMaxLength(600);
            e.HasIndex(a => a.Email).IsUnique().HasDatabaseName("ux_admin_user_email");
        });

        modelBuilder.Entity<Cart>(e =>
        {
            e.ToTable("cart");
            e.HasKey(c => c.Id);
            e.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();
            e.Property(c => c.CreatedAt).HasColumnName("created_at");
            e.Property(c => c.CouponCode).HasColumnName("coupon_code").HasMaxLength(20);
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
            e.Property(i => i.GiftRegistryItemId).HasColumnName("gift_registry_item_id");
            e.HasOne<Cart>().WithMany().HasForeignKey(i => i.CartId).OnDelete(DeleteBehavior.Cascade);
            // Liste kalemi silinirken sepet satırlarını yönetici önce siler; basamaklı yol ürün silmesiyle çakışırdı.
            e.HasOne<GiftRegistryItem>().WithMany().HasForeignKey(i => i.GiftRegistryItemId).OnDelete(DeleteBehavior.NoAction);
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
            // Eski siparişlerde kargo kuraldan gelmişti: geçiş 0 (false) yazar.
            e.Property(o => o.ShippingOverridden).HasColumnName("shipping_overridden");
            e.Property(o => o.CouponCode).HasColumnName("coupon_code").HasMaxLength(20);
            e.Property(o => o.Discount).HasColumnName("discount").HasPrecision(18, 2);
            e.Property(o => o.Total).HasColumnName("total").HasPrecision(18, 2);
            e.Property(o => o.FullName).HasColumnName("full_name").HasMaxLength(120).IsRequired();
            e.Property(o => o.Phone).HasColumnName("phone").HasMaxLength(11).IsRequired();
            e.Property(o => o.Email).HasColumnName("email").HasMaxLength(200);
            e.Property(o => o.Address).HasColumnName("address").HasMaxLength(500).IsRequired();
            e.Property(o => o.City).HasColumnName("city").HasMaxLength(60).IsRequired();
            e.Property(o => o.District).HasColumnName("district").HasMaxLength(60).IsRequired();
            e.Property(o => o.Note).HasColumnName("note").HasMaxLength(500);
            e.Property(o => o.CreatedAt).HasColumnName("created_at");
            e.Property(o => o.ConsentAt).HasColumnName("consent_at");
            e.Property(o => o.LegalVersion).HasColumnName("legal_version").HasMaxLength(20);
            e.Property(o => o.SeenAt).HasColumnName("seen_at");
            e.Property(o => o.Carrier).HasColumnName("carrier").HasMaxLength(40);
            e.Property(o => o.TrackingNo).HasColumnName("tracking_no").HasMaxLength(60);
            // Bu alan eklenmeden önceki siparişlerin hepsi vitrinden gelmişti: geçiş eski satırlara Site (1) yazar.
            e.Property(o => o.Source).HasColumnName("source").HasConversion<int>();
            e.Property(o => o.InvoiceNo).HasColumnName("invoice_no").HasMaxLength(40);
            e.Property(o => o.InvoiceDate).HasColumnName("invoice_date").HasColumnType("date");
            e.Property(o => o.InvoiceFile).HasColumnName("invoice_file").HasMaxLength(200);
            e.Property(o => o.DeliveredAt).HasColumnName("delivered_at");
            e.Property(o => o.ReviewMailAt).HasColumnName("review_mail_at");
            // Gecelik tarama: teslim edilmiş, daveti işlenmemiş siparişler.
            e.HasIndex(o => new { o.ReviewMailAt, o.DeliveredAt }).HasDatabaseName("ix_order_review_mail_at_delivered_at");
            e.Property(o => o.RefundDue).HasColumnName("refund_due").HasPrecision(18, 2);
            e.Property(o => o.RefundIban).HasColumnName("refund_iban").HasMaxLength(34);
            e.Property(o => o.RefundedAt).HasColumnName("refunded_at");
            e.HasIndex(o => o.OrderNo).IsUnique().HasDatabaseName("ux_order_order_no");
            e.HasIndex(o => o.AccessToken).IsUnique().HasDatabaseName("ux_order_access_token");
            // Yönetim sipariş listesi: duruma göre süzüp en yeniden eskiye.
            e.HasIndex(o => new { o.Status, o.CreatedAt })
                .IsDescending(false, true)
                .HasDatabaseName("ix_order_status_created_at");
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
            e.Property(i => i.IsGift).HasColumnName("is_gift");
            e.Property(i => i.GiftRegistryItemId).HasColumnName("gift_registry_item_id");
            e.HasOne<Order>().WithMany().HasForeignKey(i => i.OrderId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Payment>(e =>
        {
            e.ToTable("payment");
            e.HasKey(p => p.Id);
            e.Property(p => p.Id).HasColumnName("id");
            e.Property(p => p.OrderId).HasColumnName("order_id");
            e.Property(p => p.CartId).HasColumnName("cart_id");
            e.Property(p => p.Provider).HasColumnName("provider").HasMaxLength(20).IsRequired();
            e.Property(p => p.ConversationId).HasColumnName("conversation_id").HasMaxLength(64).IsRequired();
            e.Property(p => p.PaymentId).HasColumnName("payment_id").HasMaxLength(64);
            e.Property(p => p.Status).HasColumnName("status").HasConversion<int>();
            e.Property(p => p.Amount).HasColumnName("amount").HasPrecision(18, 2);
            e.Property(p => p.RawResponse).HasColumnName("raw_response").HasMaxLength(4000);
            e.Property(p => p.CreatedAt).HasColumnName("created_at");
            e.Property(p => p.UpdatedAt).HasColumnName("updated_at");
            e.HasIndex(p => p.ConversationId).IsUnique().HasDatabaseName("ux_payment_conversation_id");
            e.HasIndex(p => p.OrderId).HasDatabaseName("ix_payment_order_id");
            e.HasOne<Order>().WithMany().HasForeignKey(p => p.OrderId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PaymentNotice>(e =>
        {
            e.ToTable("payment_notice");
            e.HasKey(n => n.Id);
            e.Property(n => n.Id).HasColumnName("id");
            e.Property(n => n.OrderId).HasColumnName("order_id");
            e.Property(n => n.SenderName).HasColumnName("sender_name").HasMaxLength(120).IsRequired();
            e.Property(n => n.PaidOn).HasColumnName("paid_on").HasColumnType("date");
            e.Property(n => n.Amount).HasColumnName("amount").HasPrecision(18, 2);
            e.Property(n => n.ReceiptFile).HasColumnName("receipt_file").HasMaxLength(200);
            e.Property(n => n.CreatedAt).HasColumnName("created_at");
            e.Property(n => n.ApprovedAt).HasColumnName("approved_at");
            e.HasIndex(n => n.OrderId).HasDatabaseName("ix_payment_notice_order_id");
            e.HasOne<Order>().WithMany().HasForeignKey(n => n.OrderId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ReturnRequest>(e =>
        {
            e.ToTable("return_request");
            e.HasKey(r => r.Id);
            e.Property(r => r.Id).HasColumnName("id");
            e.Property(r => r.OrderId).HasColumnName("order_id");
            e.Property(r => r.Type).HasColumnName("type").HasConversion<int>();
            e.Property(r => r.Status).HasColumnName("status").HasConversion<int>();
            e.Property(r => r.Reason).HasColumnName("reason").HasMaxLength(1000).IsRequired();
            e.Property(r => r.PhotoFile).HasColumnName("photo_file").HasMaxLength(200);
            e.Property(r => r.RefundIban).HasColumnName("refund_iban").HasMaxLength(34);
            e.Property(r => r.CreatedAt).HasColumnName("created_at");
            e.Property(r => r.DecidedAt).HasColumnName("decided_at");
            e.Property(r => r.RejectReason).HasColumnName("reject_reason").HasMaxLength(500);
            e.Property(r => r.ReceivedAt).HasColumnName("received_at");
            e.Property(r => r.RefundAmount).HasColumnName("refund_amount").HasPrecision(18, 2);
            e.Property(r => r.RefundedAt).HasColumnName("refunded_at");
            e.HasIndex(r => r.OrderId).HasDatabaseName("ix_return_request_order_id");
            // Yönetim listesi ve pano: bekleyen talepler duruma göre sayılır.
            e.HasIndex(r => new { r.Status, r.CreatedAt }).HasDatabaseName("ix_return_request_status_created_at");
            e.HasOne<Order>().WithMany().HasForeignKey(r => r.OrderId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ReturnRequestItem>(e =>
        {
            e.ToTable("return_request_item", t => t.HasCheckConstraint("ck_return_request_item_quantity", "[quantity] >= 1"));
            e.HasKey(i => i.Id);
            e.Property(i => i.Id).HasColumnName("id");
            e.Property(i => i.ReturnRequestId).HasColumnName("return_request_id");
            e.Property(i => i.OrderItemId).HasColumnName("order_item_id");
            e.Property(i => i.Quantity).HasColumnName("quantity");
            e.Property(i => i.NewSku).HasColumnName("new_sku").HasMaxLength(60);
            e.HasOne<ReturnRequest>().WithMany().HasForeignKey(i => i.ReturnRequestId).OnDelete(DeleteBehavior.Cascade);
            // Sipariş silinince talep üzerinden zaten silinir; ikinci basamaklı yol SQL Server'da çakışır.
            e.HasOne<OrderItem>().WithMany().HasForeignKey(i => i.OrderItemId).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<KvkkRequest>(e =>
        {
            e.ToTable("kvkk_request");
            e.HasKey(r => r.Id);
            e.Property(r => r.Id).HasColumnName("id");
            e.Property(r => r.Subject).HasColumnName("subject").HasMaxLength(200).IsRequired();
            e.Property(r => r.ReceivedAt).HasColumnName("received_at");
            e.Property(r => r.CompletedAt).HasColumnName("completed_at");
        });

        modelBuilder.Entity<OrderNote>(e =>
        {
            e.ToTable("order_note");
            e.HasKey(n => n.Id);
            e.Property(n => n.Id).HasColumnName("id");
            e.Property(n => n.OrderId).HasColumnName("order_id");
            e.Property(n => n.AdminId).HasColumnName("admin_id");
            e.Property(n => n.Text).HasColumnName("text").HasMaxLength(1000).IsRequired();
            e.Property(n => n.CreatedAt).HasColumnName("created_at");
            e.HasIndex(n => n.OrderId).HasDatabaseName("ix_order_note_order_id");
            e.HasOne<Order>().WithMany().HasForeignKey(n => n.OrderId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Coupon>(e =>
        {
            e.ToTable("coupon", t =>
            {
                t.HasCheckConstraint("ck_coupon_dates", "[ends_at] > [starts_at]");
                t.HasCheckConstraint("ck_coupon_value", "[value] >= 0 AND [min_subtotal] >= 0");
                t.HasCheckConstraint("ck_coupon_limits", "([total_limit] IS NULL OR [total_limit] >= 1) AND ([per_person_limit] IS NULL OR [per_person_limit] >= 1)");
            });
            e.HasKey(c => c.Id);
            e.Property(c => c.Id).HasColumnName("id");
            e.Property(c => c.Code).HasColumnName("code").HasMaxLength(20).IsRequired();
            e.Property(c => c.Kind).HasColumnName("kind").HasConversion<int>();
            e.Property(c => c.Value).HasColumnName("value").HasPrecision(18, 2);
            e.Property(c => c.MinSubtotal).HasColumnName("min_subtotal").HasPrecision(18, 2);
            e.Property(c => c.StartsAt).HasColumnName("starts_at");
            e.Property(c => c.EndsAt).HasColumnName("ends_at");
            e.Property(c => c.TotalLimit).HasColumnName("total_limit");
            e.Property(c => c.PerPersonLimit).HasColumnName("per_person_limit");
            e.Property(c => c.IsActive).HasColumnName("is_active");
            e.Property(c => c.CreatedAt).HasColumnName("created_at");
            e.HasIndex(c => c.Code).IsUnique().HasDatabaseName("ux_coupon_code");
        });

        modelBuilder.Entity<GiftRegistry>(e =>
        {
            e.ToTable("gift_registry");
            e.HasKey(r => r.Id);
            e.Property(r => r.Id).HasColumnName("id");
            e.Property(r => r.Slug).HasColumnName("slug").HasMaxLength(10).IsRequired();
            e.Property(r => r.ManageToken).HasColumnName("manage_token");
            e.Property(r => r.OwnerName).HasColumnName("owner_name").HasMaxLength(60).IsRequired();
            e.Property(r => r.Phone).HasColumnName("phone").HasMaxLength(11).IsRequired();
            e.Property(r => r.Email).HasColumnName("email").HasMaxLength(200);
            e.Property(r => r.EventDate).HasColumnName("event_date").HasColumnType("date");
            e.Property(r => r.Message).HasColumnName("message").HasMaxLength(500);
            e.Property(r => r.IsPublic).HasColumnName("is_public");
            e.Property(r => r.CreatedAt).HasColumnName("created_at");
            e.HasIndex(r => r.Slug).IsUnique().HasDatabaseName("ux_gift_registry_slug");
            e.HasIndex(r => r.ManageToken).IsUnique().HasDatabaseName("ux_gift_registry_manage_token");
            // Gecelik saklama süresi taraması etkinlik tarihine bakar.
            e.HasIndex(r => r.EventDate).HasDatabaseName("ix_gift_registry_event_date");
        });

        modelBuilder.Entity<GiftRegistryItem>(e =>
        {
            e.ToTable("gift_registry_item", t =>
            {
                t.HasCheckConstraint("ck_gift_registry_item_desired_qty", "[desired_qty] BETWEEN 1 AND 99");
                t.HasCheckConstraint("ck_gift_registry_item_received_qty", "[received_qty] >= 0");
            });
            e.HasKey(i => i.Id);
            e.Property(i => i.Id).HasColumnName("id");
            e.Property(i => i.GiftRegistryId).HasColumnName("gift_registry_id");
            e.Property(i => i.ProductId).HasColumnName("product_id");
            e.Property(i => i.VariantId).HasColumnName("variant_id");
            e.Property(i => i.DesiredQty).HasColumnName("desired_qty");
            e.Property(i => i.ReceivedQty).HasColumnName("received_qty");
            // Aynı ürün-varyant listede bir kez; adet satırın kendisinde artar.
            e.HasIndex(i => new { i.GiftRegistryId, i.ProductId, i.VariantId }).IsUnique()
                .HasDatabaseName("ux_gift_registry_item_gift_registry_id_product_id_variant_id");
            e.HasOne<GiftRegistry>().WithMany().HasForeignKey(i => i.GiftRegistryId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Product>().WithMany().HasForeignKey(i => i.ProductId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne<ProductVariant>().WithMany().HasForeignKey(i => i.VariantId).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<Announcement>(e =>
        {
            e.ToTable("announcement", t => t.HasCheckConstraint("ck_announcement_dates", "[ends_at] > [starts_at]"));
            e.HasKey(a => a.Id);
            e.Property(a => a.Id).HasColumnName("id");
            e.Property(a => a.Text).HasColumnName("text").HasMaxLength(200).IsRequired();
            e.Property(a => a.Url).HasColumnName("url").HasMaxLength(200);
            e.Property(a => a.StartsAt).HasColumnName("starts_at");
            e.Property(a => a.EndsAt).HasColumnName("ends_at");
            e.Property(a => a.Color).HasColumnName("color").HasConversion<int>();
            e.Property(a => a.IsActive).HasColumnName("is_active");
            e.Property(a => a.CreatedAt).HasColumnName("created_at");
            // Her sayfa çiziminde geçerli duyuru sorulur: etkin + tarih aralığı.
            e.HasIndex(a => new { a.IsActive, a.StartsAt, a.EndsAt }).HasDatabaseName("ix_announcement_is_active_starts_at_ends_at");
        });

        modelBuilder.Entity<OutboxMessage>(e =>
        {
            e.ToTable("outbox_message");
            e.HasKey(m => m.Id);
            e.Property(m => m.Id).HasColumnName("id");
            e.Property(m => m.Type).HasColumnName("type").HasMaxLength(30).IsRequired();
            e.Property(m => m.To).HasColumnName("to").HasMaxLength(200).IsRequired();
            e.Property(m => m.Subject).HasColumnName("subject").HasMaxLength(200).IsRequired();
            e.Property(m => m.Body).HasColumnName("body").HasColumnType("nvarchar(max)").IsRequired();
            e.Property(m => m.Status).HasColumnName("status").HasConversion<int>();
            e.Property(m => m.TryCount).HasColumnName("try_count");
            // Değer verilmeyen eski kayıtlar ve testlerin elle eklediği satırlar sunucu saatini alır.
            e.Property(m => m.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("SYSUTCDATETIME()");
            e.Property(m => m.NextTryAt).HasColumnName("next_try_at");
            e.Property(m => m.SentAt).HasColumnName("sent_at");
            e.Property(m => m.Attachment).HasColumnName("attachment").HasMaxLength(200);
            // Dağıtım turu: yalnız bekleyen ve sırası gelmiş kayıtlar okunur.
            e.HasIndex(m => new { m.Status, m.NextTryAt }).HasDatabaseName("ix_outbox_message_status_next_try_at");
        });

        modelBuilder.Entity<SlugHistory>(e =>
        {
            e.ToTable("slug_history");
            e.HasKey(h => h.Id);
            e.Property(h => h.Id).HasColumnName("id");
            e.Property(h => h.EntityType).HasColumnName("entity_type").HasMaxLength(20).IsRequired();
            e.Property(h => h.EntityId).HasColumnName("entity_id");
            e.Property(h => h.OldSlug).HasColumnName("old_slug").HasMaxLength(220).IsRequired();
            e.Property(h => h.CreatedAt).HasColumnName("created_at");
            e.HasIndex(h => new { h.EntityType, h.OldSlug }).IsUnique().HasDatabaseName("ux_slug_history_entity_type_old_slug");
        });

        modelBuilder.Entity<PageView>(e =>
        {
            e.ToTable("page_view");
            e.HasKey(v => v.Id);
            e.Property(v => v.Id).HasColumnName("id");
            e.Property(v => v.Event).HasColumnName("event").HasMaxLength(20).IsRequired();
            e.Property(v => v.Path).HasColumnName("path").HasMaxLength(200).IsRequired();
            e.Property(v => v.ReferrerHost).HasColumnName("referrer_host").HasMaxLength(100);
            e.Property(v => v.UtmSource).HasColumnName("utm_source").HasMaxLength(60);
            e.Property(v => v.UtmMedium).HasColumnName("utm_medium").HasMaxLength(60);
            e.Property(v => v.UtmCampaign).HasColumnName("utm_campaign").HasMaxLength(60);
            e.Property(v => v.Device).HasColumnName("device").HasMaxLength(10).IsRequired();
            e.Property(v => v.Day).HasColumnName("day").HasColumnType("date");
            e.Property(v => v.Hour).HasColumnName("hour");
            e.Property(v => v.HalfHour).HasColumnName("half_hour");
            // Rapor gün aralığıyla süzer; özetleme de gün sınırıyla toplayıp siler.
            e.HasIndex(v => new { v.Day, v.Event }).HasDatabaseName("ix_page_view_day_event");
        });

        modelBuilder.Entity<AnalyticsDaily>(e =>
        {
            e.ToTable("analytics_daily");
            e.HasKey(d => d.Id);
            e.Property(d => d.Id).HasColumnName("id");
            e.Property(d => d.Day).HasColumnName("day").HasColumnType("date");
            e.Property(d => d.Event).HasColumnName("event").HasMaxLength(20).IsRequired();
            e.Property(d => d.Path).HasColumnName("path").HasMaxLength(200).IsRequired();
            e.Property(d => d.Source).HasColumnName("source").HasMaxLength(100).IsRequired();
            e.Property(d => d.Device).HasColumnName("device").HasMaxLength(10).IsRequired();
            e.Property(d => d.Views).HasColumnName("views");
            e.Property(d => d.Visits).HasColumnName("visits");
            e.HasIndex(d => new { d.Day, d.Event, d.Path, d.Source, d.Device }).IsUnique().HasDatabaseName("ux_analytics_daily_key");
        });

        modelBuilder.Entity<SearchLog>(e =>
        {
            e.ToTable("search_log");
            e.HasKey(l => l.Id);
            e.Property(l => l.Id).HasColumnName("id");
            e.Property(l => l.Term).HasColumnName("term").HasMaxLength(60).IsRequired();
            e.Property(l => l.ResultCount).HasColumnName("result_count");
            e.Property(l => l.CreatedAt).HasColumnName("created_at");
            // Rapor pencereyle süzüp terime göre gruplar; temizlik tarihe göre siler.
            e.HasIndex(l => new { l.CreatedAt, l.Term }).HasDatabaseName("ix_search_log_created_at_term");
        });

        modelBuilder.Entity<ContactMessage>(e =>
        {
            e.ToTable("contact_message");
            e.HasKey(m => m.Id);
            e.Property(m => m.Id).HasColumnName("id");
            e.Property(m => m.Name).HasColumnName("name").HasMaxLength(120).IsRequired();
            e.Property(m => m.Contact).HasColumnName("contact").HasMaxLength(200).IsRequired();
            e.Property(m => m.Subject).HasColumnName("subject").HasConversion<int>();
            e.Property(m => m.Message).HasColumnName("message").HasMaxLength(2000).IsRequired();
            e.Property(m => m.CreatedAt).HasColumnName("created_at");
            e.Property(m => m.ReadAt).HasColumnName("read_at");
            // Yönetim listesi: en yeniden eskiye.
            e.HasIndex(m => m.CreatedAt).IsDescending().HasDatabaseName("ix_contact_message_created_at");
        });

        modelBuilder.Entity<ProductReview>(e =>
        {
            e.ToTable("product_review", t => t.HasCheckConstraint("ck_product_review_rating", "[rating] BETWEEN 1 AND 5"));
            e.HasKey(r => r.Id);
            e.Property(r => r.Id).HasColumnName("id");
            e.Property(r => r.ProductId).HasColumnName("product_id");
            e.Property(r => r.Name).HasColumnName("name").HasMaxLength(60).IsRequired();
            e.Property(r => r.Rating).HasColumnName("rating");
            e.Property(r => r.Comment).HasColumnName("comment").HasMaxLength(1000).IsRequired();
            e.Property(r => r.IsApproved).HasColumnName("is_approved");
            e.Property(r => r.CreatedAt).HasColumnName("created_at");
            e.Property(r => r.OrderNo).HasColumnName("order_no").HasMaxLength(20);
            // Ürün sayfası: ürünün onaylı yorumları, en yeni önce.
            e.HasIndex(r => new { r.ProductId, r.IsApproved, r.CreatedAt })
                .IsDescending(false, false, true)
                .HasDatabaseName("ix_product_review_product_id_is_approved_created_at");
            e.HasOne<Product>().WithMany().HasForeignKey(r => r.ProductId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AdminAuditLog>(e =>
        {
            e.ToTable("admin_audit_log");
            e.HasKey(a => a.Id);
            e.Property(a => a.Id).HasColumnName("id");
            e.Property(a => a.AdminId).HasColumnName("admin_id");
            e.Property(a => a.Action).HasColumnName("action").HasMaxLength(60).IsRequired();
            e.Property(a => a.Entity).HasColumnName("entity").HasMaxLength(30).IsRequired();
            e.Property(a => a.EntityId).HasColumnName("entity_id");
            e.Property(a => a.At).HasColumnName("at");
            e.Property(a => a.Ip).HasColumnName("ip").HasMaxLength(45);
            e.Property(a => a.Detail).HasColumnName("detail").HasMaxLength(2000);
            // Denetim listesi: en yeniden eskiye.
            e.HasIndex(a => a.At).IsDescending().HasDatabaseName("ix_admin_audit_log_at");
        });
    }
}
