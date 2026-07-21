using Kotlet.Domain.Images;

namespace Kotlet.Domain.PreparedMeals;

public sealed class PreparedMealImage
{
    public Guid Id { get; set; }
    public Guid PreparedMealId { get; set; }
    public PreparedMeal PreparedMeal { get; set; } = null!;
    public StoredImage Image { get; set; } = new() { FileName = string.Empty, ContentType = string.Empty, Content = [] };
    public int SortOrder { get; set; }
}
