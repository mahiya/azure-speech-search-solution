namespace DurableFunctionSample
{
    public class FunctionInput
    {
        public string StorageAccountName { get; set; }
        public string ContainerName { get; set; }
        public string BlobName { get; set; }
        public string TranscriptFilesUrl { get; set; }
        public string TranscriptOutputUrl { get; set; }
    }
}
