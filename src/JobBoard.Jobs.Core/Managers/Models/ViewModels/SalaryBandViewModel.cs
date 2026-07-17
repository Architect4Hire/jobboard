namespace JobBoard.Jobs.Core.Managers.Models.ViewModels;

/// <summary>Inbound advertised pay range for a posted job. Validated by the view model's validator.</summary>
public sealed record SalaryBandViewModel
{
    public decimal Min { get; init; }

    public decimal Max { get; init; }

    /// <summary>ISO 4217 currency code (e.g. <c>USD</c>).</summary>
    public string Currency { get; init; } = default!;
}
