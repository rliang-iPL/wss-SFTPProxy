namespace SftpProxy.Models
{
    public class SftpUploadJsonRequest
    {
        public string RemotePath { get; set; }
        public string Host { get; set; }
        public string User { get; set; }
        public int Port { get; set; }
        public string FileName { get; set; }
        public string FileBase64 { get; set; }  // file content as base64
    }
}