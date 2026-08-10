namespace Kotlet.Application.Pantry;

public sealed class PantryConcurrencyException(Exception innerException)
    : Exception("The pantry changed while the operation was being applied.", innerException);
