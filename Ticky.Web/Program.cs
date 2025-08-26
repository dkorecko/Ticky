using Devity.Mailing;
using Devity.NETCore.MailKit.Infrastructure.Internal;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.AuthenticatedEncryption;
using Microsoft.AspNetCore.DataProtection.AuthenticatedEncryption.ConfigurationModel;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages().WithRazorPagesRoot("/Components/Pages");
builder
    .Services.AddRazorComponents()
    .AddInteractiveServerComponents(options =>
    {
        options.DetailedErrors = true;
    });

builder.Services.AddDbContextFactory<DataContext>(options =>
{
    var connectionString =
#if DEBUG
        builder.Configuration.GetConnectionString("Development")
#else
        builder.Configuration.GetConnectionString("Production")
#endif
        ?? $"Server={builder.Configuration.GetValue<string>("DB_HOST")};Database={builder.Configuration.GetValue<string>("DB_NAME", "ticky")};Uid={builder.Configuration.GetValue<string>("DB_USERNAME", "ticky")};Pwd={builder.Configuration.GetValue<string>("DB_PASSWORD")};";
    options.UseMySql(
        connectionString,
        ServerVersion.Create(
            new Version("8.0.36"),
            Pomelo.EntityFrameworkCore.MySql.Infrastructure.ServerType.MySql
        )
    );
#if DEBUG
    options.EnableSensitiveDataLogging();
#endif
});

builder
    .Services.AddIdentity<User, IdentityRole<int>>(options =>
    {
        options.Password.RequiredLength = 6;
        options.Password.RequireDigit = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.SignIn.RequireConfirmedEmail = false;
    })
    .AddEntityFrameworkStores<DataContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.ExpireTimeSpan = TimeSpan.FromDays(365);
    options.SlidingExpiration = true;
    options.LoginPath = "/auth/login";

    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.Configure<DataProtectionTokenProviderOptions>(options =>
{
    options.TokenLifespan = TimeSpan.FromDays(7);
});

Constants.FULLY_OFFLINE = builder.Configuration.GetValue("FULLY_OFFLINE", true);
Constants.DISABLE_USER_SIGNUPS = builder.Configuration.GetValue("DISABLE_USER_SIGNUPS", false);
Constants.SMTP_ENABLED = builder.Configuration.GetValue("SMTP_ENABLED", false);
Constants.BASE_URL = builder
    .Configuration.GetValue("BASE_URL", "http://localhost:4088")
    .TrimEnd('/');

var mailKitOptions = builder.Configuration.GetSection("Email").Get<MailKitOptions>();

if (Constants.SMTP_ENABLED)
{
    if (mailKitOptions is null)
    {
        mailKitOptions = new MailKitOptions
        {
            Server = builder.Configuration.GetValue<string>("SMTP_HOST"),
            Port = builder.Configuration.GetValue<int>("SMTP_PORT"),
            SenderName = builder.Configuration.GetValue<string>("SMTP_DISPLAY_NAME"),
            SenderEmail = builder.Configuration.GetValue<string>("SMTP_EMAIL"),
            Password = builder.Configuration.GetValue<string>("SMTP_PASSWORD"),
            Account = builder.Configuration.GetValue<string>("SMTP_USERNAME"),
            Security = builder.Configuration.GetValue("SMTP_SECURITY", true)
        };
    }

    if (
        mailKitOptions is null
        || mailKitOptions.Server is null
        || mailKitOptions.SenderEmail is null
        || mailKitOptions.Password is null
        || mailKitOptions.Port == 0
        || mailKitOptions.SenderName is null
        || mailKitOptions.Account is null
    )
    {
        throw new InvalidOperationException(
            "Some parts of the SMTP configuration are missing. Please add the necessary environment variables. Check the repository for more information: https://github.com/dkorecko/Ticky."
        );
    }
}

builder.Services.AddScoped<CodeService>();
builder.Services.AddDevityMailing<EmailService>(mailKitOptions!);
builder.Services.AddScoped<AvatarService>();
builder.Services.AddScoped<CardNumberingService>();
builder.Services.AddScoped<SearchService>();
builder.Services.AddScoped<TrelloImportService>();
builder.Services.AddHostedService<CleanupHostedService>();
builder.Services.AddHostedService<SnoozeHostedService>();
builder.Services.AddHostedService<RepeatHostedService>();

if (Constants.SMTP_ENABLED)
    builder.Services.AddHostedService<ReminderHostedService>();

builder
    .Services.AddDataProtection()
    .SetApplicationName("ticky-application")
    .PersistKeysToDbContext<DataContext>()
    .UseCryptographicAlgorithms(
        new AuthenticatedEncryptorConfiguration
        {
            EncryptionAlgorithm = EncryptionAlgorithm.AES_256_CBC,
            ValidationAlgorithm = ValidationAlgorithm.HMACSHA256
        }
    );

builder.Services.AddHttpClient();

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(
        Constants.Policies.RequireAdmin,
        policy => policy.RequireClaim(ClaimTypes.Role, Constants.Roles.Admin).Build()
    );
});

builder.Services.AddControllers();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<DataContext>()!;

    if (dbContext.Database.IsRelational())
        dbContext.Database.Migrate();

    DataSeeder.Seed(scope.ServiceProvider).GetAwaiter().GetResult();
    DataMigrator.Seed(scope.ServiceProvider).GetAwaiter().GetResult();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseAntiforgery();

app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.MapRazorPages();

app.Run();
