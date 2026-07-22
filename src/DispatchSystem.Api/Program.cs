using DispatchSystem.Api;
using DispatchSystem.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

//services 註冊在 Build() 之前、端點掛在 Build() 之後

builder.Services.AddControllers();
builder.Services.AddHealthChecks();
builder.Services.Configure<AppOptions>(builder.Configuration.GetSection("App"));
builder.Services.AddDbContext<DispatchDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DispatchDB"))
);

var app = builder.Build();

app.MapControllers();

app.MapHealthChecks("/health");

app.MapGet("/about", (IOptions<AppOptions> options) => new
{
    options.Value.Name,
    options.Value.Version,
});

app.Run();

public partial class Program { }