using System;
using Tomlyn;
public class Test {
    public static void Main() {
        var model = Toml.ToModel("a = 1");
        Console.WriteLine(model["a"]);
    }
}
