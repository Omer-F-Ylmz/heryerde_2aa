using HerYerde.Entities.Concrete;

namespace HerYerde.Business.Rules;

public static class CategoryRules
{
    /// <summary>Kategori ağacı iki seviye: yalnız kök kategori (parent_id boş) üst olabilir.</summary>
    public static bool CanBeParent(Category candidate) => candidate.ParentId is null;

    /// <summary>Kök kategoriler (Giyim, Ev) alanın kendisidir: slug'ı sabit, silinemez.</summary>
    public static bool IsRoot(Category category) => category.ParentId is null;
}
