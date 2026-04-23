using System;
using System.IO;

public class Program {
    public static void Main() {
        var tempDir = Path.Combine(Path.GetTempPath(), "test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var mapPath = Path.Combine(tempDir, "test.Map.Gbx");
        File.WriteAllBytes(mapPath, new byte[10]);
        var files = Directory.GetFiles(tempDir, "*.Map.Gbx", SearchOption.AllDirectories);
        Console.WriteLine("Files count: " + files.Length);
        foreach(var f in files) Console.WriteLine("File: " + f);
    }
}
