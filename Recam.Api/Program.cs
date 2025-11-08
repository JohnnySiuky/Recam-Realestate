using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using PMS.API.Setup;
using Recam.Common.Auth;
using Recam.Common.Auth.Implementation;
using Recam.Common.Email;
using Recam.Common.Exceptions.Middlewares;
using Recam.Common.Extensions;
using Recam.Common.Filters;
using Recam.Common.Mongo;
using Recam.Common.Storage;
using Recam.DataAccess;
using Recam.Models.Entities;
using Recam.Respostitories.Inplementations;
using Recam.Respostitories.Interfaces;
using Recam.Services.DTOs;
using Recam.Services.Email;
using Recam.Services.Interfaces;
using Recam.Services.Logging.Implementation;
using Recam.Services.Logging.interfaces;
using Recam.Services.Validators.Auth;

namespace PMS.API;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // --- Repositories & Services ---
        builder.Services.AddScoped<IPhotographyCompanyRepository, PhotographyCompanyRepository>();
        builder.Services.AddScoped<IPhotographyCompanyService, PhotographyCompanyService>();
        builder.Services.AddScoped<IIdentityUserService, IdentityUserService>();
        builder.Services.AddScoped<IAgentRepository, AgentRepository>();
        builder.Services.AddScoped<IAgentAdminService, AgentAdminService>();
        builder.Services.AddScoped<IUserInfoService, UserInfoService>();
        builder.Services.AddScoped<IMediaService, MediaService>();
        builder.Services.AddScoped<IMediaRepository, MediaRepository>();
        builder.Services.AddScoped<IAgentQueryService, AgentQueryService>();
        builder.Services.AddScoped<IListingCaseRepository, ListingCaseRepository>();
        builder.Services.AddScoped<IListingCaseService, ListingCaseService>();
        builder.Services.AddScoped<ICaseHistoryRepository, SqlCaseHistoryRepository>();
        builder.Services.AddScoped<ICaseHistoryService, SqlCaseHistoryService>();
        builder.Services.AddSingleton<IBlobStorageService, AzureBlobStorageService>();
        builder.Services.AddSingleton<IUploadPolicy, DefaultUploadPolicy>();
        builder.Services.AddScoped<IMediaUploadService, MediaUploadService>();
        builder.Services.AddScoped<IMediaAssetRepository, MediaAssetRepository>();
        builder.Services.AddScoped<IMediaAssetService, MediaAssetService>();
        builder.Services.AddScoped<ISelectedMediaRepository, SelectedMediaRepository>();
        builder.Services.AddScoped<IFinalSelectionService, FinalSelectionService>();
        builder.Services.AddSingleton<IMediaSelectionLogService, NoopMediaSelectionLogService>();
        builder.Services.AddScoped<IListingAuditLogService, ListingAuditLogService>();
        
        // public listing
        // 1. 读配置节 "PublicListing" 到一个 PublicListingOptions 实例
        var publicListingOptions = new PublicListingOptions();
        builder.Configuration.GetSection("PublicListing").Bind(publicListingOptions);

        // 2. 把该实例注册成单例，后面 ListingCaseService 直接拿这个
        builder.Services.AddSingleton(publicListingOptions);
        
        // blob
        builder.Services.Configure<AzureBlobStorageOptions>(builder.Configuration.GetSection("AzureBlobStorage"));
        // Controllers + 全局过滤器 + Enum 序列化
        builder.Services
            .AddControllers(opt => opt.Filters.Add<ApiValidationFilter>())
            .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        // FluentValidation
        builder.Services
            .AddFluentValidationAutoValidation()
            .AddFluentValidationClientsideAdapters();
        builder.Services.AddValidatorsFromAssemblyContaining<RegisterPhotographyCompanyRequestValidator>();
        builder.Services.AddValidatorsFromAssemblyContaining<UpdateListingCaseRequest>();

        // DbContext
        builder.Services.AddDbContext<RecamDbContext>(opt =>
            opt.UseSqlServer(builder.Configuration.GetConnectionString("RecamDb")));

        // Mongo 审计（只保留一次注册，避免被后面覆盖）
        var mongo = builder.Configuration.GetSection("Mongo").Get<MongoSettings>() ?? new MongoSettings();
        builder.Services.Configure<MongoSettings>(builder.Configuration.GetSection("Mongo"));
        builder.Services.AddSingleton(mongo);

        if (mongo.Enabled)
        {
            builder.Services.AddSingleton<IAuditLogService, MongoAuditService>();
            builder.Services.AddSingleton<IMediaSelectionLogService, MongoMediaSelectionLogService>();
            builder.Services.AddScoped<IListingAuditLogService, ListingAuditLogService>();
        }
        else
        {
            builder.Services.AddSingleton<IAuditLogService, NoopAuditService>();
            builder.Services.AddSingleton<IMediaSelectionLogService, NoopMediaSelectionLogService>();
            builder.Services.AddScoped<IListingAuditLogService, NoopListingAuditLogService>();
        }
        builder.Services.AddSingleton(mongo);

        // Identity
        builder.Services.AddIdentityCore<ApplicationUser>(opt =>
            {
                opt.User.RequireUniqueEmail = true;
                opt.Lockout.MaxFailedAccessAttempts = 5;
                opt.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<RecamDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        builder.Services.AddHttpContextAccessor();

        // JWT
        builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));
        builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();
        var jwt = builder.Configuration.GetSection("Jwt").Get<JwtSettings>()!;
        builder.Services.AddAuthentication("Bearer")
            .AddJwtBearer("Bearer", o =>
            {
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(2)
                };
            });
        builder.Services.AddAuthorization();

        // AutoMapper / Email
        builder.Services.AddAutoMapper(typeof(Program));
        builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("Email"));
        builder.Services.AddSingleton<IEmailSender, MailKitEmailSender>();

        // Swagger（只注册一次，加入常用修复项）
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new() { Title = "Recam API", Version = "v1" });

            // 避免不同命名空间同名类导致 schema 冲突 → 生成 swagger.json 时 500
            c.CustomSchemaIds(t => t.FullName);

            // Bearer 安全定义
            c.AddSecurityDefinition("Bearer", new()
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Typing: Bearer {token}"
            });
            c.AddSecurityRequirement(new()
            {
                {
                    new() { Reference = new() { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
                    Array.Empty<string>()
                }
            });

            //（可选）XML 注释：只有在文件存在时才包含，避免 FileNotFound 造成 500
            var xml = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xml);
            if (File.Exists(xmlPath))
                c.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
        });
        Console.WriteLine("EF Using Connection: " + builder.Configuration.GetConnectionString("RecamDb"));
        var app = builder.Build();

        // 启动时的数据种子：建议 try/catch，避免在生产启动阶段把站点拉挂
        try
        {
            using var scope = app.Services.CreateScope();
            await IdentitySeeder.SeedAsync(scope.ServiceProvider);
            await DataSeeder.SeedAsync(scope.ServiceProvider);
        }
        catch (Exception ex)
        {
            
            Console.WriteLine($"Seeding failed: {ex}");
        }

        app.UseMiddleware<ExceptionMiddleware>();

        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "Recam API v1");
            c.RoutePrefix = "swagger";
        });

        app.UseHttpsRedirection();
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();

        
        app.MapGet("/", () => Results.Redirect("/swagger"));

        app.Run();
    }
}
