using HerYerde.Core.DataAccess.EntityFramework;
using HerYerde.DataAccess.Abstract;
using HerYerde.DataAccess.Concrete.EntityFramework.Contexts;
using HerYerde.Entities.Concrete;

namespace HerYerde.DataAccess.Concrete.EntityFramework;

public class EfProductAttributeDal : EfEntityRepositoryBase<ProductAttribute, HerYerdeContext>, IProductAttributeDal
{
    public EfProductAttributeDal(HerYerdeContext context) : base(context)
    {
    }
}
