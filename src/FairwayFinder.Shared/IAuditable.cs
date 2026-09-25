namespace FairwayFinder.Shared;

/// <summary>
/// Marks an entity whose creation and modification instants are stamped automatically on save by
/// <c>AuditStampInterceptor</c>. Both values are UTC <see cref="DateTime"/>s backed by
/// <c>timestamp with time zone</c> columns.
/// </summary>
/// <remarks>
/// <c>CreatedBy</c> / <c>UpdatedBy</c> are deliberately not part of this contract. Admin actions
/// must stamp the acting admin rather than the record's owner, and the interceptor has no way to
/// know who that is — services set those two by hand.
/// </remarks>
public interface IAuditable
{
    DateTime CreatedOn { get; set; }
    DateTime UpdatedOn { get; set; }
}
