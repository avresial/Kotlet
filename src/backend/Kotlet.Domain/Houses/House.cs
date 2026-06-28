using Kotlet.Domain.Pantry;
using Kotlet.Domain.Shopping;

namespace Kotlet.Domain.Houses;

public sealed class House
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public ICollection<HouseMembership> Memberships { get; set; } = [];
    public ICollection<HouseInvitation> Invitations { get; set; } = [];
    public ICollection<PantryItem> PantryItems { get; set; } = [];
    public ICollection<ShoppingListItem> ShoppingListItems { get; set; } = [];
}

public static class DefaultHouse
{
    public static readonly Guid Id = Guid.Parse("8a8c2f75-5998-45e8-8888-1d03d5b45275");
    public const string Name = "Default house";
}
