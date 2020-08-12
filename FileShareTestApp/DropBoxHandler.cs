using Dropbox.Api;
using Dropbox.Api.Files;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Configuration;
using System.Text;

namespace FileShareTestApp
{
    public class DropBoxHandler
    {
        private static string AccessToken = ConfigurationManager.AppSettings["DropBoxAccessToken"];

        public static async Task<IList<Metadata>> GetFolderData(string folderName)
        {
            using (var dbx = new DropboxClient(AccessToken))
            {
                var result = await dbx.Files.ListFolderAsync(folderName);
                return result.Entries.ToList();
            }
        }

        public static byte[] Download(string folder, string file)
        {
            using (var dbx = new DropboxClient(AccessToken))
            {
                var result = dbx.Files.DownloadAsync(folder + "/" + file).Result;

                var fileContent = result.GetContentAsByteArrayAsync().Result;

                return fileContent;
            }
        }

        public static void Upload(string folder, string file, string content)
        {
            using (var dbx = new DropboxClient(AccessToken))
            {
                using (var mem = new MemoryStream(Encoding.UTF8.GetBytes(content)))
                {
                    var updated = dbx.Files.UploadAsync(
                        folder + "/" + file,
                        WriteMode.Overwrite.Instance,
                        body: mem).Result;
                }
            }
        }
    }
}