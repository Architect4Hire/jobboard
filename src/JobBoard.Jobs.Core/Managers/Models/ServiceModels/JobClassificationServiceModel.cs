namespace JobBoard.Jobs.Core.Managers.Models.ServiceModels;

/// <summary>Outbound category or tag on a job the API returns — its display name and URL-safe slug.</summary>
public sealed record JobClassificationServiceModel(string Name, string Slug);
