using Amazon;
using Amazon.S3;
using BLL.Interfaces.Auth;
using BLL.Interfaces.Chat;
using BLL.Interfaces.Documents;
using BLL.Services.Auth;
using BLL.Services.Chat;
using BLL.Services.Documents;
using DAL.Data;
using DAL.Interfaces.Auth;
using DAL.Interfaces.Chat;
using DAL.Interfaces.Documents;
using DAL.Repositories.Auth;
using DAL.Repositories.Chat;
using DAL.Repositories.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BLL.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBusinessLayer(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<DBContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection"), o => o.UseVector())
                .ConfigureWarnings(w => w.Ignore(RelationalEventId.MultipleCollectionIncludeWarning)));

        services.AddScoped<IAuthRepository, AuthRepository>();
        services.AddScoped<IDocumentRepository, DocumentRepository>();
        services.AddScoped<IUploadJobRepository, UploadJobRepository>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<IS3StorageService, S3StorageService>();
        services.AddScoped<IChapterSegmentationService, GeminiChapterSegmentationService>();
        services.AddScoped<IUploadProcessingService, UploadProcessingService>();
        services.AddScoped<IFileParserService, SimpleFileParserService>();
        services.AddScoped<IEmbeddingService, GeminiEmbeddingService>();
        services.AddHostedService<UploadJobBackgroundService>();

        // Chat services
        services.AddScoped<IChatRepository, ChatRepository>();
        services.AddScoped<IGeminiChatService, GeminiChatService>();
        services.AddScoped<IChatService, ChatService>();

        services.AddSingleton<IAmazonS3>(_ =>
        {
            var accessKey = configuration["AwsS3:AccessKey"];
            var secretKey = configuration["AwsS3:SecretKey"];
            var region = configuration["AwsS3:Region"] ?? "ap-southeast-1";

            var config = new AmazonS3Config
            {
                RegionEndpoint = RegionEndpoint.GetBySystemName(region)
            };

            return new AmazonS3Client(accessKey, secretKey, config);
        });

        return services;
    }
}
