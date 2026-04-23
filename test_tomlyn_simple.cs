using System;
public class Test {
    public static void Main() {
        var content = Tomlyn.Toml.FromModel(new Tomlyn.Model.TomlTable { ["a"] = 1 });
        Console.WriteLine(content);
    }
}
