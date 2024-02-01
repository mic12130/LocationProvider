# Location Provider

This is a tool to collect location data from MSFS and pass it to iPhone or iPad. As this tool communicates directly with the simulate location service of iOS/iPadOS, it can implement a system-wide location override on the device.

## Requirements

- An iPhone/iPad with developer mode enabled
- Installed a USB Ethernet driver (to create a tunnel with connected iPhone/iPad)
- WinUI 3-related SDKs
- Python 3

## Build

### WinUI App

> [!NOTE]
> MSIX packaging is enabled by default. If you're unhappy with this, feel free to switch it to the unpackaged way where you can have exe artifact. [Here is how](https://learn.microsoft.com/en-us/windows/apps/winui/winui3/create-your-first-winui3-app#unpackaged-create-a-new-project-for-an-unpackaged-c-or-c-winui-3-desktop-app)

The build of the WinUI app is the same as many other Visual Studio projects - just click build and run then you're ready to go.

### Python Script

Install dependencies:

```shell
pip install -r requirements.txt
```

## Usage

### Before Start
- Connect iPhone/iPad to your PC
- Have your MSFS flight session loaded

### Running
- Start the WinUI app and the Python script
- Press the start button
- Press the stop button when finished your flight
- Close the WinUI app and the Python script

## Behind the Scenes

This project consists of two components:
- A WinUI app to collect MSFS location via SimConnect
- A Python script to communicate with iPhone/iPad

They work together through HTTP. At the start, the WinUI app will send a start command to the Python script, letting it start the location simulate service on iPhone/iPad. Then location data will be passed from MSFS to the device regularly. In the end, everything will stop as soon as the Python script receives a stop command from the WinUI app.
