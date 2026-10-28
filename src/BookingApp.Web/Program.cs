using System.Text.Json;
using System.Text.Json.Serialization;
using System.Security.Claims;
using BookingApp.Web.Domain;
using BookingApp.Web.Ai;
using BookingApp.Web.Scheduling;
using BookingApp.Web.Data;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Render supplies PORT. Keep launchSettings.json in control when running locally.
var port = builder.Configuration["PORT"];
if (!string.IsNullOrWhiteSpace(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

builder.Services.AddControllers(options =>
{
    options.ModelMetadataDetailsProviders.Add(new SystemTextJsonValidationMetadataProvider());
    options.OutputFormatters.RemoveType<StringOutputFormatter>();
    var jsonFormatter = options.OutputFormatters.OfType<SystemTextJsonOutputFormatter>().Single();
    jsonFormatter.SupportedMediaTypes.Remove("text/json");
}).AddJsonOptions(options =>
{
    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.JsonSerializerOptions.NumberHandling = JsonNumberHandling.Strict;
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi(options => options.AddDocumentTransformer((document, context, cancellationToken) =>
{
    document.Info.Title = "Workplace booking API";
    document.Info.Version = "v1";
    document.Info.Description = """
        ## Sign-in
        `POST /api/auth/login` sets an HttpOnly, SameSite=Lax cookie.
        `GET /api/me` returns the current user.

        ## Times
        Times are ISO 8601. A request time without an offset is office time (Europe/London); responses always include the offset.
        Bookings use a 15-minute grid and half-open intervals `[start, end)`.

        ## Errors
        All errors use `application/problem+json`:
        - **400**: field validation, with `errors` keyed by camelCase field names.
        - **401**: not signed in.
        - **403**: admin only.
        - **404**: not found or not yours.
        - **409**: resource taken (capacity is the only violation), or duplicate email.
        - **422**: booking rule violations, with `violations[{code, message}]`.
        - **503**: booking drafting is unavailable; the manual booking form remains usable.
        """;
    return Task.CompletedTask;
}));
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.AddSingleton<OfficeTime>();
builder.Services.AddSingleton<BookingSeeder>();
builder.Services.AddScoped<BookingQueries>();
builder.Services.AddScoped<BookingService>();
builder.Services.AddScoped<BookingDraftService>();
builder.Services.AddBookingDraftParser(builder.Configuration);
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    };
    options.Events.OnValidatePrincipal = async context =>
    {
        var idClaim = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        var db = context.HttpContext.RequestServices.GetRequiredService<BookingDbContext>();
        var user = int.TryParse(idClaim, out var id)
            ? await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id, context.HttpContext.RequestAborted)
            : null;
        if (user is null || !user.IsActive || !context.Principal!.IsInRole(user.Role.ToString()))
        {
            context.RejectPrincipal();
        }
    };
});
builder.Services.AddAuthorization();
builder.Services.AddDbContext<BookingDbContext>((sp, options) =>
{
    var seeder = sp.GetRequiredService<BookingSeeder>();
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default"))
        .UseSeeding((context, _) => seeder.Seed((BookingDbContext)context))
        .UseAsyncSeeding((context, _, cancellationToken) =>
            seeder.SeedAsync((BookingDbContext)context, cancellationToken));
});

var app = builder.Build();

// Fail fast if the database cannot be migrated and seeded before accepting requests.
await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
    await db.Database.MigrateAsync();
}

app.UseExceptionHandler();
app.UseStatusCodePages(async context =>
{
    var response = context.HttpContext.Response;
    var factory = context.HttpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>();
    var problem = factory.CreateProblemDetails(context.HttpContext, response.StatusCode,
        title: Microsoft.AspNetCore.WebUtilities.ReasonPhrases.GetReasonPhrase(response.StatusCode));
    await response.WriteAsJsonAsync(problem, options: (JsonSerializerOptions?)null,
        contentType: "application/problem+json", cancellationToken: context.HttpContext.RequestAborted);
});
app.UseDefaultFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapStaticAssets();
app.MapOpenApi();
app.MapScalarApiReference(options => options.WithTitle("Workplace booking API"));
app.MapControllers();

app.Run();
