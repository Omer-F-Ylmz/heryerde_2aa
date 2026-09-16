using HerYerde.Core.DataAccess.EntityFramework;
using HerYerde.DataAccess.Abstract;
using HerYerde.DataAccess.Concrete.EntityFramework.Contexts;
using HerYerde.Entities.Concrete;

namespace HerYerde.DataAccess.Concrete.EntityFramework;

public class EfProductVideoDal : EfEntityRepositoryBase<ProductVideo, HerYerdeContext>, IProductVideoDal
{
    public EfProductVideoDal(HerYerdeContext context) : base(context)
    {
    }
}
