# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**AIS Console Receiver** is a .NET 10.0 console application that decodes Automatic Identification System (AIS) data from maritime vessels. It supports two modes:
- **File mode**: Parse archived AIS data from pcapng files
- **Listen mode**: Real-time UDP multicast listener for live AIS streams

## Build and Run Commands

```bash
# Restore and build
dotnet build

# Run with default pcapng file (D:\20260408AIS-DATA-STREAM.pcapng)
dotnet run

# Run with custom pcapng file
dotnet run -- --pcap <path>

# Real-time multicast listening
dotnet run -- --listen --group 239.192.0.4 --port 60004 --local-ip 192.168.1.100

# Show help
dotnet run -- --help
```

## Architecture & Key Classes

All code is contained in **Program.cs**. The architecture consists of several nested classes within the static `Program` class:

### Core Pipeline
1. **Options** (line 184-238): Command-line argument parser. Parse with `Options.Parse(args)`.
2. **AisDecoder** (line 240-332): Main decoder that orchestrates the decoding pipeline:
   - Maintains a `MultipartAssembler` to handle multi-part messages
   - `ProcessSentence()` parses NMEA sentences and returns decoded messages
3. **MultipartAssembler** (line 334-401): Handles multi-part AIS messages (sentences split across multiple NMEA frames). 2-minute timeout for incomplete sequences.
4. **NmeaSentence** (line 423-493): Parses NMEA 0183 format (`!AIVDM,` or `!AIVDO,`) with checksum validation.

### Data Models
- **DecodedMessage** (line 405-421): Container for extracted AIS information (MMSI, position, ship name, call sign, speed, course, heading, etc.)
- **AssembledPayload** (line 403): Record holding complete payload and fill-bit count

### Decoding Utilities (lines 495-591)
- **SixBitPayloadToBits()**: Converts armored 6-bit ASCII payload to binary string
- **GetUInt/GetInt**: Extract unsigned/signed integers from bit strings at given offsets
- **GetSixBitText()**: Decode 6-bit ASCII text fields (ship name, call sign, etc.)
- **Decode functions**: Longitude, latitude, speed, course, heading conversion with special case handling (invalid values encoded as specific integers)
- **Lookup tables**: `ShipTypeName()`, `NavigationStatusName()` for code-to-string mapping

### Message Type Handling (Decode method, line 261-322)
Messages 1-3, 5, 18, 19, 24 have dedicated decoding logic with hardcoded bit offsets. Other types show generic message type labels. Bit offsets differ by message type due to variable-length fields.

## Important Details

### NMEA Sentence Format
Standard format: `!AIVDM,<fragment_count>,<fragment_number>,<sequence_id>,<channel>,<payload>,<fill_bits>*<checksum>`
- Checksum is XOR of all characters before `*`, output as 2-digit hex
- Fill-bits indicate padding bits at end of payload (can be removed)
- Multipart sentences use `sequence_id` and `channel` to correlate parts

### 6-Bit Encoding
AIS payloads use a custom 6-bit ASCII encoding (see `SixBitAscii` constant, line 14). Each character in payload represents 6 bits. Conversion:
```
char_value = char_code - 48; if > 40, subtract 8
binary = Convert.ToString(value, 2).PadLeft(6, '0')
```

### Bit Field Extraction
Bit offsets and lengths are hardcoded per message type. Example (Type 1, Position Report):
- Bits 0-5: Message Type (6 bits)
- Bits 8-37: MMSI (30 bits)
- Bits 38-41: Navigation Status (4 bits)
- Bits 61-88: Longitude (28 bits)
- Bits 89-115: Latitude (27 bits)

Invalid coordinate/speed/course values are mapped to specific integers (e.g., longitude = 0x6791AC0 means invalid). See decoder functions near line 543.

### Multi-part Message Assembly
Incomplete multi-part sequences are stored with key `"{talker}|{channel}|{sequence_id}|{fragment_count}"`. Timeout is 2 minutes—expired incomplete sequences are purged before adding new ones.

## Testing

No test project exists yet. To add tests, create a new xUnit project and reference this assembly, or add integration tests that call `dotnet run` with various pcapng files.

## Configuration Files

- **.csproj**: Minimal configuration targeting .NET 10.0 with nullable types enabled
- **.sln**: Standard Visual Studio solution file; can build/debug with VS or `dotnet` CLI

## Known Defaults & Paths

Hardcoded in Program.cs:
- Default pcapng: `D:\20260408AIS-DATA-STREAM.pcapng` (line 10)
- Default multicast: `239.192.0.4:60004` (lines 11-12)
- Default local NIC: `192.168.1.100` (line 13)
- Multipart timeout: 2 minutes (line 242)

These can be overridden via command-line arguments.

## Notes for Future Development

- Output is piped to console in human-readable format (line 99-159); add structured output (JSON/CSV) if needed
- Only message types 1, 2, 3, 5, 18, 19, 24 have custom decoding; others fallback to generic type label (line 316)
- Error handling is minimal; only high-level try-catch at Main() entry point (line 25-43)
- No logging framework; uses `Console.WriteLine` and `Console.Error.WriteLine`
