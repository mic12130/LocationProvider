using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using Microsoft.FlightSimulator.SimConnect;
using Serilog;

namespace LocationProvider
{
    public class SimConnectManager
    {
        private enum DataDefineId
        {
            Definition1
        }

        private enum DataRequestId
        {
            Request1
        }

        public struct RetrievedData
        {
            public double latitude;
            public double longitude;
        }

        private const int WM_USER_SIMCONNECT = 0x0402;
        private IntPtr hWnd = new IntPtr(0);
        private SimConnect simConnect = null;
        public event DataUpdatedEventHandler DataUpdated;
        public event DataTransmitTerminatedHandler DataTransmitTerminated;

        public bool IsReceiving { get; private set; }

        public SimConnectManager()
        {
        }

        public void SetWindowHandle(IntPtr hWnd)
        {
            this.hWnd = hWnd;
        }

        public int GetUserSimConnectWinEvent()
        {
            return WM_USER_SIMCONNECT;
        }

        public void ReceiveSimConnectMessage()
        {
            simConnect?.ReceiveMessage();
        }

        public void Connect()
        {
            Log.Information("Connect to SimConnect");

            try
            {
                simConnect = new SimConnect("Location Provider", hWnd, WM_USER_SIMCONNECT, null, 0);
                simConnect.OnRecvOpen += new SimConnect.RecvOpenEventHandler(SimConnect_OnRecvOpen);
                simConnect.OnRecvQuit += new SimConnect.RecvQuitEventHandler(SimConnect_OnRecvQuit);
                simConnect.OnRecvException += new SimConnect.RecvExceptionEventHandler(SimConnect_OnRecvException);
                simConnect.OnRecvSimobjectData += new SimConnect.RecvSimobjectDataEventHandler(SimConnect_OnRecvSimobjectData);
                simConnect.AddToDataDefinition(DataDefineId.Definition1, "Plane Latitude", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simConnect.AddToDataDefinition(DataDefineId.Definition1, "Plane Longitude", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simConnect.RegisterDataDefineStruct<RetrievedData>(DataDefineId.Definition1);
                simConnect.RequestDataOnSimObject(DataRequestId.Request1, DataDefineId.Definition1, 0, SIMCONNECT_PERIOD.SECOND, SIMCONNECT_DATA_REQUEST_FLAG.CHANGED, 0, 0, 0);
            }
            catch
            {
                throw;
            }
        }

        public void Disconnect()
        {
            Log.Information("Disconnect from SimConnect");
            IsReceiving = false;

            if (simConnect != null)
            {
                simConnect.Dispose();
                simConnect = null;
            }
        }

        private void SimConnect_OnRecvOpen(SimConnect sender, SIMCONNECT_RECV_OPEN data)
        {
            Log.Information("SimConnect_OnRecvOpen");
            IsReceiving = true;
        }

        private void SimConnect_OnRecvQuit(SimConnect sender, SIMCONNECT_RECV data)
        {
            Log.Information("SimConnect_OnRecvQuit");

            Disconnect();
            DataTransmitTerminated.Invoke();
        }

        private void SimConnect_OnRecvException(SimConnect sender, SIMCONNECT_RECV_EXCEPTION data)
        {
            SIMCONNECT_EXCEPTION eException = (SIMCONNECT_EXCEPTION)data.dwException;
            Log.Warning("SimConnect_OnRecvException: " + eException.ToString());
        }

        private void SimConnect_OnRecvSimobjectData(SimConnect sender, SIMCONNECT_RECV_SIMOBJECT_DATA data)
        {
            Log.Information("SimConnect_OnRecvSimobjectData");

            try
            {
                if ((DataRequestId)data.dwRequestID == DataRequestId.Request1)
                {
                    var receivedData = (RetrievedData)data.dwData[0];
                    DataUpdated.Invoke(receivedData);
                }
            }
            catch (Exception) { }
        }

        public delegate void DataUpdatedEventHandler(RetrievedData data);
        public delegate void DataTransmitTerminatedHandler();
    }
}
