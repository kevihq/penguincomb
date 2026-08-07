using System;
using System.Linq;
var asm = typeof(Avalonia.Controls.Window).Assembly;
foreach (var name in new[]{"GroupBox","StatusBar","NumericUpDown","TabItem","TabControl","ListBox","ComboBox","RadioButton","CheckBox","WrapPanel","Border"})
{
    var t = asm.GetType("Avalonia.Controls." + name);
    Console.WriteLine($"{name}: {(t != null ? "FOUND" : "MISSING")}");
}
