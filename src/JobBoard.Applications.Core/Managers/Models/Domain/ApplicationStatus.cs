namespace JobBoard.Applications.Core.Managers.Models.Domain;

/// <summary>
/// Lifecycle of an <see cref="Application"/>. Persisted as its underlying <c>int</c> (the EF default), so
/// the numeric values are the contract — append new states, never renumber existing ones. The three
/// <i>active</i> states (<see cref="Submitted"/>, <see cref="Reviewed"/>, <see cref="Offered"/>) can still
/// transition; <see cref="Rejected"/> and <see cref="Withdrawn"/> are terminal. A candidate withdraws;
/// an employer/advance flow — or a job closing — rejects.
/// </summary>
public enum ApplicationStatus
{
    Submitted = 0,
    Reviewed = 1,
    Offered = 2,
    Rejected = 3,
    Withdrawn = 4,
}
