using HerYerde.Core.DataAccess.EntityFramework;
using HerYerde.DataAccess.Abstract;
using HerYerde.DataAccess.Concrete.EntityFramework.Contexts;
using HerYerde.Entities.Concrete;

namespace HerYerde.DataAccess.Concrete.EntityFramework;

public class EfKvkkRequestDal : EfEntityRepositoryBase<KvkkRequest, HerYerdeContext>, IKvkkRequestDal
{
    public EfKvkkRequestDal(HerYerdeContext context) : base(context)
    {
    }
}

public class EfOrderNoteDal : EfEntityRepositoryBase<OrderNote, HerYerdeContext>, IOrderNoteDal
{
    public EfOrderNoteDal(HerYerdeContext context) : base(context)
    {
    }
}
