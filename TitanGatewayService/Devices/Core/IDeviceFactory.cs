using System;
using System.Collections.Generic;
using System.Text;

namespace TitanGatewayService.Devices.Core
{
    public interface IDeviceFactory
    {
        IDeviceClient Create(DeviceConfig config);
    }

}
