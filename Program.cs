using System.Text;
using Iroh.Models.Entities;
using Iroh.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

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

// Global hata yönetimi: ProblemDetails (RFC7807) + IExceptionHandler
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<Iroh.Infrastructure.GlobalExceptionHandler>();

// Servis Kayıtları
builder.Services.AddScoped<TableService>();
builder.Services.AddScoped<CompanyService>();
builder.Services.AddScoped<CustomerService>();
builder.Services.AddScoped<BookingService>();
builder.Services.AddScoped<BookingLogService>();
builder.Services.AddScoped<PurchasePaymentService>();
builder.Services.AddScoped<PurchaseService>();
builder.Services.AddScoped<PackageService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<ChildService>();
builder.Services.AddScoped<SubscriptionService>();

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

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// app.UseHttpsRedirection(); // Yerel testlerde token kaybını önlemek için kapattık

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
