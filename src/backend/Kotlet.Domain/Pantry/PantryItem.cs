using Kotlet.Domain.Common;
using Kotlet.Domain.Houses;
using Kotlet.Domain.Ingredients;

namespace Kotlet.Domain.Pantry;

public sealed class PantryItem
{
    public Guid Id { get; set; }
    public Guid HouseId { get; set; }
    public Guid IngredientId { get; set; }
    public Quantity Quantity { get; set; }
    public DateOnly? ExpirationDate { get; set; }
    public House House { get; set; } = null!;
    public Ingredient Ingredient { get; set; } = null!;
}
