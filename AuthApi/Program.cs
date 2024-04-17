var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDataProtection()
    .UseCryptographicAlgorithms(
        new AuthenticatedEncryptorConfiguration {
            EncryptionAlgorithm = EncryptionAlgorithm.AES_256_CBC,
            ValidationAlgorithm = ValidationAlgorithm.HMACSHA512
        })
    .PersistKeysToFileSystem(new DirectoryInfo(builder.Configuration["KeyPath"] ?? "/keys"));

builder.Services.AddAuthorizationBuilder();

builder.Services.AddDbContextFactory<ModelDbContext>(
    options => {
        options.UseMySql(
            builder.Configuration.GetConnectionString("DefaultConnection"),
            new MySqlServerVersion(new Version(8, 0, 31)),
            optionsBuilder =>
                optionsBuilder.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery));
        options.EnableSensitiveDataLogging();
        options.UseLoggerFactory(new NullLoggerFactory());
    },
    ServiceLifetime.Transient
);

builder.Services.AddTransient<IEmailSender, EmailSender>();

builder.Services.Configure<MailSettings>(builder.Configuration.GetSection("MailSettings"))
    .AddOptionsWithValidateOnStart<MailSettings>()
    .ValidateDataAnnotations();

//builder.Services.AddAuthentication(options => {
//        options.DefaultAuthenticateScheme = IdentityConstants.BearerScheme;
//        options.DefaultChallengeScheme = IdentityConstants.BearerScheme;
//    })
//    .AddBearerToken(IdentityConstants.BearerScheme, options => {
//        options.BearerTokenExpiration = TimeSpan.FromDays(5);
//        options.RefreshTokenExpiration = TimeSpan.FromDays(30);
//        options.Validate();
//    });

builder.Services.AddAuthorizationBuilder()
    .SetDefaultPolicy(new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .AddAuthenticationSchemes(IdentityConstants.ApplicationScheme/*,
            IdentityConstants.BearerScheme*/)
        .Build());


builder.Services.Configure<IdentityOptions>(options => {
    // Default Lockout settings.
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;

    options.User.RequireUniqueEmail = true;
    options.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+ ";
    // Default Password settings.
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = true;
    options.Password.RequiredLength = 6;
    options.Password.RequiredUniqueChars = 1;
});

// cookie auth
builder.Services.ConfigureApplicationCookie(options => {
    options.Cookie.HttpOnly = true;
#if RELEASE
    options.Cookie.Domain = builder.Configuration["CookieDomain"] ?? throw new Exception("CookieDomain not set");
#else
    options.Cookie.Domain = "localhost";
    options.Cookie.SecurePolicy = CookieSecurePolicy.None;
#endif
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.LoginPath = "/auth/login";
    options.LogoutPath = "/auth/logout";
});


builder.Services.AddIdentity<AppUser, IdentityRole>()
    .AddEntityFrameworkStores<ModelDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddCors();

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => { c.CustomSchemaIds(type => type.FullName); });


// metrics

builder.AddMetrics();


// serilog
builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration));


var app = builder.Build();

if (app.Environment.IsDevelopment()) {
    app.UseSwagger();
    app.UseSwaggerUI();
}

// for nginx
app.UseForwardedHeaders(new ForwardedHeadersOptions {
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});


var dbCon = app.Services.GetRequiredService<ModelDbContext>();
var origins = await dbCon.CorsOrigins.AsNoTracking()
    .Select(c => c.Origin).ToArrayAsync();

app.UseCors(policy => policy.WithOrigins(origins)
    .AllowCredentials().AllowAnyHeader().AllowAnyMethod());

// metrics
app.MapMetrics();

// serilog
app.UseSerilogRequestLogging();

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();