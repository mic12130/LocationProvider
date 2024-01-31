import _thread
import asyncio
from contextlib import asynccontextmanager
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from threading import Lock
from typing import AsyncGenerator
from pymobiledevice3.cli.remote import get_device_list
from pymobiledevice3.remote.remote_service_discovery import RemoteServiceDiscoveryService
from pymobiledevice3.remote.core_device_tunnel_service import create_core_device_tunnel_service, TunnelResult
from pymobiledevice3.services.dvt.dvt_secure_socket_proxy import DvtSecureSocketProxyService
from pymobiledevice3.services.dvt.instruments.location_simulation import LocationSimulation
from pyuac import isUserAdmin
from urllib.parse import urlparse, parse_qs

# address and port
device_address = ()

# rsd and location service
device_connection = ()
device_connection_mutex = Lock()


class HTTPRequestHandler(BaseHTTPRequestHandler):
    def log_message(self, format, *args):
        return

    def do_GET(self):
        p = urlparse(self.path)
        path_only = p.path

        if path_only == '/':
            self.server_root()
        elif path_only == '/on':
            self.device_connect()
        elif path_only == '/off':
            self.device_disconnect()
        elif path_only == '/set':
            params = parse_qs(p.query)
            if params.get('lat') != None and params.get('lon') != None:
                lat = params['lat'][0]
                lon = params['lon'][0]
                self.device_set_loc(float(lat), float(lon))
            else:
                print("Bad request: Invalid coordinate")
                self.send_400('BOOM')
        else:
            self.send_404('404')

    def server_root(self):
        self.send('hello world')

    def device_connect(self):
        with device_connection_mutex:
            global device_connection
            if device_connection != ():
                print("Bad request: Device has connected")
                self.send_400('BOOM')
                return

            rsd = RemoteServiceDiscoveryService(
                (device_address[0], device_address[1]))
            rsd.connect()
            dvt = DvtSecureSocketProxyService(rsd)
            dvt.perform_handshake()
            loc = LocationSimulation(dvt)
            device_connection = (rsd, loc)

            print(
                'Do not terminate this process before clicking Stop button in Location Provider.exe.')
            print('DVT service is running...')
            self.send('success')

    def device_disconnect(self):
        with device_connection_mutex:
            global device_connection
            if device_connection == ():
                print("Bad request: No connected device")
                self.send_400('BOOM')
                return

            device_connection[1].clear()
            device_connection[0].service.close()
            device_connection = ()

            print('DVT service stopped. Now you can safely terminate this process.')
            self.send('success')

    def device_set_loc(self, lat: float, lon: float):
        with device_connection_mutex:
            global device_connection
            if device_connection == ():
                print("Bad request: No connected device")
                self.send_400('BOOM')
                return

            device_connection[1].set(lat, lon)
            self.send('success')

    def send(self, msg: str):
        self.send_response(200)
        self.send_header("Content-type", "text/html")
        self.end_headers()
        self.wfile.write(msg.encode())

    def send_400(self, msg: str):
        self.send_response(400)
        self.send_header("Content-type", "text/html")
        self.end_headers()
        self.wfile.write(msg.encode())

    def send_404(self, msg: str):
        self.send_response(404)
        self.send_header("Content-type", "text/html")
        self.end_headers()
        self.wfile.write(msg.encode())


@asynccontextmanager
async def start_tunnel(rsd: RemoteServiceDiscoveryService) -> AsyncGenerator[TunnelResult, None]:
    print('Starting tunnel...')
    with create_core_device_tunnel_service(rsd, autopair=True) as service:
        async with service.start_quic_tunnel() as tunnel_result:
            print('Tunnel started.')
            try:
                yield tunnel_result
            finally:
                print('Shutting down tunnel...')


async def start_tunnel_wrapper(rsd: RemoteServiceDiscoveryService):
    async with start_tunnel(rsd) as tunnel_result:
        global device_address
        device_address = (tunnel_result.address, tunnel_result.port)
        print('UDID:', rsd.udid)
        print('ProductType:', rsd.product_type)
        print('ProductVersion:', rsd.product_version)
        print('Interface:', tunnel_result.interface)
        print('Protocol:', 'quic')
        print('RSD Address:', tunnel_result.address)
        print('RSD Port:', tunnel_result.port)
        await tunnel_result.client.wait_closed()


def select_device() -> RemoteServiceDiscoveryService:
    print('Looking for devices...')
    devices = get_device_list()
    if not devices:
        raise Exception('No device connected')
    if len(devices) == 1:
        print('Found 1 device.')
        return devices[0]
    else:
        raise Exception('Too many devices connected')


def start_server():
    server = ThreadingHTTPServer(('localhost', 12924), HTTPRequestHandler)
    server.serve_forever()


def main():
    if not isUserAdmin():
        # requires when starting tunnel
        print('This script requires being run as an admin.')
        return

    _thread.start_new_thread(start_server, ())
    print('Server is running on http://127.0.0.1:12924')

    device = select_device()
    asyncio.run(start_tunnel_wrapper(device))


if __name__ == "__main__":
    main()
