using Dhathuru.Domain.Configuration;
using Dhathuru.Domain.Security;
using Dhathuru.Identity.Email;
using Dhathuru.Identity.Options;
using Dhathuru.Identity.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Dhathuru.Identity;

public static class IdentityModule
{
    public static IServiceCollection AddIdentityModule(this IServiceCollection services, IConfiguration configuration)
    {
        // Resolved lazily so the host can start (and answer /health) before a password is configured.
        services.AddDbContext<AccountsDbContext>(o => Configure(o, configuration.GetDatabaseOptions().ToConnectionString()));

        services.AddOptions<AuthOptions>().Bind(configuration.GetSection(AuthOptions.SectionName));
        services.TryAddSingleton(TimeProvider.System);
        services.AddMemoryCache();
        services.AddHttpContextAccessor();
        services.AddFluentValidatorsFrom();

        services.AddIdentityCore<AppUser>()
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<AccountsDbContext>();
        services.AddOptions<IdentityOptions>().Configure<IOptions<AuthOptions>>((identity, auth) =>
        {
            // Password rules live in PasswordPolicy (length based); Identity only enforces a floor.
            identity.Password.RequireDigit = false;
            identity.Password.RequireLowercase = false;
            identity.Password.RequireUppercase = false;
            identity.Password.RequireNonAlphanumeric = false;
            identity.Password.RequiredLength = auth.Value.PasswordMinLength;
            identity.User.RequireUniqueEmail = true;
            identity.Lockout.AllowedForNewUsers = true;
            identity.Lockout.MaxFailedAccessAttempts = auth.Value.LockoutMaxFailedAttempts;
            identity.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(auth.Value.LockoutMinutes);
        });

        services.AddSingleton(sp => new SigningKeyProvider(
            sp.GetRequiredService<IOptions<AuthOptions>>().Value,
            sp.GetRequiredService<IHostEnvironment>().IsDevelopment()));
        services.AddSingleton<AccessTokenIssuer>();
        services.AddSingleton<Dhathuru.Domain.Seams.IModuleMigrator, AccountsMigrator>();
        services.AddScoped<AccountService>();
        services.AddScoped<AccountEmails>();
        services.AddScoped<ICurrentUser, CurrentUser>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<SigningKeyProvider, IOptions<AuthOptions>>((jwt, keys, auth) =>
            {
                jwt.MapInboundClaims = false;
                jwt.RequireHttpsMetadata = false; // no metadata endpoint: the key is configured
                jwt.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = auth.Value.Issuer,
                    ValidAudience = auth.Value.Audience,
                    IssuerSigningKey = keys.Key,
                    ValidAlgorithms = [SecurityAlgorithms.EcdsaSha256],
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    RequireExpirationTime = true,
                    RequireSignedTokens = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = "sub",
                    RoleClaimType = "role",
                };
                jwt.Events = new JwtBearerEvents
                {
                    // A revoked or expired session must stop working immediately, not when the access token runs out.
                    OnTokenValidated = async context =>
                    {
                        var user = context.Principal?.FindFirst("sub")?.Value;
                        var session = context.Principal?.FindFirst(AccessTokenIssuer.SessionClaim)?.Value;
                        var accounts = context.HttpContext.RequestServices.GetRequiredService<AccountService>();
                        if (user is null || session is null
                            || !await accounts.IsSessionActiveAsync(user, session, context.HttpContext.RequestAborted))
                        {
                            context.Fail("session_inactive");
                        }
                    },
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(Policies.Authenticated, p => p.RequireAuthenticatedUser())
            .AddPolicy(Policies.VerifiedEmail, p => p.RequireAuthenticatedUser().RequireClaim(AccessTokenIssuer.EmailVerifiedClaim, "true"))
            .AddPolicy(Policies.Operator, p => p.RequireRole(Roles.Operator, Roles.Admin))
            .AddPolicy(Policies.Admin, p => p.RequireRole(Roles.Admin));
        return services;
    }

    internal static void Configure(DbContextOptionsBuilder options, string connectionString) =>
        options.UseNpgsql(connectionString, npgsql =>
            npgsql.MigrationsHistoryTable(AccountsDbContext.MigrationsTable, AccountsDbContext.Schema));

    private static IServiceCollection AddFluentValidatorsFrom(this IServiceCollection services)
    {
        FluentValidation.AssemblyScanner.FindValidatorsInAssembly(typeof(IdentityModule).Assembly, includeInternalTypes: true)
            .ForEach(result => services.AddScoped(result.InterfaceType, result.ValidatorType));
        return services;
    }
}

/// <summary>Lets <c>dotnet ef migrations add --project src/Dhathuru.Identity</c> work without the host.</summary>
public sealed class AccountsDesignTimeFactory : IDesignTimeDbContextFactory<AccountsDbContext>
{
    public AccountsDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();
        var builder = new DbContextOptionsBuilder<AccountsDbContext>();
        IdentityModule.Configure(builder, configuration.GetDatabaseOptions().ToConnectionString());
        return new AccountsDbContext(builder.Options);
    }
}
