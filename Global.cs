using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace TCopy
{
    public class Global
    {
        public static string PathCombineNearestDir(string strSource, string strTarget)
        {
            while (!Directory.Exists(strSource))
            {
                if (strSource.IndexOf('\\') == -1)
                    return Path.Combine(strSource, strTarget);
                string strNewDir = UnicodeAccess.GetDirectoryName(strSource);
                if (strNewDir == strSource)
                    break;

                strSource = strNewDir;
            }

            return Path.Combine(strSource, strTarget);
        }
    }
}
