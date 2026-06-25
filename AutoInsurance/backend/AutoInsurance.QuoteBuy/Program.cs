using AutoInsurance.QuoteBuy.Application.Commands.CreateQuote;
using AutoInsurance.QuoteBuy.Infrastructure.Persistence;
using AutoInsurance.QuoteBuy.Infrastructure.Persistence.Repositories;
using AutoInsurance.QuoteBuy.Infrastructure.Services;
using AutoInsurance.Shared.Interfaces;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.EntityFrameworkCore;
using Serilog;

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, lc) => lc.WriteTo.Console());

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required");

builder.Services.AddDbContext<QuoteBuyDbContext>(options => options.UseSqlServer(connectionString));
builder.Services.AddHealthChecks().AddDbContextCheck<QuoteBuyDbContext>();

builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<CreateQuoteCommand>());
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<CreateQuoteCommandValidator>();

builder.Services.AddScoped<IQuoteRepository, QuoteRepository>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddSingleton<IQuoteNumberGenerator, QuoteNumberGenerator>();

builder.Services.AddControllers();

var app = builder.Build();

app.MapHealthChecks("/health");
app.MapControllers();
app.Run();
