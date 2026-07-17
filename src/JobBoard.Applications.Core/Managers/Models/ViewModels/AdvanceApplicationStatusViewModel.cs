using JobBoard.Applications.Core.Managers.Models.Domain;

namespace JobBoard.Applications.Core.Managers.Models.ViewModels;

/// <summary>
/// Inbound request to advance an application to a new status — the only shape the advance controller
/// binds. The business layer decides whether the requested transition is legal from the application's
/// current status; the validator only checks that <see cref="TargetStatus"/> is a defined enum value.
/// </summary>
public sealed record AdvanceApplicationStatusViewModel
{
    public ApplicationStatus TargetStatus { get; init; }
}
