using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Bebochka.Api.Data;
using Bebochka.Api.Services;
using Bebochka.Api.Models;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http;

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel to avoid connection reuse issues
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(2);
    options.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(1);
    options.Limits.MaxRequestBodySize = 50 * 1024 * 1024; // 50MB
    options.Limits.MaxResponseBufferSize = 50 * 1024 * 1024; // 50MB
    
    // Увеличиваем минимальную скорость чтения тела запроса для больших файлов
    // Устанавливаем очень низкий порог, чтобы не прерывать медленные загрузки
    options.Limits.MinRequestBodyDataRate = new MinDataRate(
        bytesPerSecond: 100, // Минимум 100 байт/сек (очень низкий порог)
        gracePeriod: TimeSpan.FromMinutes(5) // Даем 5 минут на загрузку
    );
    options.Limits.MinResponseDataRate = new MinDataRate(
        bytesPerSecond: 100,
        gracePeriod: TimeSpan.FromMinutes(5)
    );
    
    options.AllowSynchronousIO = false;
});

// Configure form options for large file uploads
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 52428800; // 50MB
    options.ValueLengthLimit = int.MaxValue;
    options.MultipartHeadersLengthLimit = int.MaxValue;
    options.MultipartBoundaryLengthLimit = int.MaxValue;
    options.BufferBody = false; // Не буферизуем тело запроса для больших файлов
    options.BufferBodyLengthLimit = long.MaxValue;
    options.KeyLengthLimit = int.MaxValue;
});

// Add services to the container
builder.Services.AddControllers(options =>
{
    // Увеличиваем лимиты для multipart/form-data
    options.MaxModelBindingCollectionSize = int.MaxValue;
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Bebochka API",
        Version = "v1",
        Description = "API for managing children's second-hand clothing products",
        Contact = new Microsoft.OpenApi.Models.OpenApiContact
        {
            Name = "Bebochka Support",
            Email = "support@bebochka.com"
        }
    });
    
    // Include XML comments
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }
    
    // Настройка для поддержки загрузки файлов в Swagger
    c.MapType<IFormFile>(() => new Microsoft.OpenApi.Models.OpenApiSchema
    {
        Type = "string",
        Format = "binary"
    });
    
    c.MapType<System.Collections.Generic.List<IFormFile>>(() => new Microsoft.OpenApi.Models.OpenApiSchema
    {
        Type = "array",
        Items = new Microsoft.OpenApi.Models.OpenApiSchema
        {
            Type = "string",
            Format = "binary"
        }
    });
    
    // Add JWT authentication to Swagger
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token in the text input below.",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Add CORS with more permissive settings
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp", policy =>
    {
        policy.WithOrigins(
                "http://localhost:5173", 
                "http://localhost:3000",
                "http://localhost:5000",
                "http://89.104.67.36:55502",
                "http://89.104.67.36:55501",
                "http://127.0.0.1:5173",
                "http://127.0.0.1:3000",
                "http://127.0.0.1:5000",
                "http://89.104.67.36",
                "http://89.104.67.36:80"
              )
              .SetIsOriginAllowed(origin => true) // Allow any origin for development
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials()
              .WithExposedHeaders("Content-Type", "Content-Length", "Access-Control-Allow-Origin");
    });
    
    // Default policy for all requests
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Add DbContext - только MySQL
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

// Используем MySQL 8.0.33 как базовую версию (можно изменить под вашу версию)
var serverVersion = new MySqlServerVersion(new Version(8, 0, 33));
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, serverVersion));

// Add JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"] ?? "YourSuperSecretKeyForJWTTokenGenerationThatShouldBeAtLeast32CharactersLong!";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "BebochkaApi";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "BebochkaClient";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ValidateIssuer = true,
        ValidIssuer = jwtIssuer,
        ValidateAudience = true,
        ValidAudience = jwtAudience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

// Add services
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IAuthService, AuthService>();

var app = builder.Build();

// Configure the HTTP request pipeline
// Add request logging middleware - должно быть ПЕРЕД CORS
// ВАЖНО: Для multipart запросов НЕ читаем body, чтобы не блокировать поток
app.Use(async (context, next) =>
{
    var startTime = DateTime.UtcNow;
    var requestId = Guid.NewGuid().ToString("N")[..8];
    var isMultipart = context.Request.ContentType?.Contains("multipart") == true;
    
    try
    {
        Console.WriteLine($"[{startTime:yyyy-MM-dd HH:mm:ss.fff}] [{requestId}] ========== INCOMING REQUEST ==========");
        Console.WriteLine($"[{startTime:yyyy-MM-dd HH:mm:ss.fff}] [{requestId}] Method: {context.Request.Method}");
        Console.WriteLine($"[{startTime:yyyy-MM-dd HH:mm:ss.fff}] [{requestId}] Path: {context.Request.Path}{context.Request.QueryString}");
        Console.WriteLine($"[{startTime:yyyy-MM-dd HH:mm:ss.fff}] [{requestId}] Remote IP: {context.Connection.RemoteIpAddress}");
        Console.WriteLine($"[{startTime:yyyy-MM-dd HH:mm:ss.fff}] [{requestId}] Origin: {context.Request.Headers["Origin"]}");
        Console.WriteLine($"[{startTime:yyyy-MM-dd HH:mm:ss.fff}] [{requestId}] Content-Type: {context.Request.ContentType}");
        Console.WriteLine($"[{startTime:yyyy-MM-dd HH:mm:ss.fff}] [{requestId}] Content-Length: {context.Request.ContentLength}");
        Console.WriteLine($"[{startTime:yyyy-MM-dd HH:mm:ss.fff}] [{requestId}] Authorization: {(context.Request.Headers.ContainsKey("Authorization") ? "Present" : "Missing")}");
        Console.WriteLine($"[{startTime:yyyy-MM-dd HH:mm:ss.fff}] [{requestId}] User-Agent: {context.Request.Headers["User-Agent"]}");
        Console.WriteLine($"[{startTime:yyyy-MM-dd HH:mm:ss.fff}] [{requestId}] HasFormContentType: {context.Request.HasFormContentType}");
        
        if (isMultipart)
        {
            Console.WriteLine($"[{startTime:yyyy-MM-dd HH:mm:ss.fff}] [{requestId}] Multipart request detected - skipping body reading to avoid blocking");
        }
        
        // Для multipart запросов НЕ перехватываем response body, чтобы не блокировать
        if (isMultipart)
        {
            Console.WriteLine($"[{startTime:yyyy-MM-dd HH:mm:ss.fff}] [{requestId}] Passing multipart request directly to next middleware");
            await next();
            
            var endTime = DateTime.UtcNow;
            var duration = (endTime - startTime).TotalMilliseconds;
            Console.WriteLine($"[{endTime:yyyy-MM-dd HH:mm:ss.fff}] [{requestId}] Status Code: {context.Response.StatusCode}");
            Console.WriteLine($"[{endTime:yyyy-MM-dd HH:mm:ss.fff}] [{requestId}] Duration: {duration:F2} ms");
            Console.WriteLine($"[{endTime:yyyy-MM-dd HH:mm:ss.fff}] [{requestId}] ========== END REQUEST ==========");
        }
        else
        {
            // Для обычных запросов читаем response body для логирования
            var originalBodyStream = context.Response.Body;
            using var responseBody = new MemoryStream();
            context.Response.Body = responseBody;
            
            await next();
            
            var endTime = DateTime.UtcNow;
            var duration = (endTime - startTime).TotalMilliseconds;
            
            responseBody.Seek(0, SeekOrigin.Begin);
            var responseBodyText = await new StreamReader(responseBody).ReadToEndAsync();
            responseBody.Seek(0, SeekOrigin.Begin);
            await responseBody.CopyToAsync(originalBodyStream);
            
            Console.WriteLine($"[{endTime:yyyy-MM-dd HH:mm:ss.fff}] [{requestId}] Status Code: {context.Response.StatusCode}");
            Console.WriteLine($"[{endTime:yyyy-MM-dd HH:mm:ss.fff}] [{requestId}] Duration: {duration:F2} ms");
            Console.WriteLine($"[{endTime:yyyy-MM-dd HH:mm:ss.fff}] [{requestId}] Response Size: {responseBodyText.Length} bytes");
            if (responseBodyText.Length > 0 && responseBodyText.Length < 1000)
            {
                Console.WriteLine($"[{endTime:yyyy-MM-dd HH:mm:ss.fff}] [{requestId}] Response Body: {responseBodyText}");
            }
            Console.WriteLine($"[{endTime:yyyy-MM-dd HH:mm:ss.fff}] [{requestId}] ========== END REQUEST ==========");
        }
    }
    catch (Exception ex)
    {
        var errorTime = DateTime.UtcNow;
        var duration = (errorTime - startTime).TotalMilliseconds;
        Console.WriteLine($"[{errorTime:yyyy-MM-dd HH:mm:ss.fff}] [{requestId}] [ERROR] Exception after {duration:F2} ms: {ex.Message}");
        Console.WriteLine($"[{errorTime:yyyy-MM-dd HH:mm:ss.fff}] [{requestId}] [ERROR] Exception Type: {ex.GetType().Name}");
        Console.WriteLine($"[{errorTime:yyyy-MM-dd HH:mm:ss.fff}] [{requestId}] [ERROR] StackTrace: {ex.StackTrace}");
        if (ex.InnerException != null)
        {
            Console.WriteLine($"[{errorTime:yyyy-MM-dd HH:mm:ss.fff}] [{requestId}] [ERROR] Inner Exception: {ex.InnerException.Message}");
        }
        throw;
    }
});

// CORS must be very early in the pipeline, before UseRouting
app.UseCors("AllowReactApp");

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Bebochka API v1");
    c.RoutePrefix = "swagger";
});

// Serve static files (for uploaded images)
var wwwrootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
if (!Directory.Exists(wwwrootPath))
{
    Directory.CreateDirectory(wwwrootPath);
}

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(wwwrootPath),
    RequestPath = ""
});

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Ensure database is created and create default admin user
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    dbContext.Database.EnsureCreated();
    
    // Create default admin user if not exists
    if (!dbContext.Users.Any(u => u.Username == "admin"))
    {
        var adminUser = new User
        {
            Username = "admin",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin123!"),
            Email = "admin@bebochka.com",
            FullName = "Администратор",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        dbContext.Users.Add(adminUser);
        dbContext.SaveChanges();
        Console.WriteLine("Default admin user created: admin / Admin123!");
    }
}

// Configure listening URLs
app.Urls.Add("http://0.0.0.0:44315");
app.Urls.Add("http://localhost:5000");

app.Run();
