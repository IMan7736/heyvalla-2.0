namespace Dhathuru.Identity.Services;

public sealed record RegisterRequest(string? Email, string? Password, string? DisplayName, string? Locale);

public sealed record LoginRequest(string? Email, string? Password);

public sealed record RefreshRequest(string? RefreshToken);

public sealed record TokenRequest(string? Token);

public sealed record EmailRequest(string? Email);

public sealed record ResetPasswordRequest(string? Token, string? NewPassword);

public sealed record ChangePasswordRequest(string? CurrentPassword, string? NewPassword);

public sealed record ChangeEmailRequest(string? NewEmail, string? Password);

public sealed record DeleteAccountRequest(string? Password);

public sealed record UpdateMeRequest(string? DisplayName, string? Locale);

public sealed record UpdatePreferencesRequest(string? Language, string? RiskTolerance, List<string>? PreferredModes);

public sealed record TravellerInput(string? FullName, string? PassengerType, DateOnly? DateOfBirth, string? Phone, string? DocumentType);

public sealed record TokenPair(string AccessToken, string RefreshToken, string TokenType, int ExpiresIn);

public sealed record UserDto(string Id, string Email, bool EmailVerified, string? DisplayName, string Locale, DateTimeOffset CreatedAt);

public sealed record PreferencesDto(string Language, string RiskTolerance, IReadOnlyList<string> PreferredModes);

public sealed record TravellerDto(string Id, string FullName, string PassengerType, DateOnly? DateOfBirth, string? Phone, string DocumentType);

public sealed record SessionDto(string Id, DateTimeOffset CreatedAt, DateTimeOffset LastUsedAt, string? UserAgent, string? NetworkHint, bool Current);
