using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Messenger.Services.Interfaces.External
{
    public interface IExternalTokenProvider
    {
        Task<string> GetTokenAsync();
    }

}
