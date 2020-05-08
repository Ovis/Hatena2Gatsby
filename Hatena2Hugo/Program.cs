using ConsoleAppFramework;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Hatena2Hugo
{
    class Entry
    {
        public string Author             { get; set; }
        public string Title              { get; set; }
        public string BaseName           { get; set; }
        public string Status             { get; set; }
        public string AllowComment       { get; set; }
        public string ConvertBreaks      { get; set; }
        public string Date               { get; set; }
        public string Tags               { get; set; }
        public string Image              { get; set; }
        public List<string> Category     { get; set; } = new List<string>();
        public List<string> Excerpt      { get; set; } = new List<string>();
        public List<string> Comment      { get; set; } = new List<string>();
        public List<string> Body         { get; set; } = new List<string>();
        public List<string> ExtendedBody { get; set; } = new List<string>();
    }

    class Program : ConsoleAppBase
    {
        private static readonly HttpClient httpClient = new HttpClient();

        static async Task Main(string[] args)
        {
            // target T as ConsoleAppBase.
            await Host.CreateDefaultBuilder().RunConsoleAppFrameworkAsync<Program>(args);
        }

        [Command("version", "display version")]
        public void Getversion()
        {
            Console.WriteLine($"GetType().Assembly: { GetType().Assembly.GetName() }");
        }

        // allows void/Task return type, parameter is automatically binded from string[] args.
        public async Task RunAsync(
            [Option("s", "Source file")] string src,
            [Option("d", "Destination folder")] string dest,
            [Option("f", "Download image iles from fotolife")] string fotolife = "true",
            [Option("t", "Default title")] string defaultTitle = "No title")
        {
            var running = true;

            Console.CancelKeyPress += (sender, args) =>
            {
                running = false;
            };

            if (!File.Exists(src))
            {
                Console.WriteLine($"{src} is not found");
                return;
            }

            Entry entry = null;
            List<Entry> entries = new List<Entry>();

            bool multi_line = false;
            string key = string.Empty;
            string value = string.Empty;

            Console.WriteLine(
                $"start parsing: {src} ( cancel for Ctrl+C ) \n" +
                $"fotolife: { fotolife }\n" +
                $"default title: { defaultTitle }\n"
            );

            foreach (var line in File.ReadLines(src))
            {
                try
                {
                    if (!running) break;

                    if (entry == null) entry = new Entry();

                    if (line == "--------") // エントリーの終了
                    {
                        if (string.IsNullOrWhiteSpace(entry.BaseName))
                        {
                            throw new Exception(); /* なんかおかしい */
                        }

                        Console.WriteLine($"{entry.Title} - {entry.Date}");

                        entries.Add(entry);
                        entry = null;
                        continue;
                    }

                    if (line == "-----") // 複数行アイテムの終了
                    {
                        multi_line = false;
                        continue;
                    }

                    if (!multi_line)
                    {
                        try
                        {
                            key = line.Split(':')[0].Trim();
                            value = line.Substring(key.Length + 1).Trim();
                        }
                        catch
                        {
                            Console.WriteLine($"Invalid format: key={key}, value={value}");
                        }

                        switch (key)
                        {
                            case "AUTHOR": entry.Author = value; break;
                            case "TITLE": entry.Title = string.IsNullOrWhiteSpace(value) ? defaultTitle : value; break;
                            case "STATUS": entry.Status = value; break;
                            case "BASENAME": entry.BaseName = value; break;
                            case "ALLOW COMMENTS": entry.AllowComment = value; break;
                            case "CONVERT BREAKS": entry.ConvertBreaks = value; break;
                            case "DATE": entry.Date = value; break;
                            case "TAGS": entry.Tags = value; break;
                            case "IMAGE": entry.Image = value; break;

                            case "CATEGORY": entry.Category.Add(value); break;

                            default: /* それ以外は複数行要素 */
                                multi_line = true; 
                                continue;
                        }
                    }
                    else
                    {
                        switch (key)
                        {
                            case "EXCERPT": entry.Excerpt.Add(line.Trim()); break;
                            case "COMMENT": entry.Comment.Add(line.Trim()); break;
                            case "BODY": entry.Body.Add(line.Trim()); break;
                            case "EXTENDED BODY": entry.ExtendedBody.Add(line.Trim()); break;

                            default:
                                Console.WriteLine($"Unknown Error: key={key}, value={line}");
                                break;
                        }
                    }
                }
                catch (Exception exception)
                {
                    Console.WriteLine($"Unknown Error: key={key}, value={line}, multiline={multi_line}, { exception.Message}");
                    break;
                }
            }

            // 解析の終わり、出力の開始
            Console.WriteLine($"{entries.Count} items found.");

            foreach (var e in entries)
            {
                if (!running) break;

                try
                {
                    var dir = Path.Combine(dest, e.BaseName);

                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                    var file = Path.Combine(dir, "index.md");

                    var content = new List<string>
                    {
                        $"---",
                        $"date: {DateTime.Parse(e.Date):o}",
                        $"draft: {(e.Status != "Publish").ToString().ToLower()}",
                        $"title: \"{e.Title}\"",
                        $"tags: [" + string.Join(", ", e.Category.Select(_ => $"\"{_}\"")) + "]",
                        $"eyecatch: {e.Image}",
                        $"---"
                    };
                    content.AddRange(e.Body);
                    content.Add("***"); // Insert <hr />
                    content.AddRange(e.ExtendedBody);

                    var text = string.Join("\n", content);
                    var image_count = 0;

                    if (fotolife.ToLower() == "true")
                    {
                        var regex = new Regex("src\\s*=\\s*(?:\"(?<1>[^\"]*)\"|(?<1>\\S+))");
                        var images = regex.Matches(text).Cast<Match>()
                            .Select(_ => _.Groups[1].Value)
                            .Where(_ => _.IndexOf("cdn-ak.f.st-hatena.com") >= 0)
                            .Where(_ => _.EndsWith(".png") || _.EndsWith(".jpg"));

                        foreach (var s in images)
                        {
                            try
                            {
                                var filename = Path.GetFileName(s);
                                var d = Path.Combine(dir, filename);

                                using (var request = new HttpRequestMessage(HttpMethod.Get, new Uri(s)))
                                using (var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead))
                                {
                                    if (response.StatusCode == HttpStatusCode.OK)
                                    {
                                        using (var stream = await response.Content.ReadAsStreamAsync())
                                        using (var fileStream = new FileStream(d, FileMode.Create, FileAccess.Write, FileShare.None))
                                        {
                                            stream.CopyTo(fileStream);
                                        }
                                    }
                                }

                                text = text.Replace(s, filename);
                                image_count++;
                            }
                            catch
                            {
                                Console.WriteLine($"Download Error: {s}");
                            }
                        }
                    }

                    File.WriteAllText(file, text);
                    
                    Console.WriteLine($"Resources are saved: {dir} , include { image_count } image(s)");
                }
                catch (Exception exception)
                {
                    Console.WriteLine($"Unknown Error: {e.BaseName}, {exception.Message}");
                }
            }
        }
    }
}
