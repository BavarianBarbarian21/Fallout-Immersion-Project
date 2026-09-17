using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace FIP.YesMan;

[Serializable, DataContract]
public sealed class YesManSnapshot
{
    [DataMember(Order = 0)]
    public int schemaVersion = 1;
    [DataMember(Order = 1)]
    public string role;
    [DataMember(Order = 2)]
    public string generatedUtc;
    [DataMember(Order = 3)]
    public string gameVersion;
    [DataMember(Order = 4)]
    public string language;
    [DataMember(Order = 5)]
    public List<YesManModRecord> mods = new();
    [DataMember(Order = 6)]
    public List<YesManTextRecord> defInjected = new();
    [DataMember(Order = 7)]
    public List<YesManTextRecord> keyed = new();
    [DataMember(Order = 8)]
    public List<YesManTextRecord> strings = new();
    [DataMember(Order = 9)]
    public List<string> warnings = new();
}

[Serializable, DataContract]
public sealed class YesManModRecord
{
    [DataMember(Order = 0)]
    public int loadOrder;
    [DataMember(Order = 1)]
    public string packageId;
    [DataMember(Order = 2)]
    public string name;
    [DataMember(Order = 3)]
    public string version;
    [DataMember(Order = 4)]
    public string rootDir;
    [DataMember(Order = 5)]
    public List<string> activeContentRoots = new();
}

[Serializable, DataContract]
public sealed class YesManTextRecord
{
    [DataMember(Order = 0)]
    public string id;
    [DataMember(Order = 1)]
    public string kind;
    [DataMember(Order = 2)]
    public string defType;
    [DataMember(Order = 3)]
    public string key;
    [DataMember(Order = 4)]
    public string path;
    [DataMember(Order = 5)]
    public string text;
    [DataMember(Order = 6)]
    public string sourcePackageId;
}

[Serializable, DataContract]
public sealed class YesManDeltaManifest
{
    [DataMember(Order = 0)]
    public int schemaVersion = 1;
    [DataMember(Order = 1)]
    public string generatedUtc;
    [DataMember(Order = 2)]
    public string coreSnapshot;
    [DataMember(Order = 3)]
    public string modListSnapshot;
    [DataMember(Order = 4)]
    public string coreGameVersion;
    [DataMember(Order = 5)]
    public string modListGameVersion;
    [DataMember(Order = 6)]
    public int defInjectedCount;
    [DataMember(Order = 7)]
    public int keyedCount;
    [DataMember(Order = 8)]
    public int stringsFileCount;
    [DataMember(Order = 9)]
    public int excludedUnchangedCount;
    [DataMember(Order = 10)]
    public List<string> sourcePackages = new();
    [DataMember(Order = 11)]
    public List<string> warnings = new();
}
