using Core;
using Core.Exceptions;
using Core.Services;
using Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Npgsql;
using Service;
using Service.Services;
using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;
using Web.Conventions;

var builder = WebApplication.CreateBuilder(args);

if (builder.Configuration["DATABASE_URL"] != null)
{
    builder.Configuration["ConnectionStrings:DefaultConnection"] = builder.Configuration["DATABASE_URL"];
}

if (builder.Configuration["ConnectionStrings:DefaultConnection"] == null)
    throw new ConfigurationException("Configuration property 'ConnectionStrings:DefaultConnection' is not set.");
if (builder.Configuration["AdminPasswordHash"] == null)
    throw new ConfigurationException("Configuration property 'AdminPasswordHash' is not set.");
if (builder.Configuration["Jwt:Secret"] == null)
    throw new ConfigurationException("Configuration property 'Jwt:Secret' is not set.");

builder.Services.AddCors();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Version = "v1",
        Title = "DocsGen API"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = JwtBearerDefaults.AuthenticationScheme,
        BearerFormat = "JWT",
        Description = "JWT Authorization header using the Bearer scheme."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });

    options.CustomSchemaIds(type => type.Name.EndsWith("DTO", StringComparison.OrdinalIgnoreCase) ? type.Name[0..^3] : type.Name);

    var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlFilePath = Path.Combine(AppContext.BaseDirectory, xmlFilename);
    options.IncludeXmlComments(xmlFilePath);
});

builder.Services.AddDbContext<UniversityContext>(options =>
{
    var databaseUrl = builder.Configuration.GetConnectionString("DefaultConnection");

    var databaseUri = new Uri(databaseUrl!);
    var userInfo = databaseUri.UserInfo.Split(':');

    var conStrBuilder = new NpgsqlConnectionStringBuilder
    {
        Host = databaseUri.Host,
        Port = databaseUri.Port,
        Username = userInfo[0],
        Password = userInfo[1],
        Database = databaseUri.LocalPath.TrimStart('/')
    };

    options.UseNpgsql(conStrBuilder.ToString());

    if (builder.Environment.IsDevelopment())
    {
        options.EnableSensitiveDataLogging();
    }
});

builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

builder.Services.AddAutoMapper(typeof(MappingProfile));

builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
builder.Services.AddScoped<IDataSeedService, DataSeedService>();
builder.Services.AddScoped<IGuarantorService, GuarantorService>();
builder.Services.AddScoped<IHeadOfSmcService, HeadOfSmcService>();
builder.Services.AddScoped<IKnowledgeBranchService, KnowledgeBranchService>();
builder.Services.AddScoped<ISpecialtyService, SpecialtyService>();
builder.Services.AddScoped<ISubjectService, SubjectService>();
builder.Services.AddScoped<ISyllabusService, SyllabusService>();
builder.Services.AddScoped<ITeacherLoadService, TeacherLoadService>();
builder.Services.AddScoped<ITeacherService, TeacherService>();
builder.Services.AddScoped<ITemplateService, TemplateService>();
builder.Services.AddScoped<IDocumentService, DocumentService>();

builder.Services.AddHostedService<CleanupTemplateService>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
{
    var secret = builder.Configuration["Jwt:Secret"];

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret))
    };
});

builder.Services
    .AddControllers(options => {
        options.Conventions.Add(new ControllerDocumentationConvention());
        options.Conventions.Add(new RouteTokenTransformerConvention(new RouteParameterTransformer()));
    })
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var app = builder.Build();

app.UseCors(options =>
{
    options.AllowAnyOrigin()
    .AllowAnyHeader()
    .AllowAnyMethod();
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "DocsGen v1");
    });
}

app.UseWhen(
    context => context.Request.Path.StartsWithSegments("/api"),
    config =>
    {
        config.UseRouting();
        config.UseAuthentication();
#pragma warning disable ASP0001
        config.UseAuthorization();
#pragma warning restore ASP0001
        config.UseEndpoints(
            endpoints =>
            {
                endpoints.MapControllers();
            });
    });

app.UseWhen(
    context => !context.Request.Path.StartsWithSegments("/api"),
    config =>
    {
        config.UseStaticFiles();
        config.UseRouting();
        config.UseEndpoints(
            endpoints =>
            {
                endpoints.MapFallbackToFile("index.html");
            });
    });

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<UniversityContext>();
    var appliedMigrations = context.Database.GetAppliedMigrations();
    var isDatabaseNew = !appliedMigrations.Any();

    context.Database.Migrate();

    if (app.Environment.IsDevelopment() && isDatabaseNew)
    {
        var seeder = scope.ServiceProvider.GetRequiredService<IDataSeedService>();
        seeder.SeedTestData();
    }
}

app.Run();
