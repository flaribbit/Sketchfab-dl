using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NDesk.Options;
using Sketchfab;
using static Sketchfab.SketchfabClient;

namespace Sketchfab_Downloader
{
    class Program
    {
        
        static void Main(string[] args)
        {
            // Application Arguments
            var arguments = new Arguments();
            var p = new OptionSet() {
                { "u|url=",
                    "The {URL} input.",
                  (string v) => arguments.url = v },
                { "s|source=",
                    "The source {NAME} file.",
                  (string v) => arguments.source = v },
                { "t|textures",
                    "Download texture files.",
                  v => arguments.textures = v != null },
                { "e|export",
                    "Export the file to .obj (blender is required).",
                  v => arguments.export = v != null },
                { "c|clear",
                    "Clear the workplace (export option is required).",
                  v => arguments.clear = v != null },
                { "b|background",
                    "call the processing export in background (export option is required).",
                  v => arguments.background = v != null },
                { "h|help",  "Show this message and exit",
                  v => arguments.help = v != null },
            };

            try
            { var extra = p.Parse(args); }
            catch (OptionException e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine("Try `Sketchfab-dl --help' for more information.");
                return;
            }

            if (arguments.help)
            { ShowHelp(p); return; }

            // Check if blender exists
            if (!File.Exists(Path.Combine(Directory.GetCurrentDirectory() + "\\blender\\", "blender.exe")))
                arguments.export = false;          
            
            // List of all model 
            var listOfUrls = new List<string>();
  
            // Parse url
            if (arguments.url.Length > 0 && CheckUrlIsValid(arguments.url))
                listOfUrls.Add(arguments.url);

            // Parse source file
            if (arguments.source.Length > 0 && File.Exists(arguments.source))
            {
                foreach (string item in File.ReadAllText(arguments.source).Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (CheckUrlIsValid(item))
                        listOfUrls.Add(item);
                }
            }

            if (listOfUrls.Count == 0)
                return;

            var sketchfab = new SketchfabClient();
            foreach (var urlModel in listOfUrls.Distinct().ToList())
                sketchfab.Download(urlModel, arguments);
        }

        private static bool CheckUrlIsValid(string url)
        {
            Uri uriResult;
            return Uri.TryCreate(url, UriKind.Absolute, out uriResult) && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
        }
        private static void ShowHelp(OptionSet p)
        {
            Console.WriteLine("Usage: Sketchfab-dl [OPTIONS]");
            Console.WriteLine();
            Console.WriteLine("Options:");
            p.WriteOptionDescriptions(Console.Out);
        }

    }
}
