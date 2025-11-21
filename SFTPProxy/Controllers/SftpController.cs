using Microsoft.AspNetCore.Mvc;
using Renci.SshNet;
using Renci.SshNet.Common;
using System.Text;
using SftpProxy.Models; // import the model namespace
using System.IO;
using System.Security.Cryptography;

namespace SftpProxy.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class SftpController : ControllerBase
    {
        private const string EncryptedPemPath = @"C:\Sftp\id_rsa_openssh.dat";
        /// <summary>
        /// Upload a file via JSON request (file content is base64-encoded)
        /// </summary>
        [HttpPost("upload-json")]
        public IActionResult UploadJson([FromBody] SftpUploadJsonRequest request)
        {
            try
            {
                // Load and decrypt the PEM file
                if (!System.IO.File.Exists(EncryptedPemPath))
                    return StatusCode(500, $"Encrypted key file not found: {EncryptedPemPath}");

                byte[] encryptedBytes = System.IO.File.ReadAllBytes(EncryptedPemPath);
                byte[] decryptedBytes = ProtectedData.Unprotect(
                    encryptedBytes,
                    null,
                    DataProtectionScope.LocalMachine // or CurrentUser depending on how you encrypted
                );

                string pem = Encoding.UTF8.GetString(decryptedBytes);

                // Feed PEM into SSH.NET
                using var keyStream = new MemoryStream(Encoding.UTF8.GetBytes(pem));
                var privateKey = new PrivateKeyFile(keyStream);

                // Authentication method
                var auth = new PrivateKeyAuthenticationMethod(request.User, privateKey);
                var connInfo = new Renci.SshNet.ConnectionInfo(request.Host, request.Port, request.User, auth);

                using var sftp = new SftpClient(connInfo);
                sftp.Connect();

                // Determine remote path
                var remotePath = string.IsNullOrWhiteSpace(request.RemotePath) || request.RemotePath == "/"
                    ? "/" + request.FileName
                    : request.RemotePath.TrimEnd('/') + "/" + request.FileName;

                // Ensure the parent directory exists
                string directory = Path.GetDirectoryName(remotePath)?.Replace("\\", "/");
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    CreateRemoteDirectoryIfNotExists(sftp, directory);
                }

                // Decode base64 file content
                var fileBytes = Convert.FromBase64String(request.FileBase64);
                using var stream = new MemoryStream(fileBytes);
                sftp.UploadFile(stream, remotePath);

                sftp.Disconnect();

                return Ok($"Uploaded to {remotePath}");
            }
            catch (SshAuthenticationException authEx)
            {
                return StatusCode(401, "Authentication failed: " + authEx.Message);
            }
            catch (SshConnectionException connEx)
            {
                return StatusCode(500, "SFTP Connection error: " + connEx.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, "SFTP Error: " + ex.Message);
            }
        }

        /// <summary>
        /// Recursively create remote directories if they don't exist
        /// </summary>
        private void CreateRemoteDirectoryIfNotExists(SftpClient sftp, string remoteDirectory)
        {
            string[] folders = remoteDirectory.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            string current = "/";
            foreach (var folder in folders)
            {
                current = current.EndsWith("/") ? current + folder : current + "/" + folder;
                if (!sftp.Exists(current))
                {
                    sftp.CreateDirectory(current);
                }
            }
        }
    }
}
