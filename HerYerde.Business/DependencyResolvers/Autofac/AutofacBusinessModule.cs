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
        builder.RegisterType<EfAdminUserDal>().As<IAdminUserDal>().InstancePerLifetimeScope();

        builder.RegisterType<AdminAuthManager>().As<IAdminAuthService>().InstancePerLifetimeScope();
        builder.RegisterType<CategoryManager>().As<ICategoryService>().InstancePerLifetimeScope();
        builder.RegisterType<ProductManager>().As<IProductService>().InstancePerLifetimeScope();
    }
}
