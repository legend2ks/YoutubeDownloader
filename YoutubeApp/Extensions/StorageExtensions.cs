using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace YoutubeApp.Extensions;

public static class StorageExtensions
{
    public static string GetActualPath(this DirectoryInfo directoryInfo)
    {
        if (directoryInfo.LinkTarget is not null)
        {
            return directoryInfo.LinkTarget;
        }

        var pathSegments = new List<string> { directoryInfo.Name };
        var dirInfo = directoryInfo.Parent;

        while (dirInfo is not null)
        {
            if (dirInfo.LinkTarget is not null)
            {
                pathSegments.Add(dirInfo.LinkTarget);
                var target = Path.Join(pathSegments.AsEnumerable().Reverse().ToArray());
                return target;
            }

            pathSegments.Add(dirInfo.Name);
            dirInfo = dirInfo.Parent;
        }

        return directoryInfo.FullName;
    }
}