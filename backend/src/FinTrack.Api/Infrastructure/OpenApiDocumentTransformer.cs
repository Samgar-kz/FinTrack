using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace FinTrack.Api.Infrastructure;

/// <summary>Adds API metadata and the JWT bearer security scheme to the generated OpenAPI document.</summary>
internal sealed class OpenApiDocumentTransformer : IOpenApiDocumentTransformer
{
    private const string SchemeName = "Bearer";

    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Info = new OpenApiInfo
        {
            Title = "FinTrack API",
            Version = "v1",
            Description = "Personal finance management API: accounts, transactions, transfers, budgets and reports.",
        };

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[SchemeName] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Access token returned by /api/auth/login.",
        };

        document.Security =
        [
            new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(SchemeName, document)] = [],
            },
        ];

        return Task.CompletedTask;
    }
}
