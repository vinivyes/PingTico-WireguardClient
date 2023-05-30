using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PingTico_WireguardClient.Utils;

public static class Utils
{
    /// <summary>
    /// Gets the full path of where the application is running from
    /// </summary>
    /// <param name="localFile">The path to a file located in the local application folder</param>
    /// <returns></returns>
    public static string LocalPath(string localFile = null)
    {
        return
            localFile != null ?
            Path.Combine(Path.GetDirectoryName(Environment.ProcessPath) ?? "", localFile) :
            Path.Combine(Path.GetDirectoryName(Environment.ProcessPath) ?? "");
    }
}