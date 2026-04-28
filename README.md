# AIS Console Receiver

一个独立的 C# 控制台项目，用于：

- 读取 `JRC JHS-183` 的 AIS 数据
- 从 `pcapng` 测试文件中提取 `!AIVDM / !AIVDO`
- 自动拼接多段 AIS 报文
- 在控制台输出 `MMSI / 经纬度 / 航速 / 航向 / 船名 / 呼号 / 目的地` 等信息

## 项目位置

`D:\ModifyImageMetadata\AisConsoleReceiver`

## 测试数据

默认测试数据文件：

`D:\20260408AIS-DATA-STREAM.pcapng`

## 运行方式

直接解析默认测试文件：

```powershell
dotnet run --project D:\ModifyImageMetadata\AisConsoleReceiver\AisConsoleReceiver.csproj
```

指定测试文件：

```powershell
dotnet run --project D:\ModifyImageMetadata\AisConsoleReceiver\AisConsoleReceiver.csproj -- --pcap D:\20260408AIS-DATA-STREAM.pcapng
```

实时监听 AIS 组播：

```powershell
dotnet run --project D:\ModifyImageMetadata\AisConsoleReceiver\AisConsoleReceiver.csproj -- --listen --group 239.192.0.4 --port 60004 --local-ip 192.168.1.100
```

查看帮助：

```powershell
dotnet run --project D:\ModifyImageMetadata\AisConsoleReceiver\AisConsoleReceiver.csproj -- --help
```
