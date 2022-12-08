using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;

[assembly: FunctionsStartup(typeof(SpeechToTextSample.Function.Startup))]

namespace SpeechToTextSample.Function
{
    class Startup : FunctionsStartup
    {
        public IConfiguration Configuration { get; }

        public Startup()
        {
            var config = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .AddJsonFile("local.settings.json", true);
            Configuration = config.Build();
        }

        public override void Configure(IFunctionsHostBuilder builder)
        {
            builder.Services.AddSingleton(provider =>
            {
                var options = new DefaultAzureCredentialOptions
                {
                    ManagedIdentityClientId = Configuration["MANAGED_IDENTITY_CLIENT_ID"]
                };
                var credential = new DefaultAzureCredential(options);
                var storageAccountName = Configuration["STORAGE_ACCOUNT_NAME"];
                var serviceUri = new Uri($"https://{storageAccountName}.blob.core.windows.net");
                var blobServiceClient = new BlobServiceClient(serviceUri, credential);
                return blobServiceClient;
            });

            builder.Services.AddSingleton(provider =>
            {
                var configuration = new FunctionConfiguration(Configuration);
                return configuration;
            });

            builder.Services.AddHttpClient();
        }
    }
}
