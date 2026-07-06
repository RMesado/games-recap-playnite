using GamesRecap.Models;
using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace GamesRecap.Services
{
    public class GamesRecapApiClient : IDisposable
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private readonly HttpClient http;
        private readonly HttpClient htmlHttp;
        private readonly LocalDatabase db;
        private const string BaseUrl = "https://gamesrecap.io/";
        private const string DefaultInertiaVersion = "91c5bce49007757d62740bf9f1aacac6";

        public GamesRecapApiClient(LocalDatabase database)
        {
            db = database;
            http = new HttpClient
            {
                BaseAddress = new Uri(BaseUrl),
                Timeout = TimeSpan.FromSeconds(30)
            };
            http.DefaultRequestHeaders.Add("Accept", "application/json");
            http.DefaultRequestHeaders.Add("X-Inertia", "true");

            htmlHttp = new HttpClient
            {
                BaseAddress = new Uri(BaseUrl),
                Timeout = TimeSpan.FromSeconds(15)
            };
        }

        public async Task<List<Card>> FetchCardsByIdsAsync(List<int> ids)
        {
            if (ids == null || ids.Count == 0) return new List<Card>();

            var filters = new ActiveFilters
            {
                WishlistedIds = ids,
                WishlistedMode = "include"
            };

            var allCards = new List<Card>();
            var page = 1;
            var version = db.GetInertiaVersion() ?? DefaultInertiaVersion;
            var wishlistedIds = string.Join(",", ids);

            while (true)
            {
                var query = BuildQuery(filters);
                if (page > 1)
                    query = (string.IsNullOrEmpty(query) ? "?" : query + "&") + "page=" + page;

                var response = await SendWithVersionAsync(query, version, wishlistedIds, "include");
                if (response == null) break;

                var json = await response.Content.ReadAsStringAsync();
                var fullResponse = Deserialize<InertiaResponse>(json);

                if (fullResponse?.Version != null && fullResponse.Version != version)
                {
                    version = fullResponse.Version;
                    db.SetInertiaVersion(version);
                }

                var cards = fullResponse?.Props?.Pages?.Data;
                if (cards == null || cards.Count == 0) break;

                allCards.AddRange(cards);

                var lastPage = fullResponse.Props.Pages.LastPage;
                if (page >= lastPage) break;
                page++;
            }

            return allCards;
        }

        public async Task<HomeProps> FetchCardsAsync(ActiveFilters filters)
        {
            var url = BuildQuery(filters);
            var version = db.GetInertiaVersion() ?? DefaultInertiaVersion;

            var wishlistedIds = (filters.WishlistedIds != null && filters.WishlistedIds.Count > 0)
                ? string.Join(",", filters.WishlistedIds) : null;
            var wishlistedMode = !string.IsNullOrEmpty(filters.WishlistedMode) ? filters.WishlistedMode : null;

            var response = await SendWithVersionAsync(url, version, wishlistedIds, wishlistedMode);

            if (response == null)
            {
                logger.Warn("FetchCardsAsync: failed after all retries");
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            var fullResponse = Deserialize<InertiaResponse>(json);

            if (fullResponse?.Version != null && fullResponse.Version != version)
            {
                db.SetInertiaVersion(fullResponse.Version);
                logger.Info($"Updated Inertia version from response: {fullResponse.Version}");
            }

            return fullResponse?.Props;
        }

        private async Task<HttpResponseMessage> SendWithVersionAsync(string url, string version, string wishlistedIds = null, string wishlistedMode = null)
        {
            const int maxRetries = 2;

            for (int attempt = 0; attempt <= maxRetries; attempt++)
            {
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Remove("X-Inertia-Version");
                request.Headers.Add("X-Inertia-Version", version);

                if (!string.IsNullOrEmpty(wishlistedIds))
                {
                    request.Headers.Remove("X-Wishlisted-Ids");
                    request.Headers.Add("X-Wishlisted-Ids", wishlistedIds);
                }

                if (!string.IsNullOrEmpty(wishlistedMode))
                {
                    request.Headers.Remove("X-Wishlisted-Mode");
                    request.Headers.Add("X-Wishlisted-Mode", wishlistedMode);
                }

                var response = await http.SendAsync(request);

                if ((int)response.StatusCode == 409)
                {
                    logger.Warn($"409 Conflict on attempt {attempt + 1}, scraping HTML for new version");
                    var newVersion = await ScrapeVersionFromHtmlAsync(url);
                    if (!string.IsNullOrEmpty(newVersion) && newVersion != version)
                    {
                        version = newVersion;
                        db.SetInertiaVersion(newVersion);
                        logger.Info($"Updated Inertia version to: {newVersion}");
                        continue;
                    }
                }

                if ((int)response.StatusCode == 429 && attempt < maxRetries)
                {
                    logger.Warn("Rate limited by GamesRecap API, waiting 5s before retry");
                    await Task.Delay(5000);
                    continue;
                }

                if ((int)response.StatusCode == 429 && attempt >= maxRetries)
                {
                    logger.Error("Rate limited, max retries reached");
                    return null;
                }

                response.EnsureSuccessStatusCode();
                return response;
            }

            return null;
        }

        private async Task<string> ScrapeVersionFromHtmlAsync(string queryUrl)
        {
            try
            {
                var fullUrl = BaseUrl.TrimEnd('/') + queryUrl;
                var html = await htmlHttp.GetStringAsync(fullUrl);
                var match = Regex.Match(html, @"""version"":""([0-9a-f]{32})""");
                if (match.Success)
                {
                    logger.Info($"Scraped version from HTML: {match.Groups[1].Value}");
                    return match.Groups[1].Value;
                }
                match = Regex.Match(html, @"[0-9a-f]{32}");
                if (match.Success)
                {
                    logger.Info($"Scraped version from HTML: {match.Value}");
                    return match.Value;
                }
                logger.Warn("Could not find version hash in HTML");
                return null;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to scrape version from HTML");
                return null;
            }
        }

        private string BuildQuery(ActiveFilters filters)
        {
            var parts = new List<string>();

            if (!string.IsNullOrEmpty(filters.Q))
                parts.Add("q=" + Uri.EscapeDataString(filters.Q));

            AddIntList(parts, "platforms", filters.Platforms);
            AddIntList(parts, "exclude_platforms", filters.ExcludePlatforms);
            AddIntList(parts, "genres", filters.Genres);
            AddIntList(parts, "exclude_genres", filters.ExcludeGenres);
            AddIntList(parts, "tags", filters.Tags);
            AddIntList(parts, "exclude_tags", filters.ExcludeTags);
            AddIntList(parts, "showcases", filters.Showcases);
            AddIntList(parts, "hidden_ids", filters.HiddenIds);
            AddIntList(parts, "seen_ids", filters.SeenIds);

            if (!string.IsNullOrEmpty(filters.SeenMode))
                parts.Add("seen_mode=" + Uri.EscapeDataString(filters.SeenMode));

            if (filters.Page.HasValue && filters.Page.Value > 1)
                parts.Add("page=" + filters.Page.Value);

            if (!string.IsNullOrEmpty(filters.ReleaseFrom))
                parts.Add("release_from=" + Uri.EscapeDataString(filters.ReleaseFrom));

            if (!string.IsNullOrEmpty(filters.ReleaseTo))
                parts.Add("release_to=" + Uri.EscapeDataString(filters.ReleaseTo));

            if (!string.IsNullOrEmpty(filters.Sort) && filters.Sort != "newest")
                parts.Add("sort=" + Uri.EscapeDataString(filters.Sort));

            if (!string.IsNullOrEmpty(filters.View) && filters.View != "cards")
                parts.Add("view=" + Uri.EscapeDataString(filters.View));

            return parts.Count > 0 ? "?" + string.Join("&", parts) : "";
        }

        private static void AddIntList(List<string> parts, string key, List<int> values)
        {
            if (values != null && values.Count > 0)
                parts.Add(key + "=" + string.Join(",", values));
        }

        private static T Deserialize<T>(string json)
        {
            var serializer = new DataContractJsonSerializer(typeof(T));
            using var ms = new MemoryStream(Encoding.UTF8.GetBytes(json));
            return (T)serializer.ReadObject(ms);
        }

        public void Dispose()
        {
            http?.Dispose();
            htmlHttp?.Dispose();
        }
    }
}
