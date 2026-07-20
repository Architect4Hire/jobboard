using JobBoard.Audit.Core.Facade;
using JobBoard.Audit.Core.Managers.Models.ServiceModels;
using JobBoard.Audit.Core.Managers.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace JobBoard.Audit.Controllers;

/// <summary>
/// The read-only support-query surface for the audit trail (SCRUB A6). Thin by design: bind the query
/// filter, call the facade, return service models. No mutation surface — the trail is append-only and
/// written solely by the consumers (ADR-0014). Reached only through the gateway's auth-protected
/// <c>/audit</c> route; <c>auditdb</c> is never exposed directly.
/// </summary>
[ApiController]
[Route("audit")]
public sealed class AuditController : ControllerBase
{
    private readonly IAuditFacade _facade;

    public AuditController(IAuditFacade facade) => _facade = facade;

    /// <summary>
    /// Query the trail by any combination of correlation id, entity (subject) id, actor, and time window —
    /// the four <c>trace-a-request</c> axes. Rows come back oldest-first so the caller reads a timeline and
    /// can reconstruct the causal tree from each row's causation id.
    /// </summary>
    [HttpGet("entries")]
    public async Task<ActionResult<IReadOnlyList<AuditEntryServiceModel>>> Query(
        [FromQuery] AuditQueryViewModel query,
        CancellationToken cancellationToken)
    {
        var entries = await _facade.QueryAsync(query, cancellationToken);
        return Ok(entries);
    }
}
