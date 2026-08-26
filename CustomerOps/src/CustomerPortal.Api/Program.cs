using Asp.Versioning;
using CustomerPortal.Api.ErrorHandling;
using CustomerPortal.Application.Customers;
using CustomerPortal.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

builder.Services.AddDbContext<CustomerPortalDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("CustomerPortal")));

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
});


builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
builder.Services.AddScoped<CustomerService>();
builder.Services.AddValidatorsFromAssemblyContaining<CreateCustomerRequestValidator>();

builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1.0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
}).AddMvc();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<CustomerApiExceptionHandler>();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy("LocalDevelopment", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CustomerPortalDbContext>();
    db.Database.Migrate();
}

app.UseExceptionHandler();
app.UseHttpsRedirection();
app.UseCors("LocalDevelopment");
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

public partial class Program { }
