using Autofac;
using HerYerde.Business.Abstract;
using HerYerde.Business.Concrete;
using HerYerde.Core.DataAccess;
using HerYerde.DataAccess.Abstract;
using HerYerde.DataAccess.Concrete.EntityFramework;

namespace HerYerde.Business.DependencyResolvers.Autofac;

public class AutofacBusinessModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<EfUnitOfWork>().As<IUnitOfWork>().InstancePerLifetimeScope();

        builder.RegisterType<EfCategoryDal>().As<ICategoryDal>().InstancePerLifetimeScope();
        builder.RegisterType<EfProductDal>().As<IProductDal>().InstancePerLifetimeScope();
        builder.RegisterType<EfProductVariantDal>().As<IProductVariantDal>().InstancePerLifetimeScope();
        builder.RegisterType<EfProductImageDal>().As<IProductImageDal>().InstancePerLifetimeScope();
        builder.RegisterType<EfProductVideoDal>().As<IProductVideoDal>().InstancePerLifetimeScope();
        builder.RegisterType<EfAdminUserDal>().As<IAdminUserDal>().InstancePerLifetimeScope();
        builder.RegisterType<EfCartDal>().As<ICartDal>().InstancePerLifetimeScope();
        builder.RegisterType<EfCartItemDal>().As<ICartItemDal>().InstancePerLifetimeScope();
        builder.RegisterType<EfOrderDal>().As<IOrderDal>().InstancePerLifetimeScope();
        builder.RegisterType<EfOrderItemDal>().As<IOrderItemDal>().InstancePerLifetimeScope();
        builder.RegisterType<EfAdminAuditLogDal>().As<IAdminAuditLogDal>().InstancePerLifetimeScope();
        builder.RegisterType<EfOutboxMessageDal>().As<IOutboxMessageDal>().InstancePerLifetimeScope();
        builder.RegisterType<EfPaymentDal>().As<IPaymentDal>().InstancePerLifetimeScope();
        builder.RegisterType<EfContactMessageDal>().As<IContactMessageDal>().InstancePerLifetimeScope();
        builder.RegisterType<EfProductReviewDal>().As<IProductReviewDal>().InstancePerLifetimeScope();
        builder.RegisterType<EfSlugHistoryDal>().As<ISlugHistoryDal>().InstancePerLifetimeScope();
        builder.RegisterType<EfPaymentNoticeDal>().As<IPaymentNoticeDal>().InstancePerLifetimeScope();
        builder.RegisterType<EfReturnRequestDal>().As<IReturnRequestDal>().InstancePerLifetimeScope();
        builder.RegisterType<EfReturnRequestItemDal>().As<IReturnRequestItemDal>().InstancePerLifetimeScope();
        builder.RegisterType<EfKvkkRequestDal>().As<IKvkkRequestDal>().InstancePerLifetimeScope();
        builder.RegisterType<EfOrderNoteDal>().As<IOrderNoteDal>().InstancePerLifetimeScope();
        builder.RegisterType<EfAnnouncementDal>().As<IAnnouncementDal>().InstancePerLifetimeScope();
        builder.RegisterType<EfCouponDal>().As<ICouponDal>().InstancePerLifetimeScope();
        builder.RegisterType<EfGiftRegistryDal>().As<IGiftRegistryDal>().InstancePerLifetimeScope();
        builder.RegisterType<EfGiftRegistryItemDal>().As<IGiftRegistryItemDal>().InstancePerLifetimeScope();

        builder.RegisterType<AdminAuthManager>().As<IAdminAuthService>().InstancePerLifetimeScope();
        builder.RegisterType<CategoryManager>().As<ICategoryService>().InstancePerLifetimeScope();
        builder.RegisterType<ProductManager>().As<IProductService>().InstancePerLifetimeScope();
        builder.RegisterType<CartManager>().As<ICartService>().InstancePerLifetimeScope();
        builder.RegisterType<OrderManager>().As<IOrderService>().InstancePerLifetimeScope();
        builder.RegisterType<AdminAuditManager>().As<IAdminAuditService>().InstancePerLifetimeScope();
        builder.RegisterType<NotificationManager>().As<INotificationService>().InstancePerLifetimeScope();
        builder.RegisterType<PaymentManager>().As<IPaymentService>().InstancePerLifetimeScope();
        builder.RegisterType<ContactManager>().As<IContactService>().InstancePerLifetimeScope();
        builder.RegisterType<ReviewManager>().As<IReviewService>().InstancePerLifetimeScope();
        builder.RegisterType<ReportManager>().As<IReportService>().InstancePerLifetimeScope();
        builder.RegisterType<ProductTransferManager>().As<IProductTransferService>().InstancePerLifetimeScope();
        builder.RegisterType<ReturnManager>().As<IReturnService>().InstancePerLifetimeScope();
        builder.RegisterType<KvkkManager>().As<IKvkkService>().InstancePerLifetimeScope();
        builder.RegisterType<DashboardManager>().As<IDashboardService>().InstancePerLifetimeScope();
        builder.RegisterType<OrderTimelineManager>().As<IOrderTimelineService>().InstancePerLifetimeScope();
        builder.RegisterType<InstallmentManager>().As<IInstallmentService>().InstancePerLifetimeScope();
        builder.RegisterType<AnnouncementManager>().As<IAnnouncementService>().InstancePerLifetimeScope();
        builder.RegisterType<CouponManager>().As<ICouponService>().InstancePerLifetimeScope();
        builder.RegisterType<GiftRegistryManager>().As<IGiftRegistryService>().InstancePerLifetimeScope();
        // Taksit önbelleği istekler arası yaşamalı; yöneticinin kendisi istek kapsamında kalır.
        builder.RegisterType<InstallmentCache>().AsSelf().SingleInstance();
    }
}
