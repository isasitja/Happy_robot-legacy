
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using TMS_API.Configuration;
using TMS_API.Diagnostics;
using TMS_API.Security;
using TMS_API.Services;

namespace TMS_API
{
    public class Program
    {
        public static void Main(string[] args)
        { 

            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            builder.Services.AddControllers();
            // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
            builder.Services.AddOpenApi();

            // Behind a TLS-terminating reverse proxy (e.g. Cloud Run): honor X-Forwarded-* so the
            // app sees the original scheme (https) and client IP.
            builder.Services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                options.KnownNetworks.Clear();
                options.KnownProxies.Clear();
            });

            // API credentials + JWT settings. Username/password are exchanged at /api/auth/token
            // for a bearer token that authorizes the rest of the endpoints.
            builder.Services.AddOptions<ApiAuthOptions>()
                .Bind(builder.Configuration.GetSection(ApiAuthOptions.SectionName))
                .Validate(o => !string.IsNullOrWhiteSpace(o.Username), "ApiAuth:Username is required.")
                .Validate(o => !string.IsNullOrWhiteSpace(o.Password), "ApiAuth:Password is required.")
                .Validate(o => !string.IsNullOrWhiteSpace(o.JwtSigningKey), "ApiAuth:JwtSigningKey is required.")
                .Validate(o => Encoding.UTF8.GetByteCount(o.JwtSigningKey) >= 32, "ApiAuth:JwtSigningKey must be at least 32 bytes for HMAC-SHA256.")
                .ValidateOnStart();

            builder.Services.AddSingleton<ITokenService, TokenService>();

            var authOptions = builder.Configuration.GetSection(ApiAuthOptions.SectionName).Get<ApiAuthOptions>() ?? new ApiAuthOptions();

            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = authOptions.JwtIssuer,
                        ValidateAudience = true,
                        ValidAudience = authOptions.JwtAudience,
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(authOptions.JwtSigningKey ?? string.Empty)),
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.FromMinutes(1)
                    };
                });

            // Require an authenticated user for every endpoint by default ([AllowAnonymous] opts out).
            builder.Services.AddAuthorization(options =>
            {
                options.FallbackPolicy = new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .Build();
            });

            // Add a Bearer (JWT) scheme to the generated OpenAPI document so Swagger UI can authenticate.
            builder.Services.AddOpenApi(options =>
            {
                options.AddDocumentTransformer((document, context, cancellationToken) =>
                {
                    document.Components ??= new OpenApiComponents();
                    document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                    document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
                    {
                        Type = SecuritySchemeType.Http,
                        Scheme = "bearer",
                        BearerFormat = "JWT",
                        Description = "Pega aquí el token obtenido en /api/auth/token (sin el prefijo 'Bearer')."
                    };

                    // Require the bearer token on every operation.
                    foreach (var operation in document.Paths.Values.SelectMany(path => path.Operations))
                    {
                        operation.Value.Security ??= [];
                        operation.Value.Security.Add(new OpenApiSecurityRequirement
                        {
                            [new OpenApiSecuritySchemeReference("Bearer", document)] = []
                        });
                    }

                    return Task.CompletedTask;
                });
            });

            // Legacy TMS (LTMS) line-oriented TCP adapter.
            builder.Services.AddOptions<LegacyTmsOptions>()
                .Bind(builder.Configuration.GetSection(LegacyTmsOptions.SectionName))
                .Validate(o => !string.IsNullOrWhiteSpace(o.Host), "LegacyTms:Host is required.")
                .Validate(o => o.Port is > 0 and <= 65535, "LegacyTms:Port must be between 1 and 65535.")
                .Validate(o => !string.IsNullOrWhiteSpace(o.Token), "LegacyTms:Token is required.")
                .ValidateOnStart();
            builder.Services.AddScoped<ILegacyTmsClient, LegacyTmsClient>();

            // Health checks. The "ready" tag gates the readiness probe that pings the Legacy TMS.
            builder.Services.AddHealthChecks()
                .AddCheck<LegacyTmsHealthCheck>("legacy_tms", tags: ["ready"]);

            var app = builder.Build();

            // Must run early so downstream middleware sees the forwarded scheme/IP.
            app.UseForwardedHeaders();

            // Configure the HTTP request pipeline.
            // Expose the OpenAPI document and Swagger UI in all environments (including Cloud Run).
            // Allow anonymous access to the OpenAPI document so Swagger UI can load it
            // without a token (the global FallbackPolicy would otherwise return 401).
            app.MapOpenApi().AllowAnonymous();
            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/openapi/v1.json", "TMS_API v1");
            });

            if (app.Environment.IsDevelopment())
            {
                // HTTPS redirection only makes sense locally. Behind a TLS-terminating proxy
                // (Cloud Run) it would cause a redirect loop, so it's disabled in other environments.
                app.UseHttpsRedirection();
            }

            app.UseAuthentication();
            app.UseAuthorization();


            app.MapControllers();

            // Liveness: process is up. Does not touch the Legacy TMS.
            app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
            {
                Predicate = _ => false,
                ResponseWriter = HealthCheckResponseWriter.WriteAsync
            }).AllowAnonymous();

            // Readiness: verifies connectivity/auth against the Legacy TMS via DEBUG_ECHO.
            app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
            {
                Predicate = check => check.Tags.Contains("ready"),
                ResponseWriter = HealthCheckResponseWriter.WriteAsync
            }).AllowAnonymous();

            app.Run();
        }
    }
}
