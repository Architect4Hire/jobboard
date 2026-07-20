namespace JobBoard.Profiles.Core.Managers.Models.Domain;

/// <summary>
/// When a candidate is open to starting a new role. A small bounded set, so it's a domain enum rather than
/// free text; persisted <b>by name</b> (see the EF configuration) so the stored value survives reordering.
/// </summary>
public enum CandidateAvailability
{
    /// <summary>Available to start immediately.</summary>
    Immediate = 0,

    /// <summary>Available within about two weeks (e.g. a short notice period).</summary>
    WithinTwoWeeks = 1,

    /// <summary>Available within about a month.</summary>
    WithinAMonth = 2,

    /// <summary>Employed and open to conversations, but not actively looking.</summary>
    NotLooking = 3,
}
