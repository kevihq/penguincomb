using System.Reflection;
using GH_Toolkit_Core.Methods;
using Xunit;

namespace PenguinComb.Tests;

/// <summary>
/// Test helpers for the GH-Toolkit process-global state (the <see cref="DebugReader"/>
/// singleton holds the checksum dictionaries, and <see cref="GlobalVariables.ExeRootFolder"/>
/// points them at the application folder). Tests that redirect <c>ExeRootFolder</c> must
/// be serialized against every other test that touches the toolkit - see the
/// <c>ToolkitDebugState</c> collection - so the singleton is never initialized while a
/// redirect is in effect.
/// </summary>
internal static class DebugReaderState
{
    public static string GetExeRootFolder() =>
        (string)typeof(GlobalVariables).GetProperty("ExeRootFolder", BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!;

    public static void SetExeRootFolder(string path) =>
        typeof(GlobalVariables).GetProperty("ExeRootFolder", BindingFlags.Public | BindingFlags.Static)!.SetValue(null, path);
}

/// <summary>Serializes tests that mutate or depend on the process-global toolkit state.</summary>
[CollectionDefinition("ToolkitDebugState", DisableParallelization = true)]
public class ToolkitDebugStateCollection;
