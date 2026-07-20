using JobBoard.Identity.Core.Managers.Models.Domain;

namespace JobBoard.Identity.Core.Security;

/// <summary>
/// Issues the signed JWT an authenticated account presents to the gateway. A seam so the business layer
/// decides <i>when</i> to issue a token while the signing mechanics live in one place (and can be faked
/// in tests).
/// </summary>
public interface ITokenIssuer
{
    IssuedToken Issue(Account account);
}
