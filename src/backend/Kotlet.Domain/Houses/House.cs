using Kotlet.Domain.Auth;
using Kotlet.Domain.Pantry;

namespace Kotlet.Domain.Houses;

public sealed class House
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public ICollection<User> Users { get; set; } = [];
    public ICollection<PantryItem> PantryItems { get; set; } = [];
}

public static class DefaultHouse
{
    public static readonly Guid Id = Guid.Parse("8a8c2f75-5998-45e8-8888-1d03d5b45275");
    public const string Name = "Default house";
}
