using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace JsonFlatFileDataStore
{
    public interface IStorageAccess
    {
        Task<string> ReadJson(string path, Func<string, string> encryptJson, Func<string, string> decryptJson);
        Task<bool> WriteJson(string path, Func<string, string> encryptJson, string content);
    }

    public static class StorageAccess
    {
        private static IStorageAccess _storageAccess;

        public static IStorageAccess GetStorageAccess()
        {
            _storageAccess ??= new FileStorage();
            return _storageAccess;
        }

        public static void SetStorageAccess(IStorageAccess storageAccess)
        {
            _storageAccess = storageAccess;
        }
    }

    public class FileStorage : IStorageAccess
    {
        public async Task<string> ReadJson(string path, Func<string, string> encryptJson, Func<string, string> decryptJson)
        {
            Stopwatch sw = null;
            var json = "{}";

            while (true)
            {
                try
                {
                    json = await File.ReadAllTextAsync(path);
                    break;
                }
                catch (FileNotFoundException)
                {
                    json = encryptJson(json);
                    await File.WriteAllTextAsync(path, json);
                    break;
                }
                catch (IOException e) when (e.Message.Contains("because it is being used by another process"))
                {
                    // If some other process is using this file, retry operation unless elapsed times is greater than 10sec
                    sw ??= Stopwatch.StartNew();
                    if (sw.ElapsedMilliseconds > 10000)
                        throw;
                }
            }

            return decryptJson(json);
        }

        public async Task<bool> WriteJson(string path, Func<string, string> encryptJson, string content)
        {
            Stopwatch sw = null;

            while (true)
            {
                try
                {
                    await File.WriteAllTextAsync(path, encryptJson(content));
                    return true;
                }
                catch (IOException e) when (e.Message.Contains("because it is being used by another process"))
                {
                    // If some other process is using this file, retry operation unless elapsed times is greater than 10sec
                    sw ??= Stopwatch.StartNew();
                    if (sw.ElapsedMilliseconds > 10000)
                        return false;
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }
    }

    public class InMemoryStorage : IStorageAccess
    {
        private string _content = "{}";

        public Task<string> ReadJson(string path, Func<string, string> encryptJson, Func<string, string> decryptJson) => Task.FromResult(_content);

        public Task<bool> WriteJson(string path, Func<string, string> encryptJson, string content)
        {
            _content = content;
            return Task.FromResult(true);
        }
    }
}