using System;
using PCL.Core.Logging;

namespace PCL.Core.App.Essentials;

public sealed partial class TelemetryService
{
    // PCL XH 禁用远程遥测，保留入口以兼容日志服务；异常仍记录在本地日志中。
    public static void ReportException(Exception ex, string plain, LogLevel level)
    {
    }
}
