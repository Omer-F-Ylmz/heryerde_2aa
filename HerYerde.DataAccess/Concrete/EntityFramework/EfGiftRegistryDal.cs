using HerYerde.Core.DataAccess.EntityFramework;
using HerYerde.DataAccess.Abstract;
using HerYerde.DataAccess.Concrete.EntityFramework.Contexts;
using HerYerde.Entities.Concrete;

namespace HerYerde.DataAccess.Concrete.EntityFramework;

public class EfGiftRegistryDal : EfEntityRepositoryBase<GiftRegistry, HerYerdeContext>, IGiftRegistryDal
{
    public EfGiftRegistryDal(HerYerdeContext context) : base(context)
    {
    }
}
