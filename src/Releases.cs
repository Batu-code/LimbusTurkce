using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace LimbusTurkce
{
    // Yamanin bir surumu yani GitHub'daki bir release
    class Release
    {
        public Version Version;
        public string Tag;
        public string Title;
        public string Notes;
        public DateTime Date;
        public string PackUrl;
        public long PackSize;
        public string PackSha256;
        public string AppUrl;
        public Version AppVersion;

        public string Label => "v" + Version.ToString(3);
    }

    // Surumleri GitHub'dan okur ve dosyalari indirir
    // Release notunun icine gorunmeyen iki satir gomulu
    //   paket-sha256   indirilen zip'i dogrulamak icin
    //   uygulama       uygulamanin kendini guncellemesi icin
    // tools/yayinla.js bunlari kendisi yazar
    static class Releases
    {
        public const string Repo = "Batu-code/LimbusTurkce";
        public const string PackAsset = "turkce-yama.zip";
        public const string AppAsset = "LimbusTurkce.exe";

        // Test icin GitHub yerine yerel bir surum listesi verilebilir
        // tools/yayinla.js --deneme bu dosyayi uretir
        static string LocalList => Environment.GetEnvironmentVariable("LIMBUSTURKCE_SURUMLER");

        static readonly HttpClient Http = CreateClient();

        static HttpClient CreateClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            // GitHub API kimliksiz istegi User-Agent olmadan reddediyor
            client.DefaultRequestHeaders.UserAgent.ParseAdd("LimbusTurkce/" + SelfUpdate.Current.ToString(3));
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            return client;
        }

        public static async Task<List<Release>> FetchAsync()
        {
            string json;
            if (!string.IsNullOrEmpty(LocalList))
                json = File.ReadAllText(LocalList);
            else
            {
                var response = await Http.GetAsync($"https://api.github.com/repos/{Repo}/releases?per_page=50");
                if ((int)response.StatusCode == 403)
                    throw new InvalidOperationException("GitHub çok sık soruldu, bir saat sonra tekrar deneyin.");
                if ((int)response.StatusCode == 404)
                    throw new InvalidOperationException("Yama deposu bulunamadı.");
                response.EnsureSuccessStatusCode();
                json = await response.Content.ReadAsStringAsync();
            }

            GhRelease[] raw;
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                raw = (GhRelease[])new DataContractJsonSerializer(typeof(GhRelease[])).ReadObject(stream);

            return raw
                .Where(r => !r.Draft && !r.Prerelease)
                .Select(ToRelease)
                .Where(r => r != null)
                .OrderByDescending(r => r.Version)
                .ToList();
        }

        static Release ToRelease(GhRelease r)
        {
            if (!TryParseVersion(r.TagName, out var version)) return null;
            var pack = r.Assets?.FirstOrDefault(a => a.Name == PackAsset);
            if (pack == null) return null;
            var app = r.Assets.FirstOrDefault(a => a.Name == AppAsset);

            var body = r.Body ?? "";
            TryParseVersion(Hidden(body, "uygulama"), out var appVersion);
            var sha = Hidden(body, "paket-sha256");
            // GitHub artik her dosyanin ozetini kendisi de veriyor
            // Not elle duzenlenip silinse bile dogrulama surer
            if (sha == null && pack.Digest != null && pack.Digest.StartsWith("sha256:"))
                sha = pack.Digest.Substring(7);

            DateTime.TryParse(r.PublishedAt, out var date);
            return new Release
            {
                Version = version,
                Tag = r.TagName,
                Title = string.IsNullOrWhiteSpace(r.Name) ? r.TagName : r.Name,
                Notes = Regex.Replace(body, @"<!--.*?-->", "", RegexOptions.Singleline).Trim(),
                Date = date,
                PackUrl = pack.Url,
                PackSize = pack.Size,
                PackSha256 = sha?.ToLowerInvariant(),
                AppUrl = app?.Url,
                AppVersion = app == null ? null : appVersion,
            };
        }

        static string Hidden(string body, string key)
        {
            var m = Regex.Match(body, @"<!--\s*" + Regex.Escape(key) + @":\s*([^\s]+)\s*-->");
            return m.Success ? m.Groups[1].Value : null;
        }

        public static bool TryParseVersion(string text, out Version version)
        {
            version = null;
            if (string.IsNullOrEmpty(text)) return false;
            if (!Version.TryParse(text.TrimStart('v', 'V'), out var parsed)) return false;
            version = new Version(parsed.Major, parsed.Minor, Math.Max(parsed.Build, 0));
            return true;
        }

        public static async Task DownloadAsync(string url, string destination, IProgress<int> progress)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            var partial = destination + ".indiriliyor";

            if (File.Exists(url))
            {
                File.Copy(url, partial, true);
            }
            else
            {
                using (var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();
                    var total = response.Content.Headers.ContentLength ?? 0;
                    using (var input = await response.Content.ReadAsStreamAsync())
                    using (var output = File.Create(partial))
                    {
                        var buffer = new byte[81920];
                        long done = 0;
                        int read;
                        while ((read = await input.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await output.WriteAsync(buffer, 0, read);
                            done += read;
                            if (total > 0) progress?.Report((int)(done * 100 / total));
                        }
                    }
                }
            }

            if (File.Exists(destination)) File.Delete(destination);
            File.Move(partial, destination);
        }

        public static string Sha256(string file)
        {
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(file))
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

        [DataContract]
        class GhRelease
        {
            [DataMember(Name = "tag_name")] public string TagName { get; set; }
            [DataMember(Name = "name")] public string Name { get; set; }
            [DataMember(Name = "body")] public string Body { get; set; }
            [DataMember(Name = "draft")] public bool Draft { get; set; }
            [DataMember(Name = "prerelease")] public bool Prerelease { get; set; }
            [DataMember(Name = "published_at")] public string PublishedAt { get; set; }
            [DataMember(Name = "assets")] public GhAsset[] Assets { get; set; }
        }

        [DataContract]
        class GhAsset
        {
            [DataMember(Name = "name")] public string Name { get; set; }
            [DataMember(Name = "browser_download_url")] public string Url { get; set; }
            [DataMember(Name = "size")] public long Size { get; set; }
            [DataMember(Name = "digest")] public string Digest { get; set; }
        }
    }
}
