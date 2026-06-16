using System.Text;
using Iroh.Models.Entities;
using Iroh.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

// .env dosyasındaki değişkenleri yükle
DotNetEnv.Env.Load();

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// JWT Ayarlarını oku
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"]
    ?? throw new InvalidOperationException("JwtSettings:SecretKey yapılandırılmamış (appsettings.Development.json veya ortam değişkeni).");

// Authentication ve JWT Bearer servisini ekle
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
        ValidIssuer = jwtSettings["Issuer"],
        ValidateAudience = true,
        ValidAudience = jwtSettings["Audience"],
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        ClockSkew = TimeSpan.FromMinutes(1)
    };
});

builder.Services.AddAuthorization();

// CORS: izin verilen origin'ler config'ten (Cors:AllowedOrigins) gelir; hardcode yok.
// Dev'de değer yoksa Vite varsayılanına (localhost:5173) düşer; prod'da boşsa hiçbir cross-origin'e izin verilmez (fail-closed).
const string CorsPolicyName = "IrohClient";
var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
if (corsOrigins.Length == 0 && builder.Environment.IsDevelopment())
{
    corsOrigins = new[] { "http://localhost:5173", "http://localhost:3000", "https://playground-management.vercel.app" };
}

builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicyName, policy =>
    {
        policy.WithOrigins(corsOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();   // refreshToken HttpOnly cookie'sinin cross-origin taşınabilmesi için
    });
});

// Global hata yönetimi: ProblemDetails (RFC7807) + IExceptionHandler
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<Iroh.Infrastructure.GlobalExceptionHandler>();

// Servis Kayıtları
builder.Services.AddScoped<ITableService, TableService>();
builder.Services.AddScoped<ICompanyService, CompanyService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<IBookingService, BookingService>();
builder.Services.AddScoped<IBookingLogService, BookingLogService>();
builder.Services.AddScoped<IPurchasePaymentService, PurchasePaymentService>();
builder.Services.AddScoped<IPurchaseService, PurchaseService>();
builder.Services.AddScoped<IPackageService, PackageService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IChildService, ChildService>();
builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();

builder.Services.AddControllers(options =>
    {
        // Route token'ları kebab-case: /api/booking-log, /api/purchase-payment (C3)
        options.Conventions.Add(new Microsoft.AspNetCore.Mvc.ApplicationModels.RouteTokenTransformerConvention(
            new Iroh.Infrastructure.KebabCaseParameterTransformer()));
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        // C# property'leri PascalCase; JSON çıktısı camelCase kalsın.
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        // Client ID/sayı alanlarını JSON string olarak gönderiyor (örn. tableId:"5", userId:"12");
        // sayısal alanların string'ten de okunabilmesine izin ver, aksi halde model-binding 400 atar.
        options.JsonSerializerOptions.NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString;
    });

builder.Services.AddEndpointsApiExplorer();

// Swagger Yapılandırması
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Iroh API", Version = "v1" });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Token değerini yapıştırın (Başına Bearer eklemeden deneyin)",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
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
            new string[] {}
        }
    });
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

// Yakalanmayan tüm hatalar GlobalExceptionHandler üzerinden ProblemDetails'e dönüşür.
app.UseExceptionHandler();

app.UseSwagger();
app.UseSwaggerUI();

// app.UseHttpsRedirection(); // Yerel testlerde token kaybını önlemek için kapattık

// CORS, authentication/authorization'dan ÖNCE çalışmalı (preflight isteklerinin geçmesi için).
app.UseCors(CorsPolicyName);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
