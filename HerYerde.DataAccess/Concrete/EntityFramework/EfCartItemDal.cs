using HerYerde.Core.DataAccess.EntityFramework;
using HerYerde.DataAccess.Abstract;
using HerYerde.DataAccess.Concrete.EntityFramework.Contexts;
using HerYerde.Entities.Concrete;

namespace HerYerde.DataAccess.Concrete.EntityFramework;

public class EfCartItemDal : EfEntityRepositoryBase<CartItem, HerYerdeContext>, ICartItemDal
{
    public EfCartItemDal(HerYerdeContext context) : base(context)
    {
    }
}
