﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using tcx_util.Utilities;
using tcx_util.Utilities.Interfaces;
using tcx_util.Utilities.Services;

namespace tcx_util
{
	public static class ServiceProviderFactory
	{
		public static IServiceProvider ServiceProvider { get; set; }
	}
	public class Startup
	{
		public static IConfigurationRoot Configuration { get; set; }
		public static void Main(string[] args)
		{
			string env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
			string launch = Environment.GetEnvironmentVariable("LAUNCH_PROFILE");

			if (string.IsNullOrWhiteSpace(env))
			{
				env = "Development";
			}

			var builder = new ConfigurationBuilder()
				.SetBasePath(Directory.GetCurrentDirectory())
				//.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
				//.AddJsonFile($"appsettings.{env}.json", optional: false, reloadOnChange: true)
				.AddEnvironmentVariables();

			//if (env == "Development")
			//{
			//	builder.AddUserSecrets<Startup>();
			//}

			Configuration = builder.Build();

			// Create a service collection and configure our depdencies
			var serviceCollection = new ServiceCollection();
			ConfigureServices(serviceCollection);

			// Build the our IServiceProvider and set our static reference to it
			ServiceProviderFactory.ServiceProvider = serviceCollection.BuildServiceProvider();

			var gpsService = ServiceProviderFactory.ServiceProvider.GetService<IGpsService>();

			// Roughly 1.6km
			var start = new Coordinates() { Latitude = 47.6624556, Longitude = -122.1214378 }; // Marymoor Park Office
			var end = new Coordinates() { Latitude = 47.676215, Longitude = -122.1279931 }; // Trader Joe's
			var distance = gpsService.CalculateDistance(start, end, false);

			// Roughly 2.5km
			var coordinates = new List<Coordinates>()
			{
				new Coordinates() { Latitude = 47.6624556, Longitude = -122.1214378 }, // Marymoor Park Office
				new Coordinates() { Latitude = 47.676215, Longitude = -122.1279931 }, // Trader Joe's
				new Coordinates() { Latitude = 47.6687904, Longitude = -122.1198875 } // BJ's restaurant
			};
			distance = gpsService.CalculateDistance(coordinates, false);

			// Enter the applicaiton.. (run!)
			ServiceProviderFactory.ServiceProvider.GetService<Application>().Run(args);
		}

		private static void ConfigureServices(IServiceCollection services)
		{
			// Make configuration settings available
			//services.Configure<AppSettings>(Configuration.GetSection("AppSettings"));
			services.AddSingleton<IConfiguration>(Configuration);

			//var appSettings = new AppSettings();

			//Configuration.GetSection("AppSettings").Bind(appSettings);

			// Some libraries may still rely on web context type stuff..
			//services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
			//services.AddTransient<IViewRenderService, ViewRenderService>();
			//services.AddTransient<ICurrentUserService, CurrentUserService>();
			services.AddSingleton<IGpsService, GpsService>();

			var appConnStr = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "WindowsConnStr" : "LinuxConnStr";
			//services.AddDbContext<MyDbContext>(options =>
			//{
			//	options.UseSqlServer(Configuration.GetConnectionString(appConnStr));
			//}, ServiceLifetime.Scoped);

			// Repositories
			//services.AddScoped(typeof(IRepository<>), typeof(DomainRepository<>));

			// Add AutoMapper
			//var config = new AutoMapper.MapperConfiguration(cfg =>
			//{
			//	cfg.AddProfile(new AutoMapperProfile());
			//});

			//var mapper = config.CreateMapper();
			//services.AddSingleton(mapper);

			// Add caching
			services.AddMemoryCache();

			// Add logging            
			services.AddLogging(builder =>
			{
				builder.AddConfiguration(Configuration.GetSection("Logging"))
					.AddConsole()
					.AddDebug();
			});

			// Add Application 
			services.AddTransient<Application>();
		}
	}
}