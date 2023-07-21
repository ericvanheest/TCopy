using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Runtime.InteropServices;

namespace TCopy
{
    class FileComparer
    {
        [DllImport("msvcrt.dll")]
        private static extern int memcmp(byte[] b1, byte[] b2, long count);

        private string m_strFile1;
        private string m_strFile2;
        private long m_iSize1;
        private long m_iSize2;
        private const int bufsize = 32768;

        public FileComparer(string strFile1, string strFile2)
        {
            m_strFile1 = strFile1;
            m_strFile2 = strFile2;
            m_iSize1 = UnicodeAccess.GetFileSize(strFile1);
            m_iSize2 = UnicodeAccess.GetFileSize(strFile2);
        }

        public bool AreFilesIdentical()
        {
            if (m_iSize1 != m_iSize2)
                return false;

            byte[] buf1 = new byte[bufsize];
            byte[] buf2 = new byte[bufsize];

            Stream stream1 = UnicodeAccess.GetReadStream(m_strFile1, false, 0);
            Stream stream2 = UnicodeAccess.GetReadStream(m_strFile2, false, 0);

            try
            {
                int iRead1 = 0;
                int iRead2 = 0;
                do
                {
                    iRead1 = stream1.Read(buf1, 0, bufsize);
                    iRead2 = stream2.Read(buf2, 0, bufsize);

                    if (iRead1 != iRead2)
                        return false;

                    if (memcmp(buf1, buf2, iRead1) != 0)
                        return false;
                }
                while (iRead1 == bufsize && iRead2 == bufsize);
            }
            finally
            {
                stream1.Close();
                stream2.Close();
            }

            return true;
        }

        public bool DoHashValuesMatch()
        {
            string line1;
            string line2;

            TextReader stream1 = new StreamReader(UnicodeAccess.GetReadStream(m_strFile1, false, 0)) as TextReader;
            TextReader stream2 = new StreamReader(UnicodeAccess.GetReadStream(m_strFile2, false, 0)) as TextReader;

            try
            {
                do
                {
                    line1 = stream1.ReadLine();
                    line2 = stream2.ReadLine();

                    if (line1 == line2)
                        continue;

                    if ((line1 == null && line2 != null) || (line1 != null && line2 == null))
                        return false;   // More lines in one file than the other

                    if (line1 == null && line2 == null)
                        break;

                    int iHashEnd1 = line1.IndexOfAny(new char[] { ' ', '\t' });
                    int iHashEnd2 = line2.IndexOfAny(new char[] { ' ', '\t' });

                    if (iHashEnd1 == -1 || iHashEnd2 == -1)
                        return false;   // No hash on this line?

                    string strHash1 = line1.Substring(0, iHashEnd1);
                    string strHash2 = line2.Substring(0, iHashEnd2);

                    if (strHash1 != strHash2)
                        return false;
                }
                while (line1 != null && line2 != null);
            }
            finally
            {
                stream1.Close();
                stream2.Close();
            }

            return true;
        }
    }
}
