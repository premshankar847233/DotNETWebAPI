using CityInfo.API;
using CityInfo.API.DbContexts;
using CityInfo.API.Services;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using System.Text;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .WriteTo.File("logs/cityinfo.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();
var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();

//builder.Logging.ClearProviders();
//builder.Logging.AddConsole();
// Add services to the container.

builder.Services.AddControllers(options =>
{
    options.ReturnHttpNotAcceptable = true;
}).AddNewtonsoftJson()
  .AddXmlDataContractSerializerFormatters();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<FileExtensionContentTypeProvider>();
builder.Services.AddSingleton<IStartupFilter>(new PathBaseStartupFilter("/WebAPI"));

#if DEBUG
    builder.Services.AddTransient<IMailService, LocalMailService>();
#else
    builder.Services.AddTransient<IMailService, CloudMailService>();
#endif

builder.Services.AddSingleton<CitiesDataStore>();
builder.Services.AddDbContext<CityInfoContext>(options =>
{
    options.UseSqlServer(builder.Configuration["ConnectionStrings:CityInfoDbConnectionString"]);
});
builder.Services.AddScoped<ICityInfoRepository, CityInfoRepository>();
builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());
builder.Services.AddAuthentication("Bearer")
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new()
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Authentication:Issuer"],
        ValidAudience = builder.Configuration["Authentication:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.ASCII.GetBytes(builder.Configuration["Authentication:SecretForKey"]
            ))
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("MustBeFromAntwerp", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireClaim("city", "Antwerp");
    });
});

var app = builder.Build();
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
