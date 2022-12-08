using Microsoft.Extensions.Configuration;

namespace SpeechToTextSample.Function
{
    public class FunctionConfiguration
    {
        public readonly string StorageAccountName;
        public readonly string StorageContainerName;
        public readonly string CognitiveServiceApiKey;
        public readonly string CognitiveServiceLocation;
        public readonly string CognitiveSearchName;
        public readonly string CognitiveSearchIndexName;
        public readonly string CognitiveSearchApiKey;

        public FunctionConfiguration(IConfiguration config)
        {
            StorageAccountName = config["STORAGE_ACCOUNT_NAME"];
            StorageContainerName = config["STORAGE_CONTAINER_NAME"];
            CognitiveServiceLocation = config["COGNITIVE_SERVICE_LOCATION"];
            CognitiveServiceApiKey = config["COGNITIVE_SERVICE_API_KEY"];
            CognitiveSearchName = config["COGNITIVE_SEARCH_NAME"];
            CognitiveSearchIndexName = config["COGNITIVE_SEARCH_INDEX_NAME"];
            CognitiveSearchApiKey = config["COGNITIVE_SEARCH_API_KEY"];
        }
    }
}
