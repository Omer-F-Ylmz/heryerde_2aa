using HerYerde.Core.DataAccess.EntityFramework;
using HerYerde.DataAccess.Abstract;
using HerYerde.DataAccess.Concrete.EntityFramework.Contexts;
using HerYerde.Entities.Concrete;

namespace HerYerde.DataAccess.Concrete.EntityFramework;

public class EfProductDal : EfEntityRepositoryBase<Product, HerYerdeContext>, IProductDal
{
    public EfProductDal(HerYerdeContext context) : base(context)
    {
    }
}
