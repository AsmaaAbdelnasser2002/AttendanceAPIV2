using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;

namespace AttendanceAPIV2.Extentions
{
    public static class CustomJwtAuthExtention
    {
        public static void AddCustomJwtAuth(this IServiceCollection services, ConfigurationManager configuration)
        {
            services.AddAuthentication(o =>
            {
                o.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                o.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                o.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
            }).AddJwtBearer(o =>
            {
                o.RequireHttpsMetadata = false;
                o.SaveToken = true;
                o.TokenValidationParameters = new TokenValidationParameters()
                {
                    ValidateIssuer = true,
                    ValidIssuer = configuration["JWT:Issuer"],
                    ValidateAudience = false,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["JWT:SecretKey"]))
                };
            });
        }
        public static void AddSwaggerGenJwtAuth(this IServiceCollection services/*, ConfigurationManager configuration*/)
        {
            services.AddSwaggerGen(o =>
            {
                o.SwaggerDoc("v1",new OpenApiInfo()
                {
                    Version = "v1", 
                    Title="test api",
                    Description="abcd",
                    Contact=new OpenApiContact()
                    {
                        Name="Asmaa",
                        Email="Asmaa@gmail.com",
                        Url=new Uri("https://mydomain.com")
                    }
                });

                o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme() 
                {
                   Name="Authorization",
                   Type=SecuritySchemeType.ApiKey,
                   Scheme="Bearer",
                   BearerFormat="JWT",
                   In =ParameterLocation.Header,
                   Description="Enter the JWT key"
                });
                o.AddSecurityRequirement(new OpenApiSecurityRequirement() { 
                  {
                     new OpenApiSecurityScheme()
                    {
                        Reference =new OpenApiReference()
                        {
                            Type =ReferenceType.SecurityScheme,
                            Id="Bearer"
                        },
                        Name="Bearer",
                        In=ParameterLocation.Header
                    },
                    new List<string>()
                  }
                });
            });
        }

    } 
}
