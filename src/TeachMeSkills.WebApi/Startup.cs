﻿using System;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using NETCore.MailKit.Extensions;
using NETCore.MailKit.Infrastructure.Internal;
using Serilog;
using TeachMeSkills.BusinessLogicLayer.Interfaces;
using TeachMeSkills.BusinessLogicLayer.Managers;
using TeachMeSkills.BusinessLogicLayer.Repository;
using TeachMeSkills.DataAccessLayer.Contexts;
using TeachMeSkills.DataAccessLayer.Entities;
using TeachMeSkills.WebApi.Constants;
using TeachMeSkills.WebApi.Extensions;
using TeachMeSkills.WebApi.Helpers;

namespace TeachMeSkills.WebApi
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            // Options
            services.Configure<JwtSettings>(Configuration.GetSection("Jwt"));

            // Managers
            services.AddScoped(typeof(IRepositoryManager<>), typeof(RepositoryManager<>));
            services.AddScoped<IAccountManager, AccountManager>();
            services.AddScoped<ITodoManager, TodoManager>();

            // Database context
            services.AddDbContext<TeachMeSkillsContext>(options =>
                options.UseSqlServer(Configuration.GetConnectionString("TeachMeSkillsConnection")));

            // ASP.NET Core Identity
            services.AddIdentity<User, IdentityRole>()
                .AddEntityFrameworkStores<TeachMeSkillsContext>();

            // Microsoft services
            services.AddMemoryCache();
            services.AddControllers();
            services.AddCors();
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.RequireHttpsMetadata = false;
                    options.SaveToken = true;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = Configuration["Jwt:Issuer"],
                        ValidAudience = Configuration["Jwt:Audience"],
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Configuration["Jwt:SecretKey"])),
                        ClockSkew = TimeSpan.Zero
                    };
                });

            // NuGet services
            var mailKitOptions = Configuration.GetSection("Mail").Get<MailKitOptions>();
            services.AddMailKit(optionBuilder =>
            {
                optionBuilder.UseMailKit(new MailKitOptions()
                {
                    Server = mailKitOptions.Server,
                    Port = mailKitOptions.Port,
                    SenderName = mailKitOptions.SenderName,
                    SenderEmail = mailKitOptions.SenderEmail,
                    Account = mailKitOptions.Account,
                    Password = mailKitOptions.Password,
                    Security = true
                });
            });
            services.AddSwaggerGen(config =>
            {
                config.SwaggerDoc(SwaggerParameter.Version, new OpenApiInfo
                {
                    Version = SwaggerParameter.Version,
                    Title = SwaggerParameter.Title,
                    Description = SwaggerParameter.Description,
                    Contact = new OpenApiContact()
                    {
                        Name = SwaggerParameter.Contact.Name,
                    }
                });

                var securitySchema = new OpenApiSecurityScheme
                {
                    Description = SwaggerParameter.Security.Description,
                    Name = SwaggerParameter.Security.Name,
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.Http,
                    Scheme = SwaggerParameter.Security.HttpAuth,
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = SwaggerParameter.Security.Schema
                    }
                };
                config.AddSecurityDefinition(SwaggerParameter.Security.Schema, securitySchema);

                var securityRequirement = new OpenApiSecurityRequirement
                {
                    {
                        securitySchema, new[] { SwaggerParameter.Security.Schema }
                    }
                };
                config.AddSecurityRequirement(securityRequirement);
            });
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseSerilogRequestLogging();

            app.UseCors(x => x
                .SetIsOriginAllowed(origin => true)
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials());

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseSwagger();
            app.UseSwaggerUI(config => config.SwaggerEndpoint("/swagger/v1/swagger.json", SwaggerParameter.Description));

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseMiddleware<ErrorHandlerMiddleware>();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
