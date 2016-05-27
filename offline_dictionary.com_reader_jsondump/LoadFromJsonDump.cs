using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using Newtonsoft.Json;
using offline_dictionary.com_shared.Model;

namespace offline_dictionary.com_reader_jsondump
{
    public class LoadFromJsonDump
    {
        private readonly string _jsonDumpFilePath;

        public static JsonSerializer Serializer => com_export_jsondump.ExportJsonDump.Serializer;

        public LoadFromJsonDump(string jsonDumpFilePath)
        {
            _jsonDumpFilePath = jsonDumpFilePath;
        }

        public async Task<GenericDictionary> LoadAsync()
        {
            Task<GenericDictionary> convert = new Task<GenericDictionary>(() =>
            {
                using (FileStream jsonFileStream = new FileStream(_jsonDumpFilePath, FileMode.Open, FileAccess.Read))
                {
                    using (GZipStream zipStream = new GZipStream(jsonFileStream, CompressionMode.Decompress, false))
                    {
                        using (TextReader jsonStream = new StreamReader(zipStream))
                        {
                            using (JsonTextReader jsonReader = new JsonTextReader(jsonStream))
                            {
                                return Serializer.Deserialize<GenericDictionary>(jsonReader);
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