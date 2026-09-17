using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;

namespace FIP.YesMan;

internal static class SnapshotJsonSerializer
{
    private static DataContractJsonSerializer CreateSerializer<T>()
    {
        return new DataContractJsonSerializer(typeof(T), new DataContractJsonSerializerSettings
        {
            MaxItemsInObjectGraph = int.MaxValue
        });
    }

    public static void Write<T>(string path, T value)
    {
        string directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string temporary = path + ".tmp";
        DataContractJsonSerializer serializer = CreateSerializer<T>();
        using (FileStream stream = new(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            serializer.WriteObject(stream, value);
            byte[] newline = Encoding.UTF8.GetBytes(Environment.NewLine);
            stream.Write(newline, 0, newline.Length);
            stream.Flush(true);
        }

        if (File.Exists(path))
        {
            string backup = path + ".bak";
            if (File.Exists(backup))
            {
                File.Delete(backup);
            }
            File.Replace(temporary, path, backup);
            if (File.Exists(backup))
            {
                File.Delete(backup);
            }
        }
        else
        {
            File.Move(temporary, path);
        }
    }

    public static T Read<T>(string path)
    {
        DataContractJsonSerializer serializer = CreateSerializer<T>();
        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return (T)serializer.ReadObject(stream);
    }
}
