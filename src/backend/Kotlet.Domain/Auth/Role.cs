namespace Kotlet.Domain.Auth;

public static class RoleNames
{
    public const string User = "User";
    public const string Admin = "Admin";
}

public static class RoleIds
{
    public static readonly Guid User = Guid.Parse("01000000-0000-0000-0000-000000000001");
    public static readonly Guid Admin = Guid.Parse("01000000-0000-0000-0000-000000000002");
}

public sealed class Role
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public ICollection<User> Users { get; set; } = [];
}
