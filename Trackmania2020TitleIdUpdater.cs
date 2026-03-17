#:package GBX.NET@2.*
#:package GBX.NET.LZO@2.*

using GBX.NET;
using GBX.NET.Engines.Game;
using GBX.NET.LZO;

Gbx.LZO = new Lzo();

if (args.Length == 0)
{
    Console.WriteLine("Please specify a folder path.");
    return;
}

var folderPath = args[0];
if (!System.IO.Directory.Exists(folderPath))
{
    Console.WriteLine("The specified folder does not exist.");
    return;
}

var files = System.IO.Directory.GetFiles(folderPath, "*.Map.Gbx", System.IO.SearchOption.AllDirectories);

var filesAnalyzed = 0;
var titlesChanged = 0;

foreach (var file in files)
{
    try
    {
        var gbx = Gbx.Parse<CGameCtnChallenge>(file);
        var map = gbx.Node;
        filesAnalyzed++;

        if (map.TitleId == "OrbitalDev@falguiere")
        {
            map.TitleId = "TMStadium";
            
            gbx.Save(file);
            Console.WriteLine($"Saved: {file}");
            titlesChanged++;
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Failed to process {file}: {ex.Message}");
    }
}

Console.WriteLine($"\nAnalysis complete.");
Console.WriteLine($"Files analyzed successfully: {filesAnalyzed} out of {files.Length}");
Console.WriteLine($"Title IDs changed: {titlesChanged}");