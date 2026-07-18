using FluentValidation;
using JobBoard.Identity.Core;
using JobBoard.Identity.Core.Business;
using JobBoard.Identity.Core.Data;
using JobBoard.Identity.Core.Facade;
using JobBoard.Identity.Core.Security;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers the Identity.Core stack (facade → business → data → repository), the password/token seams,
/// the <see cref="JwtOptions"/> binding, and the validators from this assembly. The host's composition
/// root calls this alongside the shared exception handler; no per-layer wiring lives in the host.
/// Takes <see cref="IConfiguration"/> so the JWT settings are bound here, in the one place that owns
/// registration.
/// </summary>
public static class IdentityCoreServiceCollectionExtensions
{
    public static IServiceCollection AddIdentityCore(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<IAccountDataLayer, AccountDataLayer>();
        services.AddScoped<IAccountBusiness, AccountBusiness>();
        services.AddScoped<IAccountFacade, AccountFacade>();

        // Credential + token mechanics (stateless — singletons).
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<ITokenIssuer, JwtTokenIssuer>();

        // Bind + fail fast at boot if the JWT settings are missing, rather than surfacing a null-key
        // crash on the first login. Mirrors the gateway's startup guards so both ends fail the same way.
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.SigningKey), "Jwt:SigningKey is not configured.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.Issuer), "Jwt:Issuer is not configured.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.Audience), "Jwt:Audience is not configured.")
            .ValidateOnStart();

        // Validators (RegisterAccountViewModelValidator, LoginViewModelValidator) — from this assembly.
        services.AddValidatorsFromAssemblyContaining<IdentityCoreMarker>();

        return services;
    }
}
