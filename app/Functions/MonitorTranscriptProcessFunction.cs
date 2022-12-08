using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SpeechToTextSample.Function;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace DurableFunctionSample
{
    /// <summary>
    /// Speech Service API での文字起こし処理が完了するまで待機するための関数
    /// </summary>
    public class MonitorTranscriptProcessFunction
    {
        readonly HttpClient _httpClient;

        public MonitorTranscriptProcessFunction(
            FunctionConfiguration config,
            IHttpClientFactory httpClientFactory)
        {
            _httpClient = httpClientFactory.CreateClient();
            _httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", config.CognitiveServiceApiKey);
        }

        [FunctionName(nameof(MonitorTranscriptionProcess))]
        public async Task MonitorTranscriptionProcess(
            [OrchestrationTrigger] IDurableOrchestrationContext context,
            ILogger logger)
        {
            var input = context.GetInput<FunctionInput>();
            logger.LogInformation($"input is {JsonConvert.SerializeObject(input)}");

            // チェック間隔と最大チェック時間を定義する
            const int pollingIntervalSec = 15; // 15秒
            const int maximamMonitoringSec = 60 * 30; // 30分

            var expiryTime = context.CurrentUtcDateTime.AddSeconds(maximamMonitoringSec);
            while (context.CurrentUtcDateTime < expiryTime)
            {
                // Speech Services の API を呼び出して、文字起こし処理が完了しているかを確認する
                var resp = _httpClient.GetAsync(input.TranscriptFilesUrl).Result;
                var json = resp.Content.ReadAsStringAsync().Result;
                var transcriptionDetails = JsonConvert.DeserializeObject<GetTranscriptionResponse>(json).values;
                if (transcriptionDetails.Any())
                {
                    var transcriptionDetail = transcriptionDetails.First(t => t.kind == "Transcription");
                    input.TranscriptOutputUrl = transcriptionDetail.links.contentUrl;
                    await context.CallActivityAsync(nameof(IndexTranscriptionFunction.IndexTranscriptionResult), input);
                    break;
                }

                // Speech Services の文字起こし処理が完了していない場合、一定時間待機後に再度チェック処理を行う
                var nextCheck = context.CurrentUtcDateTime.AddSeconds(pollingIntervalSec);
                await context.CreateTimer(nextCheck, CancellationToken.None);
            }
            logger.LogInformation("Monitor expired.");
        }

        class GetTranscriptionResponse
        {
            public Value[] values { get; set; }

            public class Value
            {
                public string kind { get; set; }
                public Links links { get; set; }
            }

            public class Links
            {
                public string contentUrl { get; set; }
            }
        }
    }
}
