namespace JobBoard.Profiles.Core.Managers.Models.ServiceModels;

/// <summary>
/// The employer profile shape returned by <c>GET</c>/<c>PUT /profiles/employers/{employerId}</c>.
/// Maps one-to-one from a loaded <see cref="Domain.EmployerProfile"/>; the entity never leaves the service.
/// </summary>
public sealed record EmployerProfileServiceModel(
    Guid EmployerId,
    string CompanyName,
    string? Website,
    string Description,
    DateTime UpdatedOnUtc);
