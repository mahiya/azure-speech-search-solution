using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using SpeechToTextSample.Function;
using System;
using System.Threading.Tasks;

namespace DurableFunctionSample
{
    public class WebApiFunctions
    {
        readonly BlobServiceClient _blobServiceClient;
        readonly string _blobContainerName;

        public WebApiFunctions(
            FunctionConfiguration config,
            BlobServiceClient blobServiceClient)
        {
            _blobServiceClient = blobServiceClient;
            _blobContainerName = config.StorageContainerName;
        }

        [FunctionName(nameof(PublishUploadUrl))]
        public async Task<IActionResult> PublishUploadUrl(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "uploadurl")] HttpRequest req,
            ILogger logger)
        {
            // クエリストリングからBlobへアップロードするファイル名を取得する
            const string queryKey = "name";
            if (!req.Query.ContainsKey(queryKey))
                return new BadRequestResult();
            var blobName = req.Query[queryKey];

            // オーディオファイルをアップロードするための SAS を発行して、アップロード用のURLを生成する
            var urlWithSas = await GetUrlWithSasAsync(blobName, BlobSasPermissions.Write, DateTime.UtcNow.AddMinutes(3));
            return new OkObjectResult(urlWithSas);
        }

        [FunctionName(nameof(PublishDownloadUrl))]
        public async Task<IActionResult> PublishDownloadUrl(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "downloadurl")] HttpRequest req,
            ILogger logger)
        {
            // クエリストリングからダウンロードするBlob名を取得する
            const string queryKey = "name";
            if (!req.Query.ContainsKey(queryKey))
                return new BadRequestResult();
            var blobName = req.Query[queryKey];

            // オーディオファイルをダウンロードするための SAS を発行して、ダウンロード用のURLを生成する
            var urlWithSas = await GetUrlWithSasAsync(blobName, BlobSasPermissions.Read, DateTime.UtcNow.AddMinutes(10));
            return new OkObjectResult(urlWithSas);
        }

        private async Task<string> GetUrlWithSasAsync(string blobName, BlobSasPermissions permission, DateTime expiresOn)
        {
            var delegationKey = (await _blobServiceClient.GetUserDelegationKeyAsync(DateTime.UtcNow, expiresOn)).Value;
            var builder = new BlobSasBuilder
            {
                BlobContainerName = _blobContainerName,
                BlobName = blobName,
                Resource = "b",
                StartsOn = DateTime.UtcNow,
                ExpiresOn = expiresOn,
            };
            builder.SetPermissions(permission);
            var sasToken = builder.ToSasQueryParameters(delegationKey, _blobServiceClient.AccountName);
            var urlWithSas = $"{_blobServiceClient.Uri}{_blobContainerName}/{blobName}?{sasToken}";
            return urlWithSas;
        }
    }
}
