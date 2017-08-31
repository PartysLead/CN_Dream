using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace CnDream.Core
{
    public class RemoteChannelStation : ChannelStation
    {
        protected override async Task OnCreateFreeChannel()
        {
            await Task.Delay(500);
            throw new NotImplementedException();
        }
    }
}
