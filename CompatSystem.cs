using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using CompatChecker.Helpers;
using Newtonsoft.Json.Linq;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.Core;
using Terraria.Social.Base;

// ReSharper disable PropertyCanBeMadeInitOnly.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace CompatChecker;

public class CompatSystem : ModSystem
{
    /// <summary>
    ///     The environment the mod is currently running in.
    /// </summary>
    private const Environment ActiveEnvironment = Environment.Development;

    /// <summary>
    ///     The time in hours that the data cache should be kept for before sending a new request.
    ///     This is only used when <see cref="ActiveEnvironment" /> is set to <see cref="Environment.Production" />.
    /// </summary>
    private const double CacheTimeHours = 0.25; // 15 minutes for now

    private readonly JsonSerializerOptions _jsonSerializerOptions =
        new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    public static ApiCompatibilityData CompatibilityData { get; private set; }
    
    public static string RequestError { get; private set; }

    public static Dictionary<ulong, LocalMod> LocalModByID { get; } = new();

    public static Dictionary<string, ulong> ModIDByName { get; } = new();

    public static List<string> WorkshopModNames { get; } = [];

    public override void OnModLoad()
    {
        // We handle exceptions in this class
        Logging.IgnoreExceptionContents("CompatChecker.CompatSystem");

        var mods = new List<ApiClientMod>();
        foreach (var repo in ModOrganizer.WorkshopFileFinder.ModPaths)
        {
            var workshopJsonPath = Path.Combine(repo, "workshop.json");
            if (!File.Exists(workshopJsonPath)) continue;
            var workshopJson = File.ReadAllText(workshopJsonPath);
            if (!JObject.Parse(workshopJson).TryGetValue("Publicity", out var value) ||
                value.ToObject<WorkshopItemPublicSettingId>() != WorkshopItemPublicSettingId.Public) continue;
            //Mod.Logger.Debug($"Found public workshop mod: {repo}");
            var tmodPath = Directory.GetFiles(repo, "*.tmod", SearchOption.AllDirectories).LastOrDefault();
            if (tmodPath == null) continue;
            //Mod.Logger.Debug($"Found tmod file: {tmodPath}");
            if (!ModOrganizer.TryReadLocalMod(ModLocation.Workshop, tmodPath, out var mod) || !mod.Enabled ||
                !ulong.TryParse(repo.Split(Path.DirectorySeparatorChar).Last(), out var id)) continue;
            Mod.Logger.Info(
                $"Found enabled workshop mod: {mod.Name} v{mod.Version} v{mod.tModLoaderVersion} (id: {id})");
            WorkshopModNames.Add(mod.Name);
            LocalModByID[id] = mod;
            ModIDByName[mod.Name] = id;
            mods.Add(new ApiClientMod
            {
                ID = id,
                Version = mod.Version.ToString(),
                TMLVersion = mod.tModLoaderVersion.ToString()
            });
        }

        // Convert the request to JSON
        var requestJson = JsonSerializer.Serialize(new ApiCheckCompatibilityRequest
        {
            Mods = mods,
            IncludeGithub = true,
            IncludeLasts = false
        }, _jsonSerializerOptions);

        Mod.Logger.Debug($"Request JSON: {requestJson}");

        // Generate a hash of the request JSON
        var requestHash = HashGenerator.GenerateHash(requestJson);
        Mod.Logger.Debug($"Request hash: {requestHash}");

        // Check if the request is identical to the last request sent, and if so, skip sending the request and use the cached response
        var compatCheckerHashPath = Path.Combine(Main.SavePath, "compat checker.sha256");
        var compatCheckerDataPath = Path.Combine(Main.SavePath, "compat checker.json");
        if (ActiveEnvironment == Environment.Production)
            if (File.Exists(compatCheckerHashPath) && File.Exists(compatCheckerDataPath))
            {
                var timeWritten = File.GetLastWriteTimeUtc(compatCheckerHashPath);
                if (timeWritten.AddHours(CacheTimeHours) < DateTime.UtcNow)
                {
                    Mod.Logger.Debug($"Cached response is older than {CacheTimeHours} hour(s), sending a new request");
                }
                else
                {
                    var storedHash = File.ReadAllText(compatCheckerHashPath);
                    if (storedHash == requestHash)
                    {
                        Mod.Logger.Debug("Request is identical to the last request sent, using cached response");
                        var responseBody = File.ReadAllText(compatCheckerDataPath);
                        Mod.Logger.Debug($"Response JSON: {responseBody}");
                        var compatibilityResponse = JsonSerializer.Deserialize<ApiCheckCompatibilityResponse>(
                            responseBody,
                            _jsonSerializerOptions);
                        if (compatibilityResponse.Status == "success")
                            CompatibilityData = compatibilityResponse.Data;
                        return;
                    }
                }
            }
        
        // Get private static field using reflection in Logging class
        var ignoreContents = Logging.ignoreContents;
        if (ignoreContents != null && !ignoreContents.Contains("System.Net.Http.HttpRequestException"))
            ignoreContents.Add("System.Net.Http.HttpRequestException");

        Task.Run(async () =>
        {
            // Create an instance of HttpClient
            using var client = new HttpClient();

            // Add some headers to the request
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Add("X-Mod-Version",
                ModContent.GetInstance<CompatChecker>().Version.ToString());
            client.DefaultRequestHeaders.Add("X-Terraria-Version", Main.versionNumber);
            client.DefaultRequestHeaders.Add("X-TModLoader-Version", ModLoader.versionedName);
            client.DefaultRequestHeaders.Add("X-Language-Active-Culture", Language.ActiveCulture.LegacyId.ToString());
            client.DefaultRequestHeaders.Add("X-Request-Hash", requestHash);

            // Send the request
            var content = new StringContent(requestJson, Encoding.UTF8, "application/json");
            HttpResponseMessage response = null;
            try
            {
                Mod.Logger.Debug($"Sending request to {GetAPIEndpoint()}/api/check-compatibility");
                response = await client.PostAsync(GetAPIEndpoint() + "/api/check-compatibility", content);

                // Ensure the request was successful
                response.EnsureSuccessStatusCode();

                // Get the response
                var responseBody = await response.Content.ReadAsStringAsync();
                Mod.Logger.Debug($"Response JSON: {responseBody}");

                // Save the response to a file
                if (ActiveEnvironment == Environment.Production)
                {
                    await File.WriteAllTextAsync(compatCheckerDataPath, responseBody);
                    await File.WriteAllTextAsync(compatCheckerHashPath, requestHash);
                }

                // Deserialize the response
                var compatibilityResponse = JsonSerializer.Deserialize<ApiCheckCompatibilityResponse>(responseBody,
                    _jsonSerializerOptions);
                if (compatibilityResponse.Status == "success")
                    CompatibilityData = compatibilityResponse.Data;
            }
            catch (Exception)
            {
                RequestError = response is { StatusCode: HttpStatusCode.ServiceUnavailable } 
                    ? "The server is currently undergoing maintenance!\nPlease try again later."
                    : "There was an error while communicating with the server!\nPlease try again later.";

                if (!string.IsNullOrEmpty(RequestError))
                    Mod.Logger.Error($"Failed to send request to API: {RequestError}");
            }
            
            //ignoreContents?.Remove("System.Net.Http.HttpRequestException");
        });
    }

    public override void Unload()
    {
        CompatibilityData = null;
    }

    private static string GetAPIEndpoint()
    {
        return ActiveEnvironment == Environment.Development
            ? "http://localhost:3000"
            : "https://compat-checker.tmod.page";
    }

    private class ApiClientMod
    {
        public ulong ID { get; set; }
        public string Version { get; set; }
        public string TMLVersion { get; set; }
    }

    private class ApiCheckCompatibilityRequest
    {
        public List<ApiClientMod> Mods { get; set; }
        public bool IncludeGithub { get; set; }
        public bool IncludeLasts { get; set; }
    }

    public class ApiResponse
    {
        public string Status { get; set; }
        public string Message { get; set; }
    }

    public class ApiCheckCompatibilityResponse : ApiResponse
    {
        public ApiCompatibilityData Data { get; set; }
    }

    public class ApiCompatibilityData
    {
        public ApiIndividualCompatibilityData Individual { get; set; }
        public ApiBetweenCompatibilityData Between { get; set; }
        public ApiExtraCompatibilityData Extra { get; set; }
    }

    public class ApiIndividualCompatibilityData
    {
        public ApiMultiplayerCompatibility[] MPCompatible { get; set; }
        public ApiMultiplayerCompatibility[] MPUnstable { get; set; }
        public ApiMultiplayerCompatibility[] MPIncompatible { get; set; }
    }

    public class ApiMultiplayerCompatibility
    {
        public ulong ModID { get; set; }

        [JsonPropertyName("version")] public string VersionRange { get; set; }

        public string Note { get; set; }

        [JsonPropertyName("issue_ids")] public int[] IssueIDs { get; set; }

        [JsonPropertyName("fix_mods")] public string[] FixMods { get; set; }
    }

    public class ApiBetweenCompatibilityData // TODO: Support this
    {
    }

    public class ApiExtraCompatibilityData
    {
        public ApiGithubInfo[] GithubInfo { get; set; }
    }

    public class ApiGithubInfo
    {
        public ulong ModID { get; set; }
        public string Repo { get; set; }
    }

    private enum Environment
    {
        Development,
        Production
    }
}