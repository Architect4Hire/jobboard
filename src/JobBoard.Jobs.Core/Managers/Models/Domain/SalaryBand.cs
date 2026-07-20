namespace JobBoard.Jobs.Core.Managers.Models.Domain;

/// <summary>
/// The advertised pay range for a <see cref="Job"/>. An EF owned type — it has no identity of its own
/// and lives inline on the Job row (columns <c>Salary_Min</c>, <c>Salary_Max</c>, <c>Salary_Currency</c>),
/// not in a table of its own.
/// </summary>
public class SalaryBand
{
    public decimal Min { get; set; }

    public decimal Max { get; set; }

    /// <summary>ISO 4217 currency code (e.g. <c>USD</c>).</summary>
    public string Currency { get; set; } = default!;
}
