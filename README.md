# AIS Console Receiver

Small C# console utility for extracting and decoding AIS messages from:

- `pcapng` capture files
- live UDP multicast traffic

Current features:

- finds `!AIVDM` and `!AIVDO` sentences inside capture data
- reassembles multipart AIS payloads
- decodes common AIS message types such as 1, 2, 3, 5, 18, 19, and 24
- prints fields like `MMSI`, position, speed, heading, ship name, call sign, ship type, and destination

## Project Layout

- project file: `AisConsoleReceiver.csproj`
- main program: `Program.cs`
- sample capture: `20260408AIS-DATA-STREAM.pcapng`

## Run

This project currently targets `net8.0`, so you need the .NET 8 SDK/runtime installed to build or run it.

Read the bundled sample capture:

```powershell
dotnet run --project .\AisConsoleReceiver.csproj
```

Read a specific capture file:

```powershell
dotnet run --project .\AisConsoleReceiver.csproj -- --pcap C:\path\to\capture.pcapng
```

Listen for live multicast AIS packets:

```powershell
dotnet run --project .\AisConsoleReceiver.csproj -- --listen --group 239.192.0.4 --port 60004 --local-ip 192.168.1.100
```

Show help:

```powershell
dotnet run --project .\AisConsoleReceiver.csproj -- --help
```
