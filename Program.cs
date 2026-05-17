using Microsoft.EntityFrameworkCore;
using Iroh.Services;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddScoped<TableService>();
builder.Services.AddScoped<CompanyService>();
builder.Services.AddScoped<CustomerService>();
builder.Services.AddScoped<BookingService>();
builder.Services.AddScoped<BookingLogService>();


// Sadece bunları tut
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
// Doğru olan (PostgreSQL için)
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.MapControllers();

app.Run();