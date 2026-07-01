using Kotlet.Application.Houses;

namespace Kotlet.Api.Houses;

public sealed record TokenResponse(string AccessToken, DateTime AccessTokenExpiresAtUtc);
public sealed record HouseWithTokenResponse(HouseSummaryResponse House, TokenResponse? Token);
