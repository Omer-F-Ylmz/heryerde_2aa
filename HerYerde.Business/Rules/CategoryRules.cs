using HerYerde.Entities.Concrete;

namespace HerYerde.Business.Rules;

public static class CategoryRules
{
    /// <summary>Kategori ağacı iki seviye: yalnız kök kategori (parent_id boş) üst olabilir.</summary>
    public static bool CanBeParent(Category candidate) => candidate.ParentId is null;
}
