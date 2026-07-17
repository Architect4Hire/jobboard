namespace JobBoard.Jobs.Core.Managers.Models.Domain;

/// <summary>
/// The shape shared by a <see cref="Job"/>'s classifications — <see cref="Category"/> and <see cref="Tag"/>.
/// Both are app-keyed rows identified for reuse by their URL-safe <see cref="Slug"/>, which lets the
/// repository reconcile posted classifications against existing rows with one generic routine.
/// </summary>
public interface IClassification
{
    Guid Id { get; set; }

    string Name { get; set; }

    string Slug { get; set; }
}
