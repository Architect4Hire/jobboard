using FluentValidation;
using JobBoard.Profiles.Core;
using JobBoard.Profiles.Core.Business;
using JobBoard.Profiles.Core.Data;
using JobBoard.Profiles.Core.Facade;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers the Profiles.Core stacks — both aggregates (candidate + employer), each facade → business →
/// data → repository — and the validators from this assembly. The host's composition root calls this
/// alongside the shared exception handler; no per-layer wiring lives in the host.
/// </summary>
public static class ProfilesCoreServiceCollectionExtensions
{
    public static IServiceCollection AddProfilesCore(this IServiceCollection services)
    {
        // Candidate profile stack.
        services.AddScoped<ICandidateProfileRepository, CandidateProfileRepository>();
        services.AddScoped<ICandidateProfileDataLayer, CandidateProfileDataLayer>();
        services.AddScoped<ICandidateProfileBusiness, CandidateProfileBusiness>();
        services.AddScoped<ICandidateProfileFacade, CandidateProfileFacade>();

        // Employer profile stack.
        services.AddScoped<IEmployerProfileRepository, EmployerProfileRepository>();
        services.AddScoped<IEmployerProfileDataLayer, EmployerProfileDataLayer>();
        services.AddScoped<IEmployerProfileBusiness, EmployerProfileBusiness>();
        services.AddScoped<IEmployerProfileFacade, EmployerProfileFacade>();

        // Validators (Upsert{Candidate,Employer}ProfileViewModelValidator) — registered once from this assembly.
        services.AddValidatorsFromAssemblyContaining<ProfilesCoreMarker>();

        return services;
    }
}
