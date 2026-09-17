using FIP.YesMan;

string path = Path.Combine(Path.GetTempPath(), "FIP-YesMan-serializer-" + Guid.NewGuid().ToString("N") + ".json");
try
{
    YesManSnapshot source = new()
    {
        role = "ModList",
        generatedUtc = DateTime.UtcNow.ToString("O"),
        gameVersion = "test",
        language = "English"
    };
    source.mods.Add(new YesManModRecord { loadOrder = 0, packageId = "FIP.Test", name = "Test" });
    for (int index = 0; index < 100_000; index++)
    {
        source.defInjected.Add(new YesManTextRecord
        {
            id = "DefInjected|ThingDef|Test_" + index + ".label",
            kind = "DefInjected",
            defType = "ThingDef",
            key = "Test_" + index + ".label",
            text = "Test value " + index,
            sourcePackageId = "FIP.Test"
        });
    }
    source.keyed.Add(new YesManTextRecord { id = "Keyed|Test.Key", kind = "Keyed", key = "Test.Key", text = "Hello" });
    source.strings.Add(new YesManTextRecord { id = "Strings|Names/Test.txt", kind = "Strings", path = "Strings/Names/Test.txt", text = "Alice\nBob" });

    SnapshotJsonSerializer.Write(path, source);
    YesManSnapshot restored = SnapshotJsonSerializer.Read<YesManSnapshot>(path);
    if (restored.mods.Count != 1 || restored.defInjected.Count != 100_000 || restored.keyed.Count != 1 || restored.strings.Count != 1)
    {
        throw new InvalidOperationException("Snapshot collections were not preserved.");
    }
    if (restored.defInjected[99_999].text != "Test value 99999")
    {
        throw new InvalidOperationException("Snapshot content was not preserved.");
    }
    Console.WriteLine($"Snapshot serializer smoke test passed: {restored.defInjected.Count:N0} DefInjected, {new FileInfo(path).Length:N0} bytes.");
}
finally
{
    if (File.Exists(path))
    {
        File.Delete(path);
    }
}
