using CloudFlareUtilities;
using HtmlAgilityPack;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Sketchfab.Extensions;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Sketchfab
{
    public class SketchfabClient
    {
        public class Arguments
        {
            public string url { get; set; } = "";
            public string source { get; set; } = "";
            public bool help { get; set; } = false;
            public bool textures { get; set; } = false;
            public bool export { get; set; } = false;
            public bool clear { get; set; } = false;
            public bool background { get; set; } = false;
        }

        private CookieContainer CookieContainer;
        public HttpClient client { get; private set; }

        public SketchfabClient()
        {
            CookieContainer = new CookieContainer();
            client = CreateHttpClient();
        }

        private class infoProject
        {
            [Description("Uid")]
            public string uidModel { get; set; }
            [Description("Name")]
            public string nameModel { get; set; }
            [Description("Url")]
            public string urlModel { get; set; }

            [Description("Username")]
            public string username { get; set; }
            [Description("Display Name")]
            public string displayName { get; set; }
            [Description("Profile Url")]
            public string profileUrl { get; set; }

            [Description("Uid Files")]
            public string uidFiles { get; set; }
            [Description("Url")]
            public string urlFiles { get; set; }
            public List<string> modelFileNames { get; set; }

            [Description("Textures")]
            public List<Texture> textures { get; set; }
        }
        private class Texture
        {
            public string uidTexture { get; set; }
            [Description("Original Filename")]
            public string filename { get; set; }
            [Description("Filename")]
            public string remoteFilename { get; set; }

            public string uidItemTexture { get; set; }
            [Description("Url")]
            public string urlItemTexture { get; set; }
            public string width { get; set; }
            public string height { get; set; }
        }
        private class ContentType
        {
            public const string Gzip = "application/gzip";
            public const string Jpeg = "image/jpeg";
            public const string Png = "image/png";
            public const string Gif = "image/gif";
            public const string None = "";
        }

        public void Download(string url, Arguments arguments)
        {
            var document = GetHtmlDocument(url);
            var data = GetJsonItemModel(document);

            var infoData = new infoProject();
            var outputInfo = new StringBuilder();

            Console.WriteLine(GenerateHeader("Getting info model.", true));
            infoData.uidModel = GetIdModelFromHtml(document);
            
            // root nodes
            var rootModel = $"/i/models/{infoData.uidModel}";
            var rootTextures = $"/i/models/{infoData.uidModel}/textures?optimized=1";

            // Files Info
            infoData.uidFiles = data[rootModel]["files"][0]["uid"].ToString();
            infoData.urlFiles = $"https://media.sketchfab.com/urls/{infoData.uidModel}/dist/models/{infoData.uidFiles}/";

            // Model Info
            infoData.nameModel = data[rootModel]["name"].ToString();
            infoData.urlModel = data[rootModel]["viewerUrl"].ToString();
            
            // User Info
            infoData.username = data[rootModel]["user"]["username"].ToString();
            infoData.displayName = data[rootModel]["user"]["displayName"].ToString();
            infoData.profileUrl = data[rootModel]["user"]["profileUrl"].ToString();

            Console.WriteLine(" Model Id: " + infoData.uidModel);
            Console.WriteLine(" Name: " + infoData.nameModel);
            Console.WriteLine(" Author: " + infoData.displayName);
            Console.WriteLine(" Profile Url: " + infoData.profileUrl);

            var dlPath = Path.Combine(Directory.GetCurrentDirectory(), "downloads");
            var dlPathFiles = Path.Combine(dlPath, MakeDirectoryValid($"{infoData.nameModel} ({infoData.uidModel})"));
            var dlPathTextures = Path.Combine(dlPathFiles, "textures");

            if (!Directory.Exists(dlPathFiles))
                Directory.CreateDirectory(dlPathFiles);

            Console.WriteLine(GenerateHeader("Downloading model files."));
            infoData.modelFileNames = new List<string>();
            foreach (string filename in new string[] { "file.osgjs.gz", "model_file.bin.gz", "model_file_wireframe.bin.gz" })
            {
                var urlfile = infoData.urlFiles + filename;
                if (!RemoteFileExists(urlfile))
                    continue;

                infoData.modelFileNames.Add(filename);

                var outFile = Path.Combine(dlPathFiles, filename);
                Console.Write(" " + filename);
                WriteFile(urlfile, outFile);
                Console.WriteLine(" - " + "Download Completed.");
            }

            // Download textures
            if (arguments.textures)
            {
                // Textures
                infoData.textures = new List<Texture>();
                var textures = data[rootTextures]["results"];

                // Make the sub directory for texture files
                if (textures.Count() > 0 && !Directory.Exists(dlPathTextures))
                    Directory.CreateDirectory(dlPathTextures);

                Console.WriteLine(GenerateHeader("Downloading texture files."));
                foreach (var item in textures)
                {
                    var infoTexture = new Texture();
                    infoTexture.uidTexture = item["uid"].ToString();
                    infoTexture.filename = item["name"].ToString();

                    // Images Files
                    var images = item["images"]
                                 .OrderByDescending(x => x["width"])
                                 .ThenByDescending(x => (DateTimeOffset)x["updatedAt"]).First();

                    infoTexture.uidItemTexture = images["uid"].ToString();
                    infoTexture.width = images["width"].ToString();
                    infoTexture.height = images["height"].ToString();
                    infoTexture.urlItemTexture = images["url"].ToString();

                    // Add the texture data
                    infoData.textures.Add(infoTexture);

                    infoTexture.remoteFilename = infoTexture.urlItemTexture.Split('/').Last();
                    //var outFile = Path.Combine(dlPathTextures, infoTexture.filename);
                    var outFile = Path.Combine(dlPathTextures, infoTexture.remoteFilename);

                    Console.WriteLine(" Original Name: " + infoTexture.filename);
                    Console.WriteLine(" Name: " + infoTexture.remoteFilename);
                    Console.WriteLine(" Dimensions: " + infoTexture.width + "x" + infoTexture.height);

                    WriteFile(infoTexture.urlItemTexture, outFile);
                    Console.WriteLine(" Download completed.");
                    Console.WriteLine("");
                }
            }

            // Create the file with info of model
            CreateInfoFile(infoData, dlPathFiles, arguments.textures);

            // Blender exporting
            if (arguments.export)
            {
                Console.WriteLine(GenerateHeader("Exporting model file by blender."));

                var path = dlPathFiles;
                var filename = MakeDirectoryValid($"{infoData.nameModel}").Replace(' ', '_').ToLower() + ".obj";
                
                var p = new Process();
                p.StartInfo.WorkingDirectory = ".\\blender";
                p.StartInfo.FileName = "blender.exe";
                p.StartInfo.Arguments = $"-b -P .blender\\scripts\\sketchfab-decode.py -- -s \"{path}\\file.osgjs\" -o \"{path}\\{filename}\"";

                p.StartInfo.CreateNoWindow = false;
                p.StartInfo.UseShellExecute = true;

                if (arguments.background)
                    p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;

                Console.Write(" " + filename);

                p.Start();
                p.WaitForExit();
                Thread.Sleep(2500);

                switch (p.ExitCode)
                {
                    case 0:
                        Console.WriteLine(" - " + "Export completed.");
                        if (arguments.clear)
                            foreach (var file in new string[] { "file.osgjs", "file.osgjs.ys", "model_file.bin", "model_file_wireframe.bin" })
                                File.Delete(Path.Combine(path, file));
                        break;
                    case -1:
                        Console.WriteLine(" - " + "Export failed.");
                        break;
                    default:
                        Console.WriteLine(" - " + "Process failed.");
                        break;
                }                    
            }

            Console.WriteLine("");
        }

        /// <summary>
        /// Generate the output header string.
        /// </summary>
        private string GenerateHeader(string header, bool first = false)
        {
            var output = new StringBuilder();
            if (!first) output.AppendLine();
            output.AppendLine("----------------------------------------------------");
            output.AppendLine(" " + header);
            output.AppendLine("----------------------------------------------------");

            return output.ToString();
        }

        /// <summary>
        /// Creates the output info model.
        /// </summary>
        private void CreateInfoFile(infoProject model, string path, bool dltextures = false)
        {
            var output = new StringBuilder();
            output.AppendLine(GenerateHeader("Model Details", true));
            output.AppendLine($"  {GetPropertyData(() => model.uidModel).Description}: {model.uidModel}");
            output.AppendLine($"  {GetPropertyData(() => model.nameModel).Description}: { model.nameModel}");
            output.AppendLine($"  {GetPropertyData(() => model.urlModel).Description}: {model.urlModel}");

            output.AppendLine(GenerateHeader("User Profile"));
            output.AppendLine($"  {GetPropertyData(() => model.displayName).Description}: {model.displayName}");
            output.AppendLine($"  {GetPropertyData(() => model.profileUrl).Description}: {model.profileUrl}");

            output.AppendLine(GenerateHeader("Model Files"));
            foreach(var file in model.modelFileNames)
                output.AppendLine($"{model.urlFiles}" + file);

            if (dltextures && model.textures.Count > 0)
            {
                output.AppendLine(GenerateHeader("Textures"));
                foreach (var texture in model.textures)
                {
                    output.AppendLine($"  {GetPropertyData(() => texture.filename).Description}: {texture.filename}");
                    output.AppendLine($"  {GetPropertyData(() => texture.remoteFilename).Description}: {texture.remoteFilename}");
                    output.AppendLine($"  Dimensions: {texture.width}x{texture.height}");
                    output.AppendLine($"  {texture.urlItemTexture}");
                    output.AppendLine();
                }
            }

            File.WriteAllText(Path.Combine(path, "infoModel.txt"), output.ToString());
        }

        private class PropertyData
        {
            public string Name;
            public string Value;
            public string Description;
        }
        /// <summary>
        /// Get the name of a static or instance property from a property access lambda.
        /// </summary>
        private PropertyData GetPropertyData<T>(Expression<Func<T>> expression)
        {
            var m = expression.Body as MemberExpression;           
            if (m != null && m.Member is PropertyInfo)
            {
                var description = ((DescriptionAttribute)m.Member.GetCustomAttributes(typeof(DescriptionAttribute), true).FirstOrDefault());

                return new PropertyData()
                {
                    Name = m.Member.Name,
                    Description = description != null ? description.Description : "",
                    Value = Expression.Lambda(m).Compile().DynamicInvoke().ToString()
                };
            }
            throw new ArgumentException("Expression is not a Property", "expression");   
        }

        /// <summary>
        /// Downloads the file by url with correct content type
        /// </summary>
        private Stream DownloadFile(string url, string type)
        {
            HttpWebRequest request = WebRequest.Create(url) as HttpWebRequest;
            request.KeepAlive = false;
            request.Method = "GET";
            if (type != ContentType.None)
                request.ContentType = type;

            HttpWebResponse response = request.GetResponse() as HttpWebResponse;
            Stream streamResponse = response.GetResponseStream();

            return streamResponse;
        }

        /// <summary>
        /// Checks if url file exists
        /// </summary>
        private bool RemoteFileExists(string url)
        {
            try
            {
                HttpWebRequest request = WebRequest.Create(url) as HttpWebRequest;
                request.Method = "HEAD";
                HttpWebResponse response = request.GetResponse() as HttpWebResponse;
                response.Close();
                return (response.StatusCode == HttpStatusCode.OK);
            }
            catch
            { return false; }
        }

        /// <summary>
        /// Downloads and Writes the output files
        /// </summary>
        private void WriteFile(string url, string outputFile)
        {
            var type = GetContentTypeFromFilename(url.Split('/').Last());
            if (type == ContentType.Gzip)
                outputFile = outputFile.Substring(0, outputFile.LastIndexOf("."));

            using (Stream stream = DownloadFile(url, type))
            using (FileStream fs = new FileStream(outputFile, FileMode.Create, FileAccess.Write))
            {
                if (type == ContentType.Gzip)
                    using (GZipStream decompressionStream = new GZipStream(stream, CompressionMode.Decompress))
                        decompressionStream.CopyTo(fs);
                else
                    stream.CopyTo(fs);
            }
        }

        /// <summary>
        /// Returns the correct content type from file extension for HttpRequest
        /// </summary>
        private string GetContentTypeFromFilename(string filename)
        {
            var extension = Path.GetExtension(filename).ToString().ToLower();
            switch (extension)
            {
                case ".gz":
                case ".gzip":
                    return ContentType.Gzip;
                case ".jpg":
                case ".jpeg":
                    return ContentType.Jpeg;
                case ".png":
                    return ContentType.Jpeg;
                case ".gif":
                    return ContentType.Jpeg;
                default:
                    return ContentType.None;
            }
        }

        /// <summary>
        /// Returns the file or directory name in valid format
        /// </summary>
        private string MakeDirectoryValid(string name)
        {
            foreach (char charater in Path.GetInvalidFileNameChars())
                name = name.Replace(charater.ToString(), string.Empty);

            return name;
        }

        /// <summary>
        /// Returns the json document of the model
        /// </summary>
        private JObject GetJsonItemModel(HtmlDocument document)
        {
            var content = document.GetElementbyId("js-dom-data-prefetched-data").InnerHtml;
            var output = WebUtility.HtmlDecode(content)
                                   .Replace("<![CDATA[", "").Replace("]]>", "")
                                   .Replace("<!--", "").Replace("-->", "").Trim();

            return JObject.Parse(output);
        }

        /// <summary>
        /// Returns id of the model
        /// </summary>
        private string GetIdModelFromHtml(HtmlDocument document)
        {
            return document.DocumentNode.SelectSingleNode("//*[@data-modeluid]").GetAttributeValue("data-modeluid", "");
        }


        /// <summary>
        /// Sends a get request to the Url provided using this session cookies and returns the HtmlDocument of the result.
        /// </summary>
        private HtmlDocument GetHtmlDocument(string url)
        {
            var response = client.GetRequest(url);
            if (response.StatusCode != HttpStatusCode.OK)
                return null;

            var document = new HtmlDocument();
            document.LoadHtml(client.GetRequest(url).Content);
            return document;
        }

        /// <summary>
        /// Returns an web client that have this session cookies.
        /// </summary>
        private HttpClient CreateHttpClient()
        {
            // Handler for bypass Cloudflare
            var handler = new ClearanceHandler()
            {
                InnerHandler = new HttpClientHandler()
                {
                    CookieContainer = CookieContainer,
                    AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
                },

                MaxRetries = 10,
                ClearanceDelay = 3000
            };

            var client = new HttpClient(handler);
            //var client = new HttpClient(new HttpClientHandler() { CookieContainer = CookieContainer });

            client.DefaultRequestHeaders.Add("Accept", "*/*");
            client.DefaultRequestHeaders.Add("Accept-Language", "it-IT,it;q=0.8,en-US;q=0.5,en;q=0.3");
            client.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:66.0) Gecko/20100101 Firefox/66.0");
            client.DefaultRequestHeaders.Add("Origin", "https://sketchfab.com");
            client.DefaultRequestHeaders.Add("Connection", "keep-alive");
            client.DefaultRequestHeaders.Add("Cache-Control", "max-age=0");

            return client;
        }

    }
}
