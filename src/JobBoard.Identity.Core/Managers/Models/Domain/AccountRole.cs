namespace JobBoard.Identity.Core.Managers.Models.Domain;

/// <summary>
/// What an <see cref="Account"/> is allowed to be in the job board. Persisted as its string name (see
/// <c>AccountConfiguration</c>), so the names are the contract — add roles, don't rename existing ones.
/// </summary>
public enum AccountRole
{
    Employer,
    Candidate,
}
