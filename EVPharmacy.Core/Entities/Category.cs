
namespace EVPharmacy.Core.Entities;

public class Category : LookupItem
{
    public const int MaxDescriptionLength = 200;

    public string? Description { get; set; }
}
