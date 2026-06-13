using IndigoLabs.Api.Authentication;
using IndigoLabs.Api.Options;
using IndigoLabs.Api.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition(BasicAuthenticationDefaults.AuthenticationScheme, new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "basic",
        In = ParameterLocation.Header,
        Description = "Basic authentication using the configured API username and password."
    });

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecuritySchemeReference(BasicAuthenticationDefaults.AuthenticationScheme, document, null),
            []
        }
    });
});
builder.Services.AddOpenApi();

builder.Services
    .AddAuthentication(BasicAuthenticationDefaults.AuthenticationScheme)
    .AddScheme<AuthenticationSchemeOptions, BasicAuthenticationHandler>(
        BasicAuthenticationDefaults.AuthenticationScheme,
        options => { });

builder.Services.AddAuthorization();

builder.Services.Configure<MeasurementDataOptions>(
    builder.Configuration.GetSection(MeasurementDataOptions.SectionName));

builder.Services.AddSingleton<IMeasurementStatisticsService, MeasurementStatisticsService>();
builder.Services.AddHostedService<MeasurementCacheWarmupService>();
builder.Services.AddHostedService<MeasurementFileWatcherService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program;
