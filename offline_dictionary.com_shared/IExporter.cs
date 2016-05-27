using System;
using System.Threading.Tasks;

namespace offline_dictionary.com_shared
{
    public interface IExporter
    {
        Task ExportAsync();
    }
}
