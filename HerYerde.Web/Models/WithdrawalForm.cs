using System.ComponentModel.DataAnnotations;

namespace HerYerde.Web.Models;

/// <summary>/yasal/cayma-formu: sitede doldurulup PDF olarak indirilen cayma formu. Kaydedilmez; alanlar yalnız PDF'e yazılır.</summary>
public sealed class WithdrawalFormModel
{
    [StringLength(120)]
    public string? FullName { get; set; }

    [StringLength(300)]
    public string? Address { get; set; }

    [StringLength(30)]
    public string? OrderNo { get; set; }

    [StringLength(40)]
    public string? OrderDate { get; set; }

    [StringLength(500)]
    public string? Items { get; set; }

    [StringLength(40)]
    public string? Amount { get; set; }
}
