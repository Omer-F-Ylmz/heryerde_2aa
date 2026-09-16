using HerYerde.Core.Entities;

namespace HerYerde.Entities.Concrete;

/// <summary>Üyenin adres defteri satırı; varsayılan olan ödeme formunu doldurur (üye başına en çok bir tane).</summary>
public class CustomerAddress : IEntity
{
    public int Id { get; set; }
    public int CustomerId { get; set; }

    /// <summary>"Ev", "İş" gibi kısa ad.</summary>
    public string Title { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public DateTime CreatedAt { get; set; }
}
