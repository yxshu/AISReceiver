# AIS 控制台接收器

一个小型 C# 控制台工具，用于从以下来源提取并解码 AIS 消息：

- `pcapng` 抓包文件
- 实时 UDP 组播流量

当前功能：

- 从抓包数据中识别 `!AIVDM` 和 `!AIVDO` 语句
- 自动重组多分片 AIS 载荷
- 解码常见 AIS 消息类型，例如 1、2、3、5、18、19 和 24
- 输出 `MMSI`、位置、航速、航向、船名、呼号、船型、目的地等字段

## 项目结构

- 项目文件：`AisConsoleReceiver.csproj`
- 主程序：`Program.cs`
- 示例抓包：`20260408AIS-DATA-STREAM.pcapng`

## 运行要求

本项目当前目标框架为 `net10.0`，请确保已经安装 .NET 10 SDK 或运行时。

## 运行方式

读取项目自带的示例抓包文件：

```powershell
dotnet run --project .\AisConsoleReceiver.csproj
```

读取指定抓包文件：

```powershell
dotnet run --project .\AisConsoleReceiver.csproj -- --pcap C:\path\to\capture.pcapng
```

监听实时 AIS 组播数据：

```powershell
dotnet run --project .\AisConsoleReceiver.csproj -- --listen --group 239.192.0.4 --port 60004 --local-ip 192.168.1.100
```

显示帮助信息：

```powershell
dotnet run --project .\AisConsoleReceiver.csproj -- --help
```
