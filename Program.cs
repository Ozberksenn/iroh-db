using System.Text;
using Iroh.Models.Entities;
using Iroh.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

// .env dosyasındaki değişkenleri yükle
DotNetEnv.Env.Load();

// TIMEZONE STANDARDI: tüm zaman damgaları UTC instant olarak saklanır/işlenir.
// timestamptz <-> DateTime(Kind=Utc) eşlenir; DB'ye yalnızca Kind=Utc DateTime yazılabilir
// (Local/Unspecified yazımı Npgsql tarafından REDDEDİLİR -> kasıtlı koruma, sessiz kaymayı önler).
// İş günü / görüntüleme için UTC -> Europe/Istanbul çevrimi açıkça yapılır (DashboardController + client).
// Legacy davranış KAPALI tutulur; bunu true yapmak "duvar-saatini yerel say" hatasını geri getirir.
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", false);

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
var corsAllowedOrigins = builder.Configuration["Cors:AllowedOrigins"];
var corsOrigins = corsAllowedOrigins?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();

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
builder.Services.AddScoped<IPackageService, PackageService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IChildService, ChildService>();
builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();
builder.Services.AddScoped<IPricingService, PricingService>();
builder.Services.AddScoped<IWalletService, WalletService>();

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
        // Enum'lar string olarak okunup yazılsın (client status:"Active", settlement:"Debt" gönderir;
        // DTO'lar da enum'u ToString()'ler). Converter yoksa enum girişi sayı beklenir → model-binding 400.
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
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

// AddDbContextPool: DbContext instance'ları havuzlanır (her istekte yeniden kurulum/allocation
// maliyetini düşürür). Güvenli, çünkü AppDbContext yalnızca DbContextOptions alır (başka scoped
// servis enjekte etmez) — pooling'in ön koşulu.
builder.Services.AddDbContextPool<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

// Uygulama ayağa kalkarken eksik veri tabanı göçlerini (migrations) otomatik olarak uygular
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    dbContext.Database.Migrate();
}

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
