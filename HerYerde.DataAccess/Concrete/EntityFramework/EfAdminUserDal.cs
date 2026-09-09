using HerYerde.Core.DataAccess.EntityFramework;
using HerYerde.DataAccess.Abstract;
using HerYerde.DataAccess.Concrete.EntityFramework.Contexts;
using HerYerde.Entities.Concrete;

namespace HerYerde.DataAccess.Concrete.EntityFramework;

public class EfAdminUserDal : EfEntityRepositoryBase<AdminUser, HerYerdeContext>, IAdminUserDal
{
    public EfAdminUserDal(HerYerdeContext context) : base(context)
    {
    }
}
