using Azure;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SpeechToTextSample.Function;
using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace DurableFunctionSample
{
    /// <summary>
    /// Speech Service API での文字起こし結果を Cognitive Search へ登録する関数
    /// </summary>
    public class IndexTranscriptionFunction
    {
        readonly HttpClient _httpClient;
        readonly SearchIndexClient _indexClient;
        readonly string _indexName;

        public IndexTranscriptionFunction(
            FunctionConfiguration config,
            IHttpClientFactory httpClientFactory)
        {
            _httpClient = httpClientFactory.CreateClient();

            var endpoint = new Uri($"https://{config.CognitiveSearchName}.search.windows.net");
            var credentials = new AzureKeyCredential(config.CognitiveSearchApiKey);
            _indexClient = new SearchIndexClient(endpoint, credentials);
            _indexName = config.CognitiveSearchIndexName;
        }

        [FunctionName(nameof(IndexTranscriptionResult))]
        public async Task IndexTranscriptionResult([ActivityTrigger] FunctionInput input, ILogger logger)
        {
            logger.LogInformation($"input is {JsonConvert.SerializeObject(input)}");

            // Speech Service の文字起こし結果をダウンロードして、結果としてまとめる
            var json = await _httpClient.GetStringAsync(input.TranscriptOutputUrl);
            var transcription = JsonConvert.DeserializeObject<TranscriptionResult>(json);
            var phrases = transcription.recognizedPhrases.Select(p => new Phrase
            {
                account = input.StorageAccountName,
                container = input.ContainerName,
                blob = input.BlobName,
                phrase = p.nBest.First().display,
                offset = p.offsetInTicks / 10000000,
            }).ToList();

            // Cognitive Search のインデックスに文字起こし結果を登録する
            var chunks = phrases.Chunk(1000);
            foreach (var (chunk, i) in chunks.Select((c, i) => (c, i)))
            {
                logger.LogInformation($"Registering documents to Cognitive Search ({i + 1}/{chunks.Count()})");
                var batch = IndexDocumentsBatch.Create(chunk.Select(phrase => IndexDocumentsAction.Upload(phrase)).ToArray());
                var searchClient = _indexClient.GetSearchClient(_indexName);
                await searchClient.IndexDocumentsAsync(batch);
            }
        }

        class TranscriptionResult
        {
            public RecognizedPhrase[] recognizedPhrases { get; set; }

            public class RecognizedPhrase
            {
                public long offsetInTicks { get; set; }
                public Nbest[] nBest { get; set; }
            }

            public class Nbest
            {
                public string display { get; set; }
            }
        }
    }
}
