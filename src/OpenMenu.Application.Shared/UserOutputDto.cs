namespace OpenMenu.Application.Shared;

using OpenMenu.Domain;

/// <summary>User data safe to return from APIs — never includes the password hash.</summary>
public record UserOutputDto(int Id, string Username, UserRole Role, bool IsActive);
