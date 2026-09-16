using HerYerde.Core.DataAccess.EntityFramework;
using HerYerde.DataAccess.Abstract;
using HerYerde.DataAccess.Concrete.EntityFramework.Contexts;
using HerYerde.Entities.Concrete;

namespace HerYerde.DataAccess.Concrete.EntityFramework;

public class EfCategoryAttributeTemplateDal : EfEntityRepositoryBase<CategoryAttributeTemplate, HerYerdeContext>, ICategoryAttributeTemplateDal
{
    public EfCategoryAttributeTemplateDal(HerYerdeContext context) : base(context)
    {
    }
}
