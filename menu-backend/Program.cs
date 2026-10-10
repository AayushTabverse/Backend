using System.Text;
using menu_backend.Data;
using menu_backend.Helpers;
using menu_backend.Hubs;
using menu_backend.Middleware;
using menu_backend.Services;
using menu_backend.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

// ── Restaurant timezone (for "today", bill numbers, date filters) ──
BusinessClock.Configure(config["App:TimeZone"]);

// ── Database (Azure MySQL) ──
var connectionString = config.GetConnectionString("DefaultConnection")!;
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySQL(connectionString, mysqlOptions =>
    {
        mysqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorNumbersToAdd: null);
    }));

// ── Tenant Provider ──
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantProvider, TenantProvider>();

// ── JWT Authentication ──
var jwtSecret = config["Jwt:Secret"] ?? "YourSuperSecretKeyMustBeAtLeast32CharsLong!!";
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = config["Jwt:Issuer"] ?? "menu-backend",
            ValidAudience = config["Jwt:Audience"] ?? "menu-frontend",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
        };

        // Allow SignalR to receive tokens via query string
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/orders"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// ── Services (DI) ──
builder.Services.AddSingleton<JwtHelper>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IMenuService, MenuService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<ITableService, TableService>();
builder.Services.AddScoped<IPrintService, PrintService>();
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();
builder.Services.AddScoped<ISettingsService, SettingsService>();
builder.Services.AddScoped<IWebsiteService, WebsiteService>();
builder.Services.AddScoped<ISubdomainService, SubdomainService>();
builder.Services.AddSingleton<IBlobStorageService, BlobStorageService>();
builder.Services.AddScoped<IAiContentService, AiContentService>();
builder.Services.AddScoped<IReviewService, ReviewService>();
builder.Services.AddScoped<ISocialMediaService, SocialMediaService>();
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();
builder.Services.AddScoped<IStaffService, StaffService>();
builder.Services.AddScoped<IMenuImportService, MenuImportService>();
builder.Services.AddScoped<IStaffImportService, StaffImportService>();
builder.Services.AddScoped<IInventoryImportService, InventoryImportService>();
builder.Services.AddScoped<ICustomDomainService, CustomDomainService>();
builder.Services.AddScoped<IAnalyticsInsightsService, AnalyticsInsightsService>();
builder.Services.AddSingleton<DnsOverHttps>();
builder.Services.AddSingleton<SiteProbe>();
builder.Services.AddHttpClient();
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient("doh", c => c.Timeout = TimeSpan.FromSeconds(6));
// Website status check: follow redirects but don't hang on slow hosts
builder.Services.AddHttpClient("site-check", c =>
{
    c.Timeout = TimeSpan.FromSeconds(10);
    c.DefaultRequestHeaders.UserAgent.ParseAdd("TabverseSiteCheck/1.0");
});

// ── SignalR ──
builder.Services.AddSignalR()
    .AddJsonProtocol(options =>
    {
        options.PayloadSerializerOptions.Converters.Add(new UtcDateTimeJsonConverter());
    });

// ── Controllers + JSON ──
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
        // Stored timestamps are UTC; emit them with "Z" so browsers convert to local time correctly
        options.JsonSerializerOptions.Converters.Add(new UtcDateTimeJsonConverter());
    });

// ── CORS (allow Angular frontend) ──
builder.Services.AddCors(options =>
{
    // Public website reads (restaurant sites on their own domains call these). Read-only, no cookies.
    options.AddPolicy("PublicSite", policy => policy.AllowAnyOrigin().AllowAnyHeader().WithMethods("GET"));

    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
                config["App:FrontendUrl"] ?? "http://localhost:58738"
            )
            .SetIsOriginAllowed(origin =>
            {
                // Allow any *.tabverse.in subdomain
                var host = new Uri(origin).Host;
                return host.EndsWith(".tabverse.in", StringComparison.OrdinalIgnoreCase)
                    || host == "tabverse.in"
                    || host == "localhost"
                    // Local subdomain testing (name.localhost:4201) in development only
                    || (builder.Environment.IsDevelopment() && host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase));
            })
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// ── OpenAPI / Swagger ──
builder.Services.AddOpenApi();

var app = builder.Build();

// ── Middleware Pipeline ──
// CORS must be first so preflight OPTIONS requests get handled
app.UseCors("AllowFrontend");

app.UseMiddleware<ExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Only redirect to HTTPS in production; in dev it breaks CORS preflight
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<OrderHub>("/hubs/orders");

// ── Apply pending migrations on startup ──
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.Run();
