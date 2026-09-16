using HerYerde.Core.DataAccess;
using HerYerde.Entities.Concrete;

namespace HerYerde.DataAccess.Abstract;

public interface ICustomerDal : IEntityRepository<Customer>
{
    /// <summary>E-postası doğrulanan üyeye aynı e-postalı, henüz üyesi olmayan misafir siparişlerini ve çeyiz listelerini bağlar
    /// (tek UPDATE'ler). Bağlanan sipariş sayısını döner.</summary>
    Task<int> LinkGuestRecordsAsync(int customerId, string email, CancellationToken cancellationToken = default);
}
