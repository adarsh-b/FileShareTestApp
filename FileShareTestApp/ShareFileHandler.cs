using ShareFile.Api.Client;
using ShareFile.Api.Client.Extensions;
using ShareFile.Api.Client.Logging;
using ShareFile.Api.Client.Models;
using ShareFile.Api.Client.Security.Authentication.OAuth2;
using ShareFile.Api.Client.Transfers;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;

namespace FileShareTestApp
{
    public class ShareFileHandler
    {
        private static string ControlPlane = ConfigurationManager.AppSettings["ShareFileControlPlane"];
        private static string UserName = ConfigurationManager.AppSettings["ShareFileUserName"];
        private static string Password = ConfigurationManager.AppSettings["ShareFilePassword"];
        private static string Subdomain = ConfigurationManager.AppSettings["ShareFileSubdomain"];
        private static string ClientID = ConfigurationManager.AppSettings["ShareFileClientID"];
        private static string ClientSecret = ConfigurationManager.AppSettings["ShareFileClientSecret"];
        private static string ShareFileBaseAPIUrl = ConfigurationManager.AppSettings["ShareFileShareFileBaseAPIUrl"];
        private static ShareFileClient sfClient;

        public static async Task Initialize()
        {
            ServicePointManager.Expect100Continue = true;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls
               | SecurityProtocolType.Tls11
               | SecurityProtocolType.Tls12
               | SecurityProtocolType.Ssl3;

            var user = new SampleUser
            {
                ControlPlane = ControlPlane,
                Username = UserName,
                Password = Password,
                Subdomain = Subdomain
            };

            // Authenticate with username/password
            sfClient = await PasswordAuthentication(user, ClientID, ClientSecret); ;

            // Create a Session
            var session = await sfClient.Sessions.Login().Expand("Principal").ExecuteAsync();
        }

        private static async Task<ShareFileClient> PasswordAuthentication(SampleUser user, string clientId, string clientSecret)
        {
            // Initialize ShareFileClient.
            var configuration = Configuration.Default();
            configuration.Logger = new DefaultLoggingProvider();

            var sfClient = new ShareFileClient(ShareFileBaseAPIUrl, configuration);
            var oauthService = new OAuthService(sfClient, clientId, clientSecret);

            // Perform a password grant request.  Will give us an OAuthToken
            var oauthToken = await oauthService.PasswordGrantAsync(user.Username, user.Password, user.Subdomain, user.ControlPlane);

            // Add credentials and update sfClient with new BaseUri
            sfClient.AddOAuthCredentials(oauthToken);
            sfClient.BaseUri = oauthToken.GetUri();

            return sfClient;
        }

        public static async Task RunSample()
        {
            // Initialize ShareFile Client
            await Initialize();

            // Load Folder and Contents
            var defaultUserFolder = await LoadFolderAndChildren();
            Console.WriteLine("Loaded - " + defaultUserFolder.Name);

            // Create a Folder
            var createdFolder = await CreateFolder(defaultUserFolder, "Sample Folder");
            Console.WriteLine("Created a new folder - " + createdFolder.Name);

            // Upload a file
            var uploadedFileId = await Upload( "SampleFileUpload.txt", createdFolder);
            var itemUri = sfClient.Items.GetAlias(uploadedFileId);
            var uploadedFile = await sfClient.Items.Get(itemUri).ExecuteAsync();
            Console.WriteLine("Uploaded - " + uploadedFile.Name);

            // Download a file
            await Download(uploadedFile, "DownloadedFiles");
            Console.WriteLine("Downloaded - " + uploadedFile.Name);

            // Share a file using a Link
            var share = await ShareViaLink(uploadedFile);
            Console.WriteLine("Successfully created a share, it be be accessed using: " + share.Uri);

            // Share a file via ShareFile
            string recipientEmailAddress = "adarsh.b@codearray.tech";
            await ShareViaShareFileEmail(uploadedFile, recipientEmailAddress, "Test Share File Email", 10);

            Console.ReadKey();
        }

        
        public static async Task<Folder> CreateFolder(Folder parentFolder, string folderName, string description = "")
        {
            // Create instance of the new folder we want to create.  Only a few properties 
            // on folder can be defined, others will be ignored.
            var newFolder = new Folder
            {
                Name = folderName,
                Description = description
            };

            return await sfClient.Items.CreateFolder(parentFolder.url, newFolder, overwrite: true).ExecuteAsync();
        }

        public static async Task<string> Upload(string sourceFilePath, Folder destinationFolder)
        {
            var file = System.IO.File.Open(sourceFilePath, FileMode.Open);
            var uploadRequest = new UploadSpecificationRequest
            {
                FileName = file.Name,
                FileSize = file.Length,
                //Details = "Sample details",
                Parent = destinationFolder.url
            };

            var uploader = sfClient.GetAsyncFileUploader(uploadRequest, file);

            var uploadResponse = await uploader.UploadAsync();

            return uploadResponse.First().Id;
        }

        public static async Task Download(Item itemToDownload, string destinationDownloadFolder)
        {
            var downloadDirectory = new DirectoryInfo(destinationDownloadFolder);
            if (!downloadDirectory.Exists)
            {
                downloadDirectory.Create();
            }

            var downloader = sfClient.GetAsyncFileDownloader(itemToDownload);
            var file = System.IO.File.Open(Path.Combine(downloadDirectory.Name, itemToDownload.Name), FileMode.Create);

            await downloader.DownloadToAsync(file);
        }

        

        public static async Task<Folder> LoadFolderAndChildren()
        {
            var folder = (Folder)await sfClient.Items.Get().Expand("Children").ExecuteAsync();

            return folder;
        }

        public static async Task<Share> ShareViaLink(Item fileToShare)
        {
            var share = new Share
            {
                Items = new List<Item>
                {
                    fileToShare
                }
            };

            return await sfClient.Shares.Create(share).ExecuteAsync();
        }

        public static async Task ShareViaShareFileEmail(Item fileToShare, string recipientEmailAddress, string emailSubject, int expirationDays)
        {
            var sendShare = new ShareSendParams
            {
                Emails = new[] { recipientEmailAddress },
                Items = new[] { fileToShare.Id },
                Subject = emailSubject,
                MaxDownloads = -1, // Allow unlimited downloads
                ExpirationDays = expirationDays // Expires in 10 days
            };

            await sfClient.Shares.CreateSend(sendShare).ExecuteAsync();

        }
    }

    public struct SampleUser
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public string Subdomain { get; set; }
        public string ControlPlane { get; set; }
    }
}