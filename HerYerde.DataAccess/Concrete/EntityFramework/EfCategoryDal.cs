using HerYerde.Core.DataAccess.EntityFramework;
using HerYerde.DataAccess.Abstract;
using HerYerde.DataAccess.Concrete.EntityFramework.Contexts;
using HerYerde.Entities.Concrete;

namespace HerYerde.DataAccess.Concrete.EntityFramework;

public class EfCategoryDal : EfEntityRepositoryBase<Category, HerYerdeContext>, ICategoryDal
{
    public EfCategoryDal(HerYerdeContext context) : base(context)
    {
    }
}
