namespace Trackmania2020Toolbox;

public record MapDownloadRecord(string Name, string? FileName, string? FileUrl, string? Prefix);

public record BrowserItem(string DisplayName, string FullPath, bool IsDirectory)
{
    public string Icon => IsDirectory ? "📁" : "📄";
}

public record MapRef(int Campaign, int? Map = null) : IComparable<MapRef>
{
    public int CompareTo(MapRef? other)
    {
        if (other == null) return 1;
        if (Campaign != other.Campaign) return Campaign.CompareTo(other.Campaign);
        if (!Map.HasValue && !other.Map.HasValue) return 0;
        if (!Map.HasValue) return -1;
        if (!other.Map.HasValue) return 1;
        return Map.Value.CompareTo(other.Map.Value);
    }
}

public record SeasonalRef(int Year, int SeasonOrder, int? Map = null) : IComparable<SeasonalRef>
{
    public int CompareTo(SeasonalRef? other)
    {
        if (other == null) return 1;
        if (Year != other.Year) return Year.CompareTo(other.Year);
        if (SeasonOrder != other.SeasonOrder) return SeasonOrder.CompareTo(other.SeasonOrder);
        if (!Map.HasValue && !other.Map.HasValue) return 0;
        if (!Map.HasValue) return -1;
        if (!other.Map.HasValue) return 1;
        return Map.Value.CompareTo(other.Map.Value);
    }
}
