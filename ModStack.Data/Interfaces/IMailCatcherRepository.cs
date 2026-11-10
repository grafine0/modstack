using System.Collections.Generic;
using System.Threading.Tasks;
using ModStack.Common.Models;

namespace ModStack.Data.Interfaces;

public interface IMailCatcherRepository
{
    Task<IEnumerable<InterceptedEmail>> GetAllAsync();
    Task<InterceptedEmail?> GetByIdAsync(long id);
    Task<long> InsertAsync(InterceptedEmail email);
    Task DeleteAsync(long id);
    Task DeleteAllAsync();
    Task MarkAsReadAsync(long id);
}
