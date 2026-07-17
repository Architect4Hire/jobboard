using JobBoard.Identity.Core.Managers.Models.Domain;

namespace JobBoard.Identity.Core.Managers.Models.ServiceModels;

/// <summary>
/// The account shape returned by <c>POST /identity/register</c>. Maps from a persisted
/// <see cref="Account"/>; the password hash never leaves the service.
/// </summary>
public sealed record AccountServiceModel(Guid Id, string Email, AccountRole Role);
