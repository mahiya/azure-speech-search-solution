using Azure.Messaging.EventGrid;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.EventGrid;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SpeechToTextSample.Function;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace DurableFunctionSample
{
    /// <summary>
    /// Azure Storage Blob にアップロードされた音声ファイルを Speech Service API に送信する関数
    /// </summary>
    public class ProcessUploadedDataFunction
    {
        readonly BlobServiceClient _blobServiceClient;
        readonly HttpClient _httpClient;

        public ProcessUploadedDataFunction(
            FunctionConfiguration config,
            BlobServiceClient blobServiceClient,
            IHttpClientFactory httpClientFactory)
        {
            _blobServiceClient = blobServiceClient;

            _httpClient = httpClientFactory.CreateClient();
            _httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", config.CognitiveServiceApiKey);
            _httpClient.BaseAddress = new Uri($"https://{config.CognitiveServiceLocation}.api.cognitive.microsoft.com/speechtotext/v3.0/");
        }

        [FunctionName(nameof(ProcessUploadedData))]
        public async Task ProcessUploadedData(
            ILogger logger,
            [EventGridTrigger] EventGridEvent e,
            [DurableClient] IDurableOrchestrationClient starter)
        {
            // 入力を取得する
            var blobEvent = JsonConvert.DeserializeObject<BlobEvent>(e.Data.ToString());

            // アップロードされた Blob 情報を取得する
            var url = new Uri(blobEvent.url);
            var storageAccountName = url.Host.Replace(".blob.core.windows.net", string.Empty);
            var containerName = url.LocalPath.Split("/")[1];
            var blobName = url.LocalPath.Replace($"/{containerName}/", "");

            // Cognitive Searvice - Speech To Text API が使用するための Blob の SAS + URL を生成する
            var delegationKey = (await _blobServiceClient.GetUserDelegationKeyAsync(DateTime.UtcNow, DateTime.UtcNow.AddMinutes(10))).Value;
            var builder = new BlobSasBuilder
            {
                BlobContainerName = containerName,
                BlobName = blobName,
                Resource = "b",
                StartsOn = DateTime.UtcNow,
                ExpiresOn = DateTime.UtcNow.AddMinutes(10),
            };
            builder.SetPermissions(BlobSasPermissions.Read);
            var sasToken = builder.ToSasQueryParameters(delegationKey, _blobServiceClient.AccountName);
            var urlWithSas = $"{_blobServiceClient.Uri}{containerName}/{blobName}?{sasToken}";

            // Speech To Text API に文字起こし(Transcription)作成リクエストを送る
            var body = JsonConvert.SerializeObject(new
            {
                contentUrls = new[] { urlWithSas },
                locale = "ja-JP",
                displayName = blobName,
                properties = new { wordLevelTimestampsEnabled = true }
            });
            var content = new StringContent(body);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            var resp = await _httpClient.PostAsync("transcriptions", content);

            // リクエスト結果を出力する
            var json = await resp.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<StartTranscriptResponse>(json);

            // リクエストした文字起こしのURLを入力として、後続のモニタージョブを起動する
            var functionInput = new FunctionInput
            {
                StorageAccountName = storageAccountName,
                ContainerName = containerName,
                BlobName = blobName,
                TranscriptFilesUrl = result.links.files,
            };
            await starter.StartNewAsync(nameof(MonitorTranscriptProcessFunction.MonitorTranscriptionProcess), input: functionInput);
        }

        class BlobEvent
        {
            public string url { get; set; }
        }

        class StartTranscriptResponse
        {
            public Links links { get; set; }

            public class Links
            {
                public string files { get; set; }
            }
        }
    }
}