using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace CnDream.Core
{
    public class LocalChannelStation : ChannelStation
    {
        protected override Task OnCreateFreeChannel()
        {
            throw new NotImplementedException();
        }
    }
}
