using HerYerde.Core.DataAccess.EntityFramework;
using HerYerde.DataAccess.Abstract;
using HerYerde.DataAccess.Concrete.EntityFramework.Contexts;
using HerYerde.Entities.Concrete;

namespace HerYerde.DataAccess.Concrete.EntityFramework;

public class EfOrderItemDal : EfEntityRepositoryBase<OrderItem, HerYerdeContext>, IOrderItemDal
{
    public EfOrderItemDal(HerYerdeContext context) : base(context)
    {
    }
}
