#:package ManiaAPI.TrackmaniaIO@2.*
#:package TmEssentials@2.*

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using ManiaAPI.TrackmaniaIO;
using TmEssentials;

internal static class WeeklyShortsDownloader
{
    private static readonly string UserAgent = "WeeklyShortsDownloader/1.0 (contact: trackmania-downloader-script@example.com)";
    private static readonly HttpClient HttpClient = new HttpClient();

    public static async Task Main(string[] args)
    {
        HttpClient.DefaultRequestHeaders.Add("User-Agent", UserAgent);

        using var tmio = new TrackmaniaIO(UserAgent);

        Console.WriteLine("Which weeks would you like to download? (e.g., 68, 65-67, 60, 62)");
        var input = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(input))
        {
            Console.WriteLine("No input provided. Exiting.");
            return;
        }

        var requestedWeeks = ParseWeeks(input);
        if (!requestedWeeks.Any())
        {
            Console.WriteLine("No valid week numbers found in input.");
            return;
        }

        Console.WriteLine("Fetching available Weekly Shorts campaigns...");
        var campaigns = await FetchAllWeeklyCampaigns(tmio);

        foreach (var weekNum in requestedWeeks)
        {
            var weekName = $"Week {weekNum}";
            var campaign = campaigns.FirstOrDefault(c => c.Name.Equals(weekName, StringComparison.OrdinalIgnoreCase));

            if (campaign == null)
            {
                Console.WriteLine($"Error: Could not find campaign for {weekName}. Skipping.");
                continue;
            }

            Console.WriteLine($"Processing {weekName} (ID: {campaign.Id})...");
            try
            {
                await DownloadCampaign(tmio, campaign, weekNum);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing {weekName}: {ex.Message}");
            }
        }

        Console.WriteLine("Finished.");
    }

    private static List<int> ParseWeeks(string input)
    {
        var result = new HashSet<int>();
        var parts = input.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var part in parts)
        {
            if (part.Contains("-"))
            {
                var range = part.Split('-');
                if (range.Length == 2 && int.TryParse(range[0], out var start) && int.TryParse(range[1], out var end))
                {
                    for (var i = Math.Min(start, end); i <= Math.Max(start, end); i++)
                    {
                        result.Add(i);
                    }
                }
            }
            else if (int.TryParse(part, out var num))
            {
                result.Add(num);
            }
        }

        return result.OrderBy(n => n).ToList();
    }

    private static async Task<List<CampaignItem>> FetchAllWeeklyCampaigns(TrackmaniaIO tmio)
    {
        var allCampaigns = new List<CampaignItem>();
        int page = 0;
        int pageCount = 1;

        while (page < pageCount)
        {
            var response = await tmio.GetWeeklyShortCampaignsAsync(page);
            if (response.Campaigns != null)
            {
                allCampaigns.AddRange(response.Campaigns);
            }
            pageCount = response.PageCount;
            page++;
        }

        return allCampaigns;
    }

    private static async Task DownloadCampaign(TrackmaniaIO tmio, CampaignItem campaign, int weekNum)
    {
        var fullCampaign = await tmio.GetWeeklyShortCampaignAsync(campaign.Id);
        if (fullCampaign == null || fullCampaign.Playlist == null)
        {
            Console.WriteLine($"Could not retrieve playlist for Week {weekNum}.");
            return;
        }

        var downloadDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Trackmania2020", "Maps", "Downloaded", "Weekly Shorts", weekNum.ToString());

        if (!Directory.Exists(downloadDir))
        {
            Directory.CreateDirectory(downloadDir);
        }

        foreach (var map in fullCampaign.Playlist)
        {
            var fileName = map.FileName;
            if (string.IsNullOrEmpty(fileName))
            {
                fileName = map.Name;
            }

            // Remove color codes and trim whitespace around the name, preserving the extension
            fileName = TextFormatter.Deformat(fileName);
            if (fileName.EndsWith(".Map.Gbx", StringComparison.OrdinalIgnoreCase))
            {
                fileName = fileName.Substring(0, fileName.Length - 8).Trim() + ".Map.Gbx";
            }
            else if (fileName.EndsWith(".Gbx", StringComparison.OrdinalIgnoreCase))
            {
                fileName = fileName.Substring(0, fileName.Length - 4).Trim() + ".Gbx";
            }
            else
            {
                fileName = fileName.Trim();
            }

            // Clean filename from illegal characters
            foreach (var c in Path.GetInvalidFileNameChars())
            {
                fileName = fileName.Replace(c, '_');
            }

            var filePath = Path.Combine(downloadDir, fileName);

            if (string.IsNullOrEmpty(map.FileUrl))
            {
                 Console.WriteLine($"No download URL for map: {map.Name}");
                 continue;
            }

            Console.WriteLine($"Downloading {map.Name}...");
            var fileData = await HttpClient.GetByteArrayAsync(map.FileUrl);
            await File.WriteAllBytesAsync(filePath, fileData);

            Console.WriteLine($"Saved to {filePath}");

            // 1 second delay
            await Task.Delay(1000);
        }
    }
}
