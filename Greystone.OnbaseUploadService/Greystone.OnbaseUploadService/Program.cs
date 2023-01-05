using System.Reflection;

using Greystone.OnbaseUploadService.Database;
using Greystone.OnbaseUploadService.Services.Keywords;
using Greystone.OnbaseUploadService.Services.Locking;
using Greystone.OnbaseUploadService.Services.SessionManagement;

using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

var services = builder.Services;
// Add services to the container.

services.AddControllers();

services.AddDbContext<OnbaseUploadServiceDbContext>(
	cfg => cfg.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

services.AddOnBaseSessionManagement().AddFileLocking();

services.AddScoped<IKeywordService, KeywordService>();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
services.AddEndpointsApiExplorer();
services.AddSwaggerGen(
	cfg =>
	{
		cfg.SwaggerDoc(
			"v1",
			new OpenApiInfo
			{
				Version = "V1",
				Title = "Greystone OnBase Upload Service",
				Description = "An OnBase upload REST API produced for Greystone, Inc.",
				Contact = new OpenApiContact
				{
					Name = "Conner Phillis",
					Email = "conner.phillis@keymarkinc.com",
					Url = new Uri("https://keymarkinc.com")
				},
			});
		var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
		var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
		cfg.IncludeXmlComments(xmlPath);
	});

var app = builder.Build();

// await InitializeDatabase(app);

// Configure the HTTP request pipeline.
// app.UseHttpsRedirection();

if (app.Environment.IsDevelopment())
{
	app.UseSwagger();
	app.UseSwaggerUI();
}

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
