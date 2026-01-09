using System;
using System.Collections.Generic;
using System.Text;
using TitanGatewayService.Devices;

namespace TitanGatewayService
{
    public interface IDeviceFactory
    {
        IDeviceClient Create(DeviceConfig config);
    }

}
