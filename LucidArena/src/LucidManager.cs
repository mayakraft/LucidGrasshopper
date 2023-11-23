using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LucidArena
{
    class LucidManager
    {
        const UInt32 UPDATE_TIMEOUT = 100;

        public static bool SystemIsOpen = false;
        public static bool DeviceIsConnected = false;
        public static ArenaNET.ISystem system;
        public static List<ArenaNET.IDevice> devices = new List<ArenaNET.IDevice>();

        public static bool OpenSystem()
        {
            Console.WriteLine("OpenSystem");
            try
            {
                system = ArenaNET.Arena.OpenSystem();
                SystemIsOpen = true;
                return true;
            }
            catch (Exception error)
            {
                throw error;
            }
        }

        public static int ConnectAllDevices()
        {
            // Enumerate device
            //    Starting Arena just requires opening the system. From there,
            //    update and grab the device list, and create the device. Notice
            //    that failing to update the device list will return an empty
            //    list, even if devices are connected.
            if (!SystemIsOpen) return 0;

            // ArenaNET.ISystem system = ArenaNET.Arena.OpenSystem();
            system.UpdateDevices(UPDATE_TIMEOUT);
            if (system.Devices.Count == 0) return 0;

            Console.WriteLine("Enumerate device");
            devices = system.Devices.Select(deviceInfo => system.CreateDevice(deviceInfo)).ToList();
            DeviceIsConnected = true;

            devices.ForEach(device =>
            {
                // enable stream auto negotiate packet size
                var streamAutoNegotiatePacketSizeNode = (ArenaNET.IBoolean)device.TLStreamNodeMap.GetNode("StreamAutoNegotiatePacketSize");
                streamAutoNegotiatePacketSizeNode.Value = true;
                // enable stream packet resend
                var streamPacketResendEnableNode = (ArenaNET.IBoolean)device.TLStreamNodeMap.GetNode("StreamPacketResendEnable");
                streamPacketResendEnableNode.Value = true;
            });
            return devices.Count;
        }

        public static void DisconnectDevices()
        {
            devices.ForEach(device => system.DestroyDevice(device));
            devices = new List<ArenaNET.IDevice>();
        }

        // prevent throw.
        // errors will throw if system is already closed. prevent this.
        // only issue is if the system truly had an issue closing, we would be missing that information.
        public static void CloseSystem()
        {
            if (system == null) return;
            try
            {
                if (DeviceIsConnected) DeviceIsConnected = false;
                DisconnectDevices();
                ArenaNET.Arena.CloseSystem(system);
                SystemIsOpen = false;
            }
            catch (Exception)
            {
                return;
            }
        }
    }
}
