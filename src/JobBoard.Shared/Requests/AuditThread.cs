using JobBoard.Contracts;

namespace JobBoard.Shared.Requests;

/// <summary>
/// The audit thread stamped onto an integration event at its publish site (ADR-0013, SCRUB A3): the
/// <see cref="IIntegrationEvent.CorrelationId"/>, <see cref="IIntegrationEvent.CausationId"/>, and
/// <see cref="IIntegrationEvent.ActorId"/> that let the support trail reconstruct and attribute a request.
/// Derived once per publish — from the ambient request context on a request-initiated event, or from the
/// consumed event on a follow-on — via <see cref="AuditThreadExtensions"/>, so the root-vs-follow-on rule
/// lives in one place. Pure cross-cutting mechanism; carries no domain.
/// </summary>
/// <param name="CorrelationId">The originating request's thread, constant across its whole fan-out.</param>
/// <param name="CausationId">The <see cref="IIntegrationEvent.Id"/> of the direct cause — the request's
/// own id for a root event, the consumed event's id for a follow-on.</param>
/// <param name="ActorId">The acting identity, or <c>null</c> for an anonymous request.</param>
public readonly record struct AuditThread(Guid CorrelationId, Guid CausationId, Guid? ActorId);
