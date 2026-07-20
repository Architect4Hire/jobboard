namespace JobBoard.Jobs.Core.Managers.Models.ServiceModels;

/// <summary>Outbound advertised pay range on a job the API returns.</summary>
public sealed record SalaryBandServiceModel(decimal Min, decimal Max, string Currency);
