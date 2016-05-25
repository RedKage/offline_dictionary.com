using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using offline_dictionary.com_shared;
using offline_dictionary.com_shared.Model;

namespace offline_dictionary.com_reader_jsondump
{
    public class LoadFromJsonDump
    {
        private readonly string _jsonDumpFilePath;
        private readonly int _loadWordsLimit;

        public LoadFromJsonDump(string jsonDumpFilePath, int loadWordsLimit = -1)
        {
            _jsonDumpFilePath = jsonDumpFilePath;
            _loadWordsLimit = loadWordsLimit;
        }

        public async Task<GenericDictionary> LoadAsync(IProgress<LoadingProgressInfo> progress)
        {
            Task<GenericDictionary> convert = new Task<GenericDictionary>(() =>
            {
                using (FileStream jsonFileStream = new FileStream(_jsonDumpFilePath, FileMode.Open, FileAccess.Read))
                {
                    using (GZipStream zipStream = new GZipStream(jsonFileStream, CompressionMode.Decompress, false))
                    {
                        using (TextReader jsonStream = new StreamReader(zipStream, Encoding.UTF32))
                        {
                            using (JsonTextReader jsonReader = new JsonTextReader(jsonStream))
                            {
                                JsonSerializer serializer = new JsonSerializer();
                                return serializer.Deserialize<GenericDictionary>(jsonReader);
                            }
                        }
                    }
                }
            });
            convert.Start();
            return await convert;
        }
    }
}