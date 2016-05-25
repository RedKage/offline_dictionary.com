using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using offline_dictionary.com_shared;
using offline_dictionary.com_shared.Model;

namespace offline_dictionary.com_export_jsondump
{
    public class ExportJsonDump : IExporter
    {
        private readonly string _outputDirPath;
        private readonly GenericDictionary _genericDictionary;

        public ExportJsonDump(GenericDictionary genericDictionary, string outputDirPath)
        {
            _genericDictionary = genericDictionary;
            _outputDirPath = outputDirPath;
        }

        public async Task ExportAsync(IProgress<ExportingProgressInfo> progress)
        {
            string outJsonDumpFilePath = $@"{_outputDirPath}\{_genericDictionary.Name}-{_genericDictionary.Version}.json.gz";

            Task convert = new Task(() =>
            {
                using (FileStream jsonFileStream = new FileStream(outJsonDumpFilePath, FileMode.Create, FileAccess.Write))
                {
                    using (GZipStream zipStream = new GZipStream(jsonFileStream, CompressionLevel.Optimal, false))
                    {
                        using (TextWriter jsonStream = new StreamWriter(zipStream, Encoding.UTF32))
                        {
                            using (JsonTextWriter jsonWriter = new JsonTextWriter(jsonStream))
                            {
                                JsonSerializer serializer = new JsonSerializer();
                                serializer.Serialize(jsonWriter, _genericDictionary, typeof(GenericDictionary));
                            }
                        }
                    }
                }
            });
            convert.Start();
            await convert;
        }
    }
}