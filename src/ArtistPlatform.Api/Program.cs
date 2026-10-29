using System.Text.Json.Serialization;
using ArtistPlatform.Api.Json;
using ArtistPlatform.Api.Middleware;
using ArtistPlatform.Application;
using ArtistPlatform.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    // Wall clock: horários saem sem sufixo de fuso (o sistema trata tudo como hora do Brasil)
    options.JsonSerializerOptions.Converters.Add(new ArtistPlatform.Api.Json.WallClockDateTimeConverter());
});

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHttpContextAccessor();

builder.Services.AddCors(options => options.AddPolicy("Frontend", policy => policy
    .WithOrigins(
        "http://localhost:5173",
        "http://localhost:3000",
        "http://frontend:80",
        "http://localhost",
        "https://artists-frontend-psi.vercel.app")
    .AllowAnyHeader()
    .AllowAnyMethod()));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo
    {
        Title = "Artist Platform API",
        Version = "v1",
        Description = "Sistema de gestão de carreira + marketplace para contratação de artistas (PRD v1.0)."
    });
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.ParameterLocation.Header,
        Description = "Informe o token JWT (sem o prefixo Bearer)."
    });
    options.AddSecurityRequirement(document => new Microsoft.OpenApi.OpenApiSecurityRequirement
    {
        [new Microsoft.OpenApi.OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});

// Garante wwwroot/uploads ANTES do Build: o UseStaticFiles resolve o provider do web root na
// construção do pipeline — sem a pasta, ele não serve nada pelo resto da vida do processo.
Directory.CreateDirectory(Path.Combine(builder.Environment.ContentRootPath, "wwwroot", "uploads"));

var app = builder.Build();

app.UseMiddleware<ExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<SubscriptionGateMiddleware>();
app.MapControllers();

await app.Services.InitializeDatabaseAsync();

app.Run();
