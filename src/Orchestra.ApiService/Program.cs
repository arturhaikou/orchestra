using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Text.Json.Serialization;
using Orchestra.Infrastructure.Hubs;
using Orchestra.Infrastructure.Agents;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Orchestra.Application.Common;
using Microsoft.Agents.AI.Hosting.AGUI.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire components.
builder.AddServiceDefaults();

//builder.Services.AddRateLimiter(options =>
//{
//    options.AddFixedWindowLimiter("auth", opt =>
//    {
//        opt.PermitLimit = 5;
//        opt.Window = TimeSpan.FromMinutes(1);
//        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
//        opt.QueueLimit = 0; // No queuing - immediate rejection
//    });

//    options.OnRejected = async (context, cancellationToken) =>
//    {
//        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
//        {
//            context.HttpContext.Response.Headers.RetryAfter = retryAfter.TotalSeconds.ToString();
//        }

//        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
//        await context.HttpContext.Response.WriteAsJsonAsync(
//            new { error = "Too many requests. Please try again later." },
//            cancellationToken: cancellationToken);
//    };
//});

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.Converters.Add(new OptionalJsonConverterFactory());
    });
builder.Services.AddExceptionHandler<Orchestra.ApiService.Middleware.NotImplementedExceptionHandler>();
builder.Services.AddProblemDetails();
builder.AddInfrastructureServices();

// Configure JWT Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwtSettings = builder.Configuration.GetSection("Jwt");

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidAudience = jwtSettings["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]!))
        };

        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                if (context.Exception.GetType() == typeof(SecurityTokenExpiredException))
                {
                    context.Response.Headers.Append("Token-Expired", "true");
                }
                return Task.CompletedTask;
            },
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"].ToString();
                var path = context.HttpContext.Request.Path;

                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddAGUI();

builder.Services.AddSignalR();

// Configure CORS for Aspire UI integration
builder.Services.AddCors(options =>
{
    var allowedOrigins = builder.Configuration
        .GetSection("Cors:AllowedOrigins")
        .Get<string[]>() ?? [];

    options.AddPolicy("AllowAspireUI", policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            // Use specific origins when configured (Aspire dynamic ports, or Development fallback)
            policy.WithOrigins(allowedOrigins)
                  .AllowCredentials()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        }
        else
        {
            // Fallback for non-Aspire environments: allow any origin without credentials
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        }
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
app.MapDefaultEndpoints();

app.UseExceptionHandler();

app.UseSwagger();
app.UseSwaggerUI();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// CORS must be before authentication/authorization
app.UseCors("AllowAspireUI");

// Rate limiting MUST come before authentication/authorization
//app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

// AG-UI streaming endpoint: /workspaces/{workspaceId}/agents/{agentId}
// Dynamically resolves the agent per request using the workspace AI provider and tools.
var dynamicAgent = app.Services.GetRequiredService<DynamicWorkspaceAgent>();
app.MapAGUI("/workspaces/{workspaceId}/agents/{agentId}", dynamicAgent)
   .RequireAuthorization();

app.MapHub<NotificationHub>("/hubs/notifications");

app.MapControllers();

app.Run();
