using System.Reflection;
using System.Text;
using FamilyCalendar.Api.Data;
using FamilyCalendar.Api.Middleware;
using FamilyCalendar.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Load configuration from appsettings.Local.json if it exists
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

// Configure structured logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Configure Swagger/OpenAPI
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Family Calendar API",
        Version = "v1",
        Description = "Multi-tenant family calendar management system with Google OAuth authentication",
        Contact = new OpenApiContact
        {
            Name = "Family Calendar Team",
            Email = "support@familycalendar.dev"
        }
    });

    // Add JWT Authentication to Swagger
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token in the text input below. Example: 'Bearer eyJhbGc...'",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });

    // Include XML comments for better documentation
    var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFilename);
    
    // Try multiple possible locations for the XML file
    string[] possiblePaths = new[]
    {
        xmlPath,
        Path.Combine(AppContext.BaseDirectory, "FamilyCalendar.Api.xml"),
        Path.Combine(Directory.GetCurrentDirectory(), "bin/Debug/net8.0", xmlFilename),
        Path.Combine(Directory.GetCurrentDirectory(), "bin/Release/net8.0", xmlFilename)
    };
    
    bool xmlFound = false;
    foreach (var path in possiblePaths)
    {
        if (File.Exists(path))
        {
            options.IncludeXmlComments(path);
            xmlFound = true;
            Console.WriteLine($"Swagger: Loading XML comments from {path}");
            break;
        }
    }
    
    if (!xmlFound)
    {
        Console.WriteLine($"Swagger: No XML documentation file found. Tried paths:");
        foreach (var path in possiblePaths)
            Console.WriteLine($"  - {path}");
    }

    // Add enum descriptions
    options.UseInlineDefinitionsForEnums();
    
    // Ensure controllers and methods are documented
    options.DocInclusionPredicate((docName, apiDesc) => true);
});

// Configure database
var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection") 
    ?? builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

// Configure HTTP context accessor (required for TenantProvider)
builder.Services.AddHttpContextAccessor();

// Configure services
builder.Services.AddScoped<ITenantProvider, TenantProvider>();
builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddScoped<IEmailService, SmtpEmailService>();

// Configure JWT authentication
var jwtSecretKey = builder.Configuration["Jwt:SecretKey"] 
    ?? throw new InvalidOperationException("JWT SecretKey is not configured");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecretKey))
    };
})
.AddGoogle(options =>
{
    options.ClientId = builder.Configuration["Google:ClientId"] 
        ?? throw new InvalidOperationException("Google ClientId is not configured");
    options.ClientSecret = builder.Configuration["Google:ClientSecret"] 
        ?? throw new InvalidOperationException("Google ClientSecret is not configured");
});

builder.Services.AddAuthorization();

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() 
            ?? new[] { "http://localhost:3000", "http://localhost:5000" };
        
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Configure health checks
builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("DefaultConnection")!);

var app = builder.Build();

// Log application startup info
app.Logger.LogInformation("Application starting in {Environment} mode", app.Environment.EnvironmentName);
app.Logger.LogInformation("Base path: {BasePath}", app.Environment.ContentRootPath);

// Apply database migrations/initialization on startup
try
{
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        app.Logger.LogInformation("Ensuring database is created with all required tables...");
        
        // Check if tables exist
        var tablesExist = false;
        try
        {
            var connection = dbContext.Database.GetDbConnection();
            connection.Open();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public' AND table_name NOT LIKE '%EFMigrationsHistory%'";
                var result = (long)command.ExecuteScalar();
                tablesExist = result > 0;
            }
            connection.Close();
        }
        catch (Exception ex)
        {
            app.Logger.LogWarning(ex, "Could not check if tables exist");
            tablesExist = false;
        }
        
        // If tables don't exist, create them
        if (!tablesExist)
        {
            app.Logger.LogInformation("Creating database schema...");
            dbContext.Database.EnsureCreated();
            app.Logger.LogInformation("Database schema created successfully");
        }
        else
        {
            app.Logger.LogInformation("Database schema already exists");
        }
    }
}
catch (Exception ex)
{
    app.Logger.LogError(ex, "Failed to initialize database");
    throw;
}

// Configure the HTTP request pipeline
app.UseMiddleware<ExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.Logger.LogInformation("Development environment detected. Enabling Swagger...");
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Family Calendar API v1");
        options.RoutePrefix = "swagger";
        options.DocumentTitle = "Family Calendar API - Swagger UI";
        options.DisplayRequestDuration();
        options.EnableDeepLinking();
        options.EnableFilter();
        options.ShowExtensions();
    });
    app.Logger.LogInformation("Swagger UI available at http://localhost:5000/swagger");
}
else
{
    app.Logger.LogInformation("Non-development environment. Swagger disabled.");
}

app.UseHttpsRedirection();
app.UseCors();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.Logger.LogInformation("Controllers mapped to endpoints");

// Health check endpoint
app.MapHealthChecks("/health");

app.Logger.LogInformation("Application ready to accept requests");
app.Run();
