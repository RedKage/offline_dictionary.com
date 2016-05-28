using System.Threading.Tasks;
using offline_dictionary.com_shared.Model;

namespace offline_dictionary.com_shared
{
    public interface IReader
    {
        Task<GenericDictionary> LoadAsync();
    }
}
