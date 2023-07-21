using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Runtime.InteropServices;
using System.Collections;
using System.Management;
using System.Net;

namespace TCopy
{
    public enum ErrorType { Read, Write, Overwrite, Size, Other }
    public enum ChecksumType { None, MD5, SHA1, SHA256 }

    public class TCopyInfo
    {
        public int iDelay;
        public int iSpeedLimit;
        public int iBlockSize;
        public Source[] sSources;
        public bool bNoHead;
        public bool bRecursive;
        public bool bWriteRecoveryFile;
        public bool bCreateTarget;
        public bool bOverwrite;
        public bool bCalculateSizes;
        public string sExecuteBeforeVerify;
        public string sMD5File;
        public string sSHAFile;
        public string sSHAFile256;
        public string sVerifyFile;
        public string sHashFile;
        public bool bIgnoreReadErrors;
        public bool bIgnoreSizeMismatches;
        public bool bIgnoreWriteErrors;
        public bool bNormalizeHashFile;
        public bool bCopyAttributes;
        public bool bZeroUnreadableSectors;
        public bool bISOMode;
        public bool bWaitForMedia;
        public bool bCopyFiles;
        public bool bCopyAnnoyingFiles;
        public bool bThreaded;
        public int iNumBuffers;
        public bool bReverify;
        public bool bResume;
        public bool bRetryErrorsContinuously;
        public List<Destination> Destinations;
        public ArrayList IgnoreForVerify;
        public Dictionary<string,bool> ExclusionsSpecific;
        public ArrayList ExclusionsGeneric;
        public long iExcludedSource;
        public long iExcludedTarget;
        public string sErrorFile;
        public DirectoryMonitor monitor;
        public bool bIsURL;
        public bool bIgnoreDelayForVerify;
        public ChecksumType csType;
        public string csString;
        public string sNormalizeConstant;
        public int m_iTruncateBytes;
        public string sDeleteDir;
        public uint fExcludeAttributes;
        public bool bCopyACLs;
        public bool bCopyOwners;

        public TCopyInfo()
        {
            sSources = null;
            bRecursive = false;
            bNoHead = false;
            bWriteRecoveryFile = false;
            bOverwrite = false;
            bCreateTarget = false;
            bCalculateSizes = true;
            bReverify = false;
            sMD5File = "";
            sSHAFile = "";
            sSHAFile256 = "";
            sVerifyFile = "";
            bIgnoreSizeMismatches = false;
            bIgnoreReadErrors = false;
            bIgnoreWriteErrors = false;
            bWaitForMedia = false;
            bCopyFiles = true;
            bThreaded = false;
            iNumBuffers = 5;
            bRetryErrorsContinuously = false;
            bNormalizeHashFile = false;
            bCopyAttributes = true;
            bZeroUnreadableSectors = false;
            bISOMode = false;
            bCopyAnnoyingFiles = false;
            Destinations = new List<Destination>();
            IgnoreForVerify = new ArrayList();
            ExclusionsSpecific = new Dictionary<string, bool>();
            ExclusionsGeneric = new ArrayList();
            iExcludedSource = 0;
            iExcludedTarget = 0;
            m_iTruncateBytes = Program.m_iDefaultTruncate;
            sErrorFile = "";
            monitor = new DirectoryMonitor();
            bIsURL = false;
            iSpeedLimit = 0;
            bIgnoreDelayForVerify = false;
            csType = ChecksumType.None;
            csString = "";
            sDeleteDir = "";
            sExecuteBeforeVerify = "";
            fExcludeAttributes = 0;
            bCopyACLs = false;
            bCopyOwners = false;
        }

        public void AddExclusions(string str)
        {
            foreach (string strFile in str.Split(new char[] { ':' }))
            {
                if (UnicodeAccess.ContainsWildcards(strFile))
                    ExclusionsGeneric.Add(strFile.ToLower());
                else
                    ExclusionsSpecific.Add(strFile.ToLower(), true);
            }
        }

        public void GenerateNormalizationConstant()
        {
            sNormalizeConstant = null;
            bool bTruncated = false;

            foreach (Source s in sSources)
            {
                if (sNormalizeConstant == null)
                    sNormalizeConstant = s.Path;
                else
                {
                    StringBuilder sb = new StringBuilder(sNormalizeConstant.Length);
                    for (int i = 0; i < sNormalizeConstant.Length; i++)
                    {
                        if (i >= s.Path.Length)
                            break;
                        if (sNormalizeConstant[i] == s.Path[i])
                            sb.Append(s.Path[i]);
                        else
                        {
                            // Back up to the last non-filename character
                            sNormalizeConstant = sb.ToString();
                            int iIndex = sNormalizeConstant.LastIndexOfAny(Path.GetInvalidFileNameChars());
                            if (iIndex != -1)
                            {
                                sNormalizeConstant = sNormalizeConstant.Substring(0, iIndex);
                                bTruncated = true;
                                break;
                            }
                        }
                    }
                    if (!bTruncated)
                        sNormalizeConstant = sb.ToString();
                }
            }

            if (sNormalizeConstant == null)
                sNormalizeConstant = "";
        }
    }

    public class WriterInfo
    {
        public BinaryWriter Writer;
        public string Path;

        public WriterInfo()
        {
            Writer = null;
            Path = "";
        }
    }

    public enum PrintMode
    {
        None,
        CR,
        CRLF
    }

    public class Destination
    {
        public Destination(string sPath)
        {
            Path = UnicodeAccess.FullPathName(sPath.Replace("\r", "").Replace("\n", ""));
            OrigPath = Path;
            IsDirectory = UnicodeAccess.DirectoryExists(Path);
        }

        public string FullPath(string sFile, TCopyInfo info)
        {
            if (!IsDirectory)
                return Path;
            if (info.bIsURL)
                return System.IO.Path.Combine(Path, System.IO.Path.GetFileName(sFile));

            return System.IO.Path.Combine(Path, sFile);
        }

        public bool IsDirectory;
        public string Path;
        public string OrigPath;
    }

    public enum BufferState { Empty, Read, Written };

    public class CopyBuffer
    {
        public byte[] data;
        public int length;
        public BufferState state;

        public CopyBuffer()
        {
            data = null;
            length = 0;
            state = BufferState.Empty;
        }

        public CopyBuffer(int InitialSize)
        {
            data = new byte[InitialSize];
            length = 0;
            state = BufferState.Empty;
        }
    }

    public class ThrottleCopier
    {
        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(int vKey);

        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();

        public TCopyInfo m_info = new TCopyInfo();

        long m_iSourceDirectorySize = 0;
        long m_iBytesProcessed;
        long m_iBytesTransferred;
        bool m_bNeverOverwrite = false;
        bool m_bAbortCopy = false;
        long m_iLastBytes = 0;
        DateTime m_dtLastBPSStart = DateTime.Now;

        MD5CryptoServiceProvider m_md5 = new MD5CryptoServiceProvider();
        SHA1CryptoServiceProvider m_sha1 = new SHA1CryptoServiceProvider();
        SHA256Managed m_sha256 = new SHA256Managed();
        TimeSpan m_tsETR = new TimeSpan(0);
        StreamWriter m_md5Writer = null;
        StreamWriter m_sha1Writer = null;
        StreamWriter m_sha256Writer = null;

        private EventWaitHandle whDataWritten = new EventWaitHandle(false, EventResetMode.AutoReset);
        private EventWaitHandle whDataRead = new EventWaitHandle(false, EventResetMode.AutoReset);

        private BinaryReader m_reader = null;
        private ArrayList m_writers = null;

        private bool m_bReadFinished = true;
        private bool m_bWriteFinished = true;

        private int m_bufferSize = 8192;

        private CopyBuffer[] m_copyBuffers = null;

        private IOException m_exRead = null;
        private IOException m_exWrite = null;

        private long m_lCurrent = 0;
        private long m_iSourceSize = 0;
        private string m_sDestPath = "";
        private int m_iRead = 0;
        private string m_sSourcePath = "";
        private double m_fLastBPS = 0.0;
        private string m_sLastHashFile = "";

        private DateTime m_dtLastRead;
        public bool m_bVerifyPass;

        public void ResetETR()
        {
            m_tsETR = new TimeSpan(0);
        }

        public static string StripBeeps(string str)
        {
            return str.Replace("\x7", "");
        }

        public string ConsoleTruncate(string str)
        {
            int iWidth = Console.WindowWidth;
            if (str.Length < iWidth - 1)
                return str;
            return str.Substring(0, iWidth - 1);
        }

        public string chomp(string s)
        {
            if (s.EndsWith("\r\n"))
                return s.Substring(0, s.Length - 2);
            if (s.EndsWith("\n"))
                return s.Substring(0, s.Length - 1);
            return s;
        }

        public string GetLastLine(string sFile)
        {
            StreamReader reader = new StreamReader(UnicodeAccess.GetReadStream(sFile, false, 0));
            if (reader.BaseStream.Length > 4096)
                reader.BaseStream.Seek(-4096, SeekOrigin.End);
            string str = reader.ReadLine();
            string strOut = "";
            while (str != null)
            {
                int iIndex = str.IndexOf('*');
                if (iIndex != -1)
                    str = str.Substring(iIndex + 1);
                strOut = str;
                str = reader.ReadLine();
            }

            reader.Close();

            return strOut;
        }

        public long GetDirectorySize(string sDir, string sWildcard, bool bRetry)
        {
            if (!m_info.bCalculateSizes)
                return 0;

            if (m_info.bISOMode)
            {
                if (sDir.Length < 2)
                    return 0;
                if (sDir[1] != ':')
                    return 0;

                ManagementObject diskSize = new ManagementObject("win32_logicaldisk.deviceid=\"" + sDir.Substring(0, 2) + "\"");
                diskSize.Get();
                return Convert.ToInt64(diskSize["size"]);
            }

            if (Console.KeyAvailable)
            {
                ConsoleKeyInfo keyInfo = Console.ReadKey(true);
                if ((keyInfo.Key == ConsoleKey.K) && ((keyInfo.Modifiers & ConsoleModifiers.Control) > 0))
                {
                    m_info.bCalculateSizes = false;
                    return 0;
                }
            }

            long iSize = 0;
            bool bFirst = true;

            foreach (string sFile in UnicodeAccess.DirectoryGetFiles(sDir, sWildcard))
            {
                if (CheckExcluded(sDir, sFile, PrintMode.CR))
                    continue;

                if (m_info.fExcludeAttributes != 0)
                {
                    uint attributes = UnicodeAccess.GetFileAttributes(sFile);
                    if ((attributes & m_info.fExcludeAttributes) != 0)
                        continue;
                }

                if (bFirst)
                {
                    if (bRetry)
                        Console.WriteLine();
                    string strConsole = String.Format("{{0,-{0}}}", Console.WindowWidth);
                    Console.Write(strConsole, "\r" + ConsoleTruncate("Dir: " + StripBeeps(Path.GetFileName(sDir))));
                    Console.Write("\r");
                    bFirst = false;
                }

                bool bSkip = false;

                if (!m_info.bCopyFiles)
                {
                    foreach (string s in m_info.IgnoreForVerify)
                    {
                        if (s.EndsWith(sFile))
                        {
                            bSkip = true;
                            break;
                        }
                    }
                }

                if (bSkip)
                    continue;

                iSize += UnicodeAccess.GetFileSize(sFile, m_info.bNoHead);
            }

            if (!m_info.bRecursive)
                return iSize;

            foreach (string sSubdir in UnicodeAccess.DirectoryGetDirectories(sDir))
            {
                if (CheckExcluded(sDir, sSubdir, PrintMode.CR))
                    continue;

                if (m_info.fExcludeAttributes != 0)
                {
                    uint attributes = UnicodeAccess.GetFileAttributes(sSubdir);
                    if ((attributes & m_info.fExcludeAttributes) != 0)
                        continue;
                }

                try
                {
                    iSize += GetDirectorySize(sSubdir, sWildcard, false);
                }
                catch (Exception)
                {
                    // It doesn't matter if we can't find any matching files in subdirectories
                }
            }

            return iSize;
        }

        public void StartCopy(bool bVerifyPass)
        {
            // Console.WriteLine("Source: {0}\nDestination: {1}\nBlocksize: {2} kb\nDelay: {2} ms\n", m_sSource, m_sDest, m_iBlockSize, m_iDelay);

            bool bReady = true;
            m_bVerifyPass = bVerifyPass;
            m_iBytesProcessed = 0;
            m_iBytesTransferred = 0;
            m_bAbortCopy = false;
            m_iSourceDirectorySize = 0;

            if (m_info.bCalculateSizes && !m_info.bIsURL)
                Console.WriteLine("Calculating directory sizes.  Press Control+K to skip.");

            foreach (Source sSource in m_info.sSources)
            {
                string sDrive = sSource.Path.Substring(0, 1).ToUpper();
                if ((sDrive[0] >= 'A') && (sDrive[0] <= 'Z'))
                {
                    if (sSource.Path[1] == ':')
                        bReady = new DriveInfo(sDrive).IsReady;
                }
                if (!bReady && m_info.bWaitForMedia)
                {
                    Console.Write("Waiting for media..");
                    while (!bReady)
                    {
                        System.Threading.Thread.Sleep(500);
                        Console.Write(".");
                        bReady = new DriveInfo(sDrive).IsReady;
                    }
                }

                if (UnicodeAccess.DirectoryExists(sSource.Path))
                {
                    sSource.SourceDir = sSource.Path;
                    sSource.Wildcard = "*.*";
                }
                else if (!UnicodeAccess.IsURL(sSource.Path))
                {
                    sSource.SourceDir = UnicodeAccess.GetDirectoryName(sSource.Path);
                    int iLast = sSource.Path.LastIndexOf(Path.DirectorySeparatorChar);
                    if (iLast == -1)
                        sSource.Wildcard = sSource.Path;
                    else
                        sSource.Wildcard = sSource.Path.Substring(iLast + 1);

                    if (sSource.SourceDir == "")
                        sSource.SourceDir = ".";

                    if (sSource.SourceDir == "" || sSource.SourceDir == null)
                    {
                        Console.WriteLine("ERROR!  Could not locate source directory {0}", sSource.Path);
                        return;
                    }
                }
                else
                {
                    sSource.SourceDir = "";
                    sSource.Wildcard = sSource.Path;
                    m_info.bIsURL = true;
                }

                //Console.WriteLine("Directory: {0}\nWildcard: {1}", sSourceDir, sWildcard);

                long iSourceDirectorySize = 0;

                if (m_info.bCalculateSizes && !m_info.bIsURL)
                {
                    bool bRetry = false;
                    do
                    {
                        if (m_info.fExcludeAttributes != 0)
                        {
                            uint attributes = UnicodeAccess.GetFileAttributes(sSource.Path);
                            if ((attributes & m_info.fExcludeAttributes) != 0)
                                break;
                        }
                        try
                        {
                            //                        if (bRetry)
                            //                            Console.WriteLine();
                            if (UnicodeAccess.DirectoryExists(sSource.Path))
                                iSourceDirectorySize = GetDirectorySize(sSource.Path, sSource.Wildcard, bRetry);
                            else if (UnicodeAccess.FileExists(sSource.Path))
                                iSourceDirectorySize = UnicodeAccess.GetFileSize(sSource.Path, m_info.bNoHead);
                            else
                                iSourceDirectorySize = GetDirectorySize(sSource.SourceDir, sSource.Wildcard, bRetry);
                            if (!m_info.bCalculateSizes)
                                iSourceDirectorySize = 0;
                            bRetry = false;
                        }
                        catch (IOException ex)
                        {
                            if (m_info.bWaitForMedia && !bRetry)
                            {
                                Console.Write("Waiting for media...");
                                bRetry = true;
                            }

                            if (!bRetry)
                            {
                                Console.WriteLine("Could not get source size: " + chomp(ex.Message));
                                Console.Write("Wait until device is ready? (y/n) ");
                                string sInput = Console.ReadLine() + " ";
                                switch (Char.ToLower(sInput[0]))
                                {
                                    case 'n':
                                        return;
                                    default:
                                        bRetry = true;
                                        Console.Write("Waiting...");
                                        break;
                                }
                            }
                        }
                        if (bRetry)
                        {
                            System.Threading.Thread.Sleep(500);
                            Console.Write(".");
                        }
                    } while (bRetry);
                }
                else
                    iSourceDirectorySize = 0;

                if (m_info.bRecursive || bVerifyPass)
                {
                    if (UnicodeAccess.DirectoryExists(sSource.Path))
                        m_info.monitor.Start(sSource.Path, bVerifyPass);
                }

                m_iSourceDirectorySize += iSourceDirectorySize;
            }

            if (m_info.bCalculateSizes && !m_info.bIsURL)
                Console.WriteLine();

            if (m_info.bCopyFiles)
            {
                foreach (Destination dest in m_info.Destinations)
                {
                    long iAvailableFreeSpace = (long) UnicodeAccess.GetDirectoryFreeSpace(dest.Path);
                    if (m_iSourceDirectorySize > iAvailableFreeSpace)
                    {
                        Console.WriteLine("WARNING:  The free disk space on the destination drive ({0}) appears to be less than the source total ({1}).", HRBytes(iAvailableFreeSpace), HRBytes(m_iSourceDirectorySize));
                        Console.Write("Start the copy anyway?  (y)es/(n)o -> ");
                        string sInput = Console.ReadLine();
                        if (char.ToLower(sInput[0]) != 'y')
                            Environment.Exit(0);
                    }
                }
            }

            foreach (Destination dest in m_info.Destinations)
            {
                if (dest.IsDirectory)
                {
                    if (!UnicodeAccess.DirectoryExists(dest.Path))
                    {
                        if (m_info.bCreateTarget)
                        {
                            Console.WriteLine("Creating target directory: " + Path.GetFileName(dest.Path));
                            UnicodeAccess.DirectoryCreateDirectory(dest.Path);
                        }
                        else
                            AskCreateDirectory(dest.Path);
                    }

                    foreach (Source sSource in m_info.sSources)
                    {
                        if (m_info.bCreateTarget && m_info.bCopyFiles && !UnicodeAccess.FileExists(sSource.Path))
                        {
                            if (UnicodeAccess.DirectoryExists(sSource.SourceDir) && dest.IsDirectory)
                            {
                                string sSourceDirOnly = Path.GetFileName(sSource.SourceDir);
                                string sCombine = Path.Combine(dest.Path, sSourceDirOnly);
                                UnicodeAccess.DirectoryCreateDirectory(sCombine);
                            }
                        }
                    }
                }
            }

            bool bRetryHash = false;
            bool bAttemptedCreate = false;

            do
            {
                bRetryHash = false;
                try
                {
                    if (m_info.sMD5File != "")
                        if (m_info.bResume && File.Exists(m_info.sMD5File))
                        {
                            m_sLastHashFile = GetLastLine(m_info.sMD5File);
                            m_md5Writer = new StreamWriter(UnicodeAccess.GetAppendStream(m_info.sMD5File));
                        }
                        else
                            m_md5Writer = new StreamWriter(UnicodeAccess.GetWriteStream(m_info.sMD5File));

                    if (m_info.sSHAFile != "")
                        if (m_info.bResume && File.Exists(m_info.sSHAFile))
                        {
                            m_sLastHashFile = GetLastLine(m_info.sSHAFile);
                            m_sha1Writer = new StreamWriter(UnicodeAccess.GetAppendStream(m_info.sSHAFile));
                        }
                        else
                            m_sha1Writer = new StreamWriter(UnicodeAccess.GetWriteStream(m_info.sSHAFile));

                    if (m_info.sSHAFile256 != "")
                        if (m_info.bResume && File.Exists(m_info.sSHAFile256))
                        {
                            m_sLastHashFile = GetLastLine(m_info.sSHAFile256);
                            m_sha256Writer = new StreamWriter(UnicodeAccess.GetAppendStream(m_info.sSHAFile256));
                        }
                        else
                            m_sha256Writer = new StreamWriter(UnicodeAccess.GetWriteStream(m_info.sSHAFile256));
                }
                catch (Exception ex)
                {
                    if (ex is DirectoryNotFoundException && m_info.bCreateTarget && !bAttemptedCreate)
                    {
                        UnicodeAccess.DirectoryCreateDirectory(UnicodeAccess.GetDirectoryName(m_info.sMD5File));
                        bRetryHash = true;
                        bAttemptedCreate = true;
                    }
                    else
                    {
                        Console.WriteLine("Unable to create hash file: {0}", chomp(ex.Message));
                        Console.Write("Retry? (y/n) ");
                        string sInput = Console.ReadLine() + " ";
                        switch (Char.ToLower(sInput[0]))
                        {
                            case 'n':
                                return;
                            default:
                                bRetryHash = true;
                                break;
                        }
                    }
                }
            } while (bRetryHash);

            m_bufferSize = m_info.iBlockSize * 1024;

            m_copyBuffers = new CopyBuffer[m_info.iNumBuffers];
            for (int i = 0; i < m_info.iNumBuffers; i++)
            {
                m_copyBuffers[i] = new CopyBuffer(m_bufferSize);
            }

            m_writers = new ArrayList();
            for (int i = 0; i < m_info.Destinations.Count; i++)
            {
                WriterInfo wi = new WriterInfo();
                m_writers.Add(wi);
            }

            DateTime dtStart = DateTime.Now;
            m_dtLastBPSStart = dtStart;
            m_iLastBytes = 0;

            foreach (Source sSource in m_info.sSources)
            {
                foreach(Destination dest in m_info.Destinations)
                    dest.Path = dest.OrigPath;

                if (m_info.bISOMode || m_info.bIsURL)
                    CopyFile(sSource.SourceDir, sSource.Wildcard, m_info.Destinations);
                else if (((UnicodeAccess.ContainsWildcards(sSource.Wildcard)) || (UnicodeAccess.DirectoryExists(Path.Combine(sSource.SourceDir, sSource.Wildcard)))))
                {
                    if (m_info.bCreateTarget && m_info.bCopyFiles)
                    {
                        foreach (Destination dest in m_info.Destinations)
                        {
                            dest.Path = Path.Combine(dest.Path, Path.GetFileName(sSource.SourceDir));
                            UnicodeAccess.DirectoryCreateDirectory(dest.Path);
                        }
                    }

                    CopyDirectory(sSource.SourceDir, m_info.Destinations, sSource.Wildcard);
                }
                else
                    CopyFile(sSource.SourceDir, sSource.Wildcard, m_info.Destinations);
            }

            TimeSpan ts = DateTime.Now - dtStart;

            if (m_md5Writer != null)
                m_md5Writer.Close();

            if (m_sha1Writer != null)
                m_sha1Writer.Close();

            if (m_sha256Writer != null)
                m_sha256Writer.Close();

            if (ts.TotalMilliseconds > 100)
                Console.WriteLine("{0} {1} bytes in {2}:{3:D2} ({4}/sec).", m_info.bCopyFiles ? "Copied" : "Processed", m_iBytesTransferred, (int)ts.TotalMinutes, ts.Seconds, HRBytes((long)(m_iBytesTransferred / ts.TotalSeconds), 3));
            else
                Console.WriteLine("{0} {1} bytes.", m_info.bCopyFiles ? "Copied" : "Processed", m_iBytesTransferred);

            if (m_sLastHashFile != "")
            {
                Console.WriteLine("WARNING:  The last hash file was not found during reprocessing; no hashes were\ncalculated: " + StripBeeps(m_sLastHashFile));
            }
        }

        private List<Destination> CombinePaths(List<Destination> dirs, string sNew)
        {
            List<Destination> newArray = new List<Destination>();
            foreach (Destination dest in dirs)
            {
                newArray.Add(new Destination(Path.Combine(dest.Path, sNew)));
            }
            return newArray;
        }

        private bool EndsWithAny(string sTest, string sPrefix, List<Destination> array)
        {
            foreach (Destination dest in array)
            {
                if (sTest.EndsWith(sPrefix + dest.Path))
                    return true;
            }
            return false;
        }

        private bool CheckExcluded(string strDir, string strFile, PrintMode mode)
        {
            if (m_info.bIsURL)
                return false;

            bool bContainsWildcards = UnicodeAccess.ContainsWildcards(strFile);
            string strPrintFile = null;
            string strPrintFormat = String.Format("{{0,-{0}}}", Console.WindowWidth - 1);

            if (strFile == null || strFile == "")
            {
                strFile = Path.GetFileName(strDir);
                strDir = UnicodeAccess.GetDirectoryName(strFile);
            }

            if (!bContainsWildcards)
            {
                foreach (string sSearchWildcard in m_info.ExclusionsGeneric)
                {
                    string[] strFiles = Directory.GetFiles(strDir, sSearchWildcard);
                    foreach (string strExistingFile in strFiles)
                    {
                        if (Path.GetFileName(strExistingFile) == Path.GetFileName(strFile))
                        {
                            strPrintFile = strExistingFile;
                            break;
                        }
                    }
                }
            }

            if (strPrintFile == null)
            {
                string strCompare = bContainsWildcards ? Path.GetFileName(strDir) : Path.GetFileName(strFile);
                if (m_info.ExclusionsSpecific.ContainsKey(strCompare.ToLower()))
                    strPrintFile = strCompare;
            }

            if (strPrintFile != null)
            {
                switch (mode)
                {
                    case PrintMode.None:
                        break;
                    case PrintMode.CR:
                        Console.Write("\r" + strPrintFormat, "Excluding: " + StripBeeps(Path.GetFileName(strPrintFile)));
                        break;
                    case PrintMode.CRLF:
                        Console.WriteLine(strPrintFormat, "Excluding: " + StripBeeps(Path.GetFileName(strPrintFile)));
                        break;
                }
                return true;
            }

            return false;
        }

        private bool AskCreateDirectory(string strDir)
        {
            Console.Write("Destination directory \"{0}\" does not exist.\nWould you like to create it? (y)es (n)o -> ", Path.GetFileName(strDir));
            string sInput = Console.ReadLine() + " ";
            switch (Char.ToLower(sInput[0]))
            {
                case 'n':
                    return false;
                default:
                    UnicodeAccess.DirectoryCreateDirectory(strDir);
                    break;
            }
            return true;
        }

        private void CopyDirectory(string sSource, List<Destination> Destinations, string sWildcard)
        {
            if ((sSource.EndsWith("\\System Volume Information")) && (!m_info.bCopyAnnoyingFiles))
                return;
            else if ((sSource.EndsWith("\\lost+found")) && (!m_info.bCopyAnnoyingFiles))
                return;
            else if ((sSource.EndsWith("\\RECYCLER")) && (!m_info.bCopyAnnoyingFiles))
                return;

            if (CheckExcluded(sSource, sWildcard, PrintMode.CRLF))
                return;

            if (m_info.fExcludeAttributes != 0)
            {
                if ((UnicodeAccess.GetFileAttributes(sSource) & m_info.fExcludeAttributes) != 0)
                    return;
            }
            bool bRetry = false;
            bool bCopySub = m_info.bRecursive;
            if (bCopySub)
            {
                bool bCreated = false;
                foreach (Destination dest in Destinations)
                {
                    string sDest = dest.Path;
                    if (!dest.IsDirectory && m_info.bCopyFiles)
                    {
                        string sNewDirectory = null;

                        do
                        {
                            bRetry = false;
                            try
                            {
                                sNewDirectory = UnicodeAccess.DirectoryCreateDirectory(sDest, m_info.bCopyACLs ? sSource : null, m_info.bCopyOwners ? sSource : null);
                            }
                            catch (Exception ex)
                            {
                                if (m_info.sErrorFile != "")
                                {
                                    string strError = "Could not set ACLs/Ownership for directory \"" + sDest + "\" - " + chomp(ex.Message);
                                    Console.WriteLine(strError);
                                    LogError(ErrorType.Read, strError);
                                    bRetry = false;
                                }
                                else
                                {
                                    Console.Write("\nError setting ACLs/Ownership for directory {0}: {1}\nRetry? (y)es/(n)o -> ", StripBeeps(sDest), chomp(ex.Message));
                                    string sRetry = Console.ReadLine();
                                    bRetry = sRetry.ToLower().StartsWith("y");
                                }
                            }
                        } while (bRetry);

                        if (sNewDirectory != null)
                            sDest = sNewDirectory;
                        bCreated = true;
                        dest.IsDirectory = true;
                    }
                }

                string[] directories = null;
                bRetry = false;

                do
                {
                    try
                    {
                        directories = UnicodeAccess.DirectoryGetDirectories(sSource);
                    }
                    catch (Exception ex)
                    {
                        if (m_info.sErrorFile != "")
                        {
                            string strError = "Could not read directory \"" + sSource + "\" - " + chomp(ex.Message);
                            Console.WriteLine(strError);
                            LogError(ErrorType.Read, strError);
                            bRetry = false;
                        }
                        else
                        {
                            Console.Write("\nError reading directory {0}: {1}\nRetry? (y)es/(n)o -> ", StripBeeps(sSource), chomp(ex.Message));
                            string sRetry = Console.ReadLine();
                            bRetry = sRetry.ToLower().StartsWith("y");
                        }
                        if (!bRetry)
                            directories = null;
                    }
                } while (bRetry);

                if (directories != null)
                {
                    foreach (string sDir in directories)
                    {
                        if (sDir != "." && sDir != ".." && !(bCreated && EndsWithAny(sDir, "\\", Destinations)))
                            CopyDirectory(sDir, CombinePaths(Destinations, Path.GetFileName(sDir)), sWildcard);
                    }
                }
                else
                    return;
            }

            if (bCopySub || UnicodeAccess.ContainsWildcards(sWildcard))
            {
                string[] files = null;
                bRetry = false;

                do
                {
                    try
                    {
                        files = UnicodeAccess.GetFiles(sSource, sWildcard, UnicodeAccess.FileTypes.Files);
                    }
                    catch (Exception ex)
                    {
                        if (m_info.sErrorFile != "")
                        {
                            string strError = String.Format("Could not read path {0}\\{1}: {2}", sSource, sWildcard, chomp(ex.Message));
                            Console.WriteLine(strError);
                            LogError(ErrorType.Read, strError);
                            bRetry = false;
                        }
                        else
                        {
                            Console.Write("\nError reading path {0}\\{1}: {2}\nRetry? (y)es/(n)o -> ", StripBeeps(sSource), StripBeeps(sWildcard), chomp(ex.Message));
                            string sRetry = Console.ReadLine();
                            bRetry = sRetry.ToLower().StartsWith("y");
                        }
                        if (!bRetry)
                            files = null;
                    }
                } while (bRetry);

                if (files != null)
                {
                    foreach (string sFile in files)
                    {
                        CopyFile(sSource, Path.GetFileName(sFile), Destinations);
                        if (m_bAbortCopy)
                            return;
                    }
                }
            }
        }

        private void ThreadReader()
        {
            int iBytesIndex = 0;

            while (!m_bReadFinished)
            {
                bool bRetryRead = false;

                if (m_copyBuffers[iBytesIndex] == null)
                    m_copyBuffers[iBytesIndex] = new CopyBuffer();

                while (m_copyBuffers[iBytesIndex].state == BufferState.Read)
                {
                    if (!m_bWriteFinished)
                        whDataWritten.WaitOne();
                }

                ReadBuffer(iBytesIndex);

                if (m_exRead != null)
                {
                    bRetryRead = HandleReadException(iBytesIndex);
                    if (!bRetryRead)
                    {
                        m_bReadFinished = true;
                        m_bWriteFinished = true;
                        break;
                    }
                }

                ProcessReadBuffer(iBytesIndex);

                if (!bRetryRead)
                {
                    m_copyBuffers[iBytesIndex].state = BufferState.Read;
                    whDataRead.Set();

                    if (m_iRead == 0)
                    {
                        m_bReadFinished = true;
                        break;
                    }

                    iBytesIndex++;

                    if (iBytesIndex >= m_info.iNumBuffers)
                        iBytesIndex = 0;
                }
            }

            m_reader.Close();
            if (m_reader.BaseStream != null)
                m_reader.BaseStream.Dispose();
            m_reader = null;

            FinalizeHashes(m_copyBuffers[iBytesIndex]);

            whDataRead.Set();
        }

        private string NormalizePath(string sPath)
        {
            string sResult;
            if (sPath.StartsWith(".\\"))
                sResult = sPath.Substring(2);
            else
                sResult = sPath;

            if (m_info.bNormalizeHashFile)
            {
                if (sResult.StartsWith(m_info.sNormalizeConstant))
                {
                    if (sResult.Length == m_info.sNormalizeConstant.Length)
                    {
                        // Don't show a blank filename; that's always incorrect
                        sResult = Path.GetFileName(sResult);
                    }
                    else
                        sResult = sResult.Substring(m_info.sNormalizeConstant.Length);
                }
                if (sResult.StartsWith("\\"))
                    sResult = sResult.Substring(1);
            }
            return sResult;
        }

        private void FinalizeHashes(CopyBuffer buf)
        {
            if (m_md5Writer != null)
            {
                m_md5.TransformFinalBlock(buf.data, 0, 0);
                string sPath = NormalizePath(m_sSourcePath);
                m_md5Writer.WriteLine(PrintBytes(m_md5.Hash) + " *" + sPath);
                m_md5Writer.Flush();
            }

            if (m_sha1Writer != null)
            {
                m_sha1.TransformFinalBlock(buf.data, 0, 0);
                string sPath = NormalizePath(m_sSourcePath);
                m_sha1Writer.WriteLine(PrintBytes(m_sha1.Hash) + " *" + sPath);
                m_sha1Writer.Flush();
            }

            if (m_sha256Writer != null)
            {
                m_sha256.TransformFinalBlock(buf.data, 0, 0);
                string sPath = NormalizePath(m_sSourcePath);
                m_sha256Writer.WriteLine(PrintBytes(m_sha256.Hash) + " *" + sPath);
                m_sha256Writer.Flush();
            }

            if (m_info.csType != ChecksumType.None)
            {
                string strChecksum = "";
                switch (m_info.csType)
                {
                    case ChecksumType.MD5:
                        m_md5.TransformFinalBlock(buf.data, 0, 0);
                        strChecksum = PrintBytes(m_md5.Hash);
                        break;
                    case ChecksumType.SHA1:
                        m_sha1.TransformFinalBlock(buf.data, 0, 0);
                        strChecksum = PrintBytes(m_sha1.Hash);
                        break;
                    case ChecksumType.SHA256:
                        m_sha256.TransformFinalBlock(buf.data, 0, 0);
                        strChecksum = PrintBytes(m_sha256.Hash);
                        break;
                    default:
                        break;
                }
                if (m_info.csString.ToLower() == strChecksum.ToLower())
                    Console.WriteLine("Checksums match ({0})", strChecksum);
                else
                    Console.WriteLine("Checksums FAIL!  Given: {0}  File: {1}", m_info.csString, strChecksum);
            }
        }

        private void DoThrottle()
        {
            if (m_info.bIgnoreDelayForVerify && m_bVerifyPass)
                return;

            if (m_info.iDelay > 0)
                Thread.Sleep(m_info.iDelay);
            else if (m_info.iSpeedLimit > 0)
            {
                TimeSpan ts = DateTime.Now - m_dtLastRead;
                if (ts.TotalSeconds == 0 || (m_iRead * 1024 / ts.TotalSeconds > m_info.iSpeedLimit))
                {
                    int iSleep = (int)(((m_iRead / (m_info.iSpeedLimit * 1024.0)) - ts.TotalSeconds) * 1000);
                    if (iSleep > 0)
                        Thread.Sleep(iSleep);
                }
            }
        }

        private void ReadBuffer(int index)
        {
            m_exRead = null;

            m_dtLastRead = DateTime.Now;
            try
            {
                m_copyBuffers[index].length = m_reader.Read(m_copyBuffers[index].data, 0, m_bufferSize);
            }
            catch (IOException ex)
            {
                m_exRead = ex;
                return;
            }
        }

        private void ProcessReadBuffer(int index)
        {
            m_iRead = m_copyBuffers[index].length;

            if (m_copyBuffers[index].length == 0)
                return;

            m_iBytesProcessed += m_iRead;
            m_iBytesTransferred += m_iRead;
            m_iLastBytes += m_iRead;

            if (m_md5Writer != null || m_info.csType == ChecksumType.MD5)
                m_md5.TransformBlock(m_copyBuffers[index].data, 0, m_iRead, m_copyBuffers[index].data, 0);

            if (m_sha1Writer != null || m_info.csType == ChecksumType.SHA1)
                m_sha1.TransformBlock(m_copyBuffers[index].data, 0, m_iRead, m_copyBuffers[index].data, 0);

            if (m_sha256Writer != null || m_info.csType == ChecksumType.SHA256)
                m_sha256.TransformBlock(m_copyBuffers[index].data, 0, m_iRead, m_copyBuffers[index].data, 0);

            m_lCurrent += m_iRead;

            DoThrottle();
        }

        private void ThreadWriter()
        {
            int iBytesIndex = 0;

            while (!m_bWriteFinished)
            {
                while ((m_copyBuffers[iBytesIndex] == null) || (m_copyBuffers[iBytesIndex].state != BufferState.Read))
                {
                    if (m_bReadFinished)
                    {
                        m_bWriteFinished = true;
                    }
                    whDataRead.WaitOne();
                }

                if (m_bWriteFinished)
                    break;

                foreach (WriterInfo wi in m_writers)
                {
                    bool bRetry = false;
                    do
                    {
                        bRetry = false;
                        WriteBuffer(wi.Writer, iBytesIndex);

                        if (m_exWrite != null)
                        {
                            if (!HandleWriteException())
                            {
                                m_bReadFinished = true;
                                m_bWriteFinished = true;
                                break;
                            }
                            else
                                bRetry = true;
                        }
                    } while (bRetry);
                }

                if (m_exWrite == null)
                {
                    // Successful write
                    m_copyBuffers[iBytesIndex].state = BufferState.Written;
                    whDataWritten.Set();
                }

                iBytesIndex++;

                if (iBytesIndex >= m_info.iNumBuffers)
                    iBytesIndex = 0;

                if (m_bReadFinished)
                {
                    m_bWriteFinished = true;
                    foreach (CopyBuffer cb in m_copyBuffers)
                    {
                        if (cb != null)
                        {
                            if (cb.state == BufferState.Read)   // Still have at least one to write
                            {
                                m_bWriteFinished = false;
                                break;
                            }
                        }
                    }
                    if (m_bWriteFinished)
                        break;
                }
            }

            foreach (WriterInfo wi in m_writers)
            {
                wi.Writer.Close();
                if (wi.Writer.BaseStream != null)
                    wi.Writer.BaseStream.Dispose();
                wi.Writer = null;
            }
        }

        private void WriteBuffer(BinaryWriter writer, int index)
        {
            try
            {
                writer.Write(m_copyBuffers[index].data, 0, m_copyBuffers[index].length);
            }
            catch (IOException ex)
            {
                m_exWrite = ex;
                return;
            }

            if (m_info.bReverify)
            {
                long lPos = writer.BaseStream.Position;

                int iLength = m_copyBuffers[index].length;

                BinaryReader reader = new BinaryReader(writer.BaseStream);
                reader.BaseStream.Seek(-iLength, SeekOrigin.Current);
                byte[] bVerify = reader.ReadBytes(iLength);
                for (int i = 0; i < iLength; i++)
                {
                    if (bVerify[i] != m_copyBuffers[index].data[i])
                    {
                        Console.WriteLine("\nERROR:  Verification FAILED at position {0}", lPos - (iLength - i));
                    }
                }
            }
        }

        private bool HandleReadException(int iBufferIndex)
        {
            if (m_exRead != null)
            {
                string sInput;
                if (m_info.bZeroUnreadableSectors)
                {
                    Console.WriteLine("Error reading {0} bytes: {1} [replacing with NULLs]", m_bufferSize, chomp(m_exRead.Message));
                    Array.Clear(m_copyBuffers[iBufferIndex].data, 0, m_bufferSize);
                    m_copyBuffers[iBufferIndex].length = m_bufferSize;
                    m_reader.BaseStream.Position += m_bufferSize;
                    return false;
                }
                else if (m_info.bRetryErrorsContinuously)
                {
                    Console.WriteLine("Error reading {0} bytes: {1} [retrying continuously]", m_bufferSize, chomp(m_exRead.Message));
                    sInput = "y";
                }
                else if (m_info.sErrorFile != "")
                {
                    string strErr = string.Format("Error reading {0} bytes from file \"{1}\" ({2}) - aborted", m_bufferSize, m_sDestPath, chomp(m_exRead.Message));
                    Console.WriteLine(strErr);
                    LogError(ErrorType.Read, strErr);
                    sInput = "n";
                }
                else
                {
                    Console.Write("\nError reading {0} bytes: {1}\nRetry? (y)es/(n)o/(c)ontinuous/(o)pen again/(r)educe buffer size -> ", m_bufferSize, chomp(m_exRead.Message));
                    sInput = Console.ReadLine() + " ";
                }
                switch (Char.ToLower(sInput[0]))
                {
                    case 'c':
                        m_info.bRetryErrorsContinuously = true;
                        return true;
                    case 'n':
                        foreach (WriterInfo wi in m_writers)
                            wi.Writer.Close();
                        m_reader.Close();
                        if (m_reader.BaseStream != null)
                            m_reader.BaseStream.Dispose();
                        m_reader = null;
                        m_iBytesProcessed += (m_iSourceSize - m_lCurrent);
                        return false;
                    case 'r':
                        m_bufferSize /= 2;
                        if (m_bufferSize < 512)
                            m_bufferSize = 512;
                        return true;
                    case 'o':
                        long lWrite, lRead;
                        SaveHandles(out lRead, out lWrite);

                        return ReopenHandles(lRead, lWrite);
                    default:
                        return true;
                }
            }
            return false;
        }

        private bool HandleWriteException()
        {
            if (m_exWrite != null)
            {
                string sInput = "n";
                if ((m_info.sErrorFile != "") && (!m_exWrite.Message.Contains("There is not enough space on the disk")))
                {
                    // Don't ignore disk space errors, no matter what the user says
                    string strErr = String.Format("Could not write to \"{0}\": {1}", m_sDestPath, chomp(m_exWrite.Message));
                    Console.WriteLine(strErr);
                    LogError(ErrorType.Write, strErr);
                }
                else
                {
                    Console.WriteLine("\nError writing to file \"" + StripBeeps(m_sDestPath) + "\" (" + chomp(m_exWrite.Message) + ")");
                    Console.Write("Retry? (y)es/(n)o/(o)pen again) ");
                    sInput = Console.ReadLine() + " ";
                }
                switch (Char.ToLower(sInput[0]))
                {
                    case 'n':
                        m_iBytesProcessed += (m_iSourceSize - m_lCurrent);
                        return false;
                    case 'o':
                        long lWrite, lRead;
                        SaveHandles(out lRead, out lWrite);

                        return ReopenHandles(lRead, lWrite);
                    default:
                        return true;
                }
            }
            return false;
        }

        private void RecalculateHashes(long pos)
        {
            m_exRead = null;

            m_reader.BaseStream.Position = 0;
            long iRead = 0;
            byte[] bytes;

            DateTime dtDisplay = DateTime.Now;

            while (iRead < pos)
            {
                if ((DateTime.Now - dtDisplay).TotalMilliseconds > 300)
                {
                    Console.Write("Re-calculating initial hash from file: {0:F2}%\r", (iRead / (double)pos) * 100);
                    dtDisplay = DateTime.Now;
                }

                if (iRead + m_bufferSize < pos)
                    bytes = m_reader.ReadBytes(m_bufferSize);
                else
                    bytes = m_reader.ReadBytes((int)(pos - iRead));

                iRead += bytes.Length;

                if (m_md5Writer != null || m_info.csType == ChecksumType.MD5)
                    m_md5.TransformBlock(bytes, 0, bytes.Length, bytes, 0);

                if (m_sha1Writer != null || m_info.csType == ChecksumType.SHA1)
                    m_sha1.TransformBlock(bytes, 0, bytes.Length, bytes, 0);

                if (m_sha256Writer != null || m_info.csType == ChecksumType.SHA256)
                    m_sha256.TransformBlock(bytes, 0, bytes.Length, bytes, 0);
            }

            Console.Write("Re-calculating initial hash from file: 100%     \n");

            m_lCurrent += m_iRead;

            DoThrottle();
        }

        private void SaveHandles(out long lRead, out long lWrite)
        {
            lWrite = -1;

            // We need to save the smallest writer, since we need a consistent place to restart
            foreach (WriterInfo wi in m_writers)
            {
                if (wi.Writer != null)
                {
                    if (lWrite == -1)
                        lWrite = wi.Writer.BaseStream.Position;
                    else if (lWrite > wi.Writer.BaseStream.Position)
                        lWrite = wi.Writer.BaseStream.Position;

                    wi.Writer.Close();
                    wi.Writer = null;
                }
            }

            lRead = m_reader.BaseStream.Position;
            m_reader.Close();
            if (m_reader.BaseStream != null)
                m_reader.BaseStream.Dispose();
            m_reader = null;

            if (m_md5Writer != null)
                m_md5Writer.Close();

            if (m_sha1Writer != null)
                m_sha1Writer.Close();

            if (m_sha256Writer != null)
                m_sha256Writer.Close();
        }

        private bool ReopenHandles(long lRead, long lWrite)
        {
            bool bRetry = false;

            do
            {
                bRetry = false;
                try
                {
                    m_reader = new BinaryReader(UnicodeAccess.GetReadStream(m_sSourcePath, m_info.bISOMode, lRead));

                    if (lWrite != -1)
                    {
                        foreach (WriterInfo wi in m_writers)
                        {
                            wi.Writer = new BinaryWriter(UnicodeAccess.GetExistingWriteStream(wi.Path));
                            wi.Writer.BaseStream.Position = lWrite;
                        }
                    }

                    if (m_info.sMD5File != "")
                        m_md5Writer = new StreamWriter(UnicodeAccess.GetAppendStream(m_info.sMD5File));

                    if (m_info.sSHAFile != "")
                        m_sha1Writer = new StreamWriter(UnicodeAccess.GetAppendStream(m_info.sSHAFile));

                    if (m_info.sSHAFile256 != "")
                        m_sha256Writer = new StreamWriter(UnicodeAccess.GetAppendStream(m_info.sSHAFile256));

                }
                catch (Exception ex)
                {
                    string sInput = "n";
                    if (m_info.sErrorFile != "")
                    {
                        string strErr = String.Format("Could not reopen handles: {0}", chomp(ex.Message));
                        Console.WriteLine(strErr);
                        LogError(ErrorType.Other, strErr);
                    }
                    else
                    {
                        Console.WriteLine("Unable to reopen handles: {0}", chomp(ex.Message));
                        Console.Write("Retry? (y/n) ");
                        sInput = Console.ReadLine() + " ";
                    }
                    switch (Char.ToLower(sInput[0]))
                    {
                        case 'n':
                            m_iBytesProcessed += (m_iSourceSize - m_lCurrent);
                            return false;
                        default:
                            bRetry = true;
                            break;
                    }
                }

            } while (bRetry);

            return true;
        }

        private void CopyFile(string sSource, string sFile, List<Destination> Destinations)
        {
            if (m_info.bISOMode)
            {
                m_sSourcePath = sSource.Substring(0, 2);
                m_iSourceSize = m_iSourceDirectorySize;
            }
            else
            {
                m_sSourcePath = Global.PathCombineNearestDir(sSource, sFile);
                m_iSourceSize = UnicodeAccess.GetFileSize(m_sSourcePath, m_info.bNoHead);
            }

            if (m_sLastHashFile != "")
            {
                if (m_sLastHashFile == NormalizePath(Path.Combine(sSource, sFile)))
                {
                    Console.WriteLine("\nResuming hash: " + sFile);
                    m_sLastHashFile = "";
                    return;
                }
                string strConsoleFull = String.Format("\r{{0,-{0}}}", Console.WindowWidth - 1);
                Console.Write(strConsoleFull, ConsoleTruncate("Skip: " + sFile));
                m_iBytesProcessed += m_iSourceSize;
                return;
            }

            if (CheckExcluded(sSource, sFile, PrintMode.CRLF))
                return;

            if (m_info.fExcludeAttributes != 0)
            {
                if ((UnicodeAccess.GetFileAttributes(m_sSourcePath) & m_info.fExcludeAttributes) != 0)
                    return;
            }

            if (m_md5Writer != null || m_info.csType == ChecksumType.MD5)
                m_md5.Initialize();

            if (m_sha1Writer != null || m_info.csType == ChecksumType.SHA1)
                m_sha1.Initialize();

            if (m_sha256Writer != null || m_info.csType == ChecksumType.SHA256)
                m_sha256.Initialize();

            m_sDestPath = "";

            if (m_info.bIsURL)
                m_iSourceDirectorySize = m_iSourceSize;

            uint attributes = 0;
            if ((m_info.bCopyAttributes) && (!m_info.bIsURL))
                attributes = UnicodeAccess.GetFileAttributes(m_sSourcePath);

            bool bRetryEntireCopy = false;

            do
            {
                m_lCurrent = 0;
                bRetryEntireCopy = false;
                long iTargetSize = 0;

                List<string> destinationFiles = new List<string>(Destinations.Count);

                if (m_info.bCopyFiles)
                {
                    foreach (Destination dest in Destinations)
                    {
                        m_sDestPath = dest.FullPath(sFile, m_info);

                        destinationFiles.Add(m_sDestPath);

                        Console.WriteLine("{0} -> {1}", StripBeeps(m_sSourcePath), StripBeeps(m_sDestPath));
                        if (UnicodeAccess.FileExists(m_sDestPath))
                        {
                            if (m_bNeverOverwrite)
                            {
                                m_iBytesProcessed += m_iSourceSize;
                                return;
                            }

                            if (m_info.bResume && (m_iSourceSize == UnicodeAccess.GetFileSize(m_sDestPath)))
                            {
                                m_iBytesProcessed += m_iSourceSize;
                                return;
                            }

                            if (m_info.bResume)
                            {
                                if (UnicodeAccess.IsReadOnly(m_sDestPath))
                                {
                                    //Console.WriteLine("Skipping readonly file: {0}", m_sDestPath);
                                    continue;
                                }
                                if (m_info.m_iTruncateBytes > 0)
                                    TruncateFile(m_sDestPath, m_info.m_iTruncateBytes);
                                iTargetSize = UnicodeAccess.GetFileSize(m_sDestPath);
                            }

                            if (!m_info.bOverwrite && !m_info.bResume)
                            {
                                if (m_info.sErrorFile != "")
                                {
                                    // Do not overwrite; note this in the error log and continue
                                    string strError = "\"" + m_sDestPath + "\" already exists; not overwritten.";
                                    Console.WriteLine(strError);
                                    LogError(ErrorType.Overwrite, strError);
                                    m_iBytesProcessed += m_iSourceSize;
                                    return;
                                }
                                Console.Write("File exists; overwrite? (y)es (n)o (a)lways n(e)ver (r)esume -> ");
                                string sInput = Console.ReadLine() + " ";
                                switch (Char.ToLower(sInput[0]))
                                {
                                    case 'n':
                                        m_iBytesProcessed += m_iSourceSize;
                                        return;
                                    case 'a':
                                        m_info.bOverwrite = true;
                                        break;
                                    case 'e':
                                        m_bNeverOverwrite = true;
                                        m_iBytesProcessed += m_iSourceSize;
                                        return;
                                    case 'r':
                                        m_info.bResume = true;
                                        if (UnicodeAccess.IsReadOnly(m_sDestPath))
                                        {
                                            //Console.WriteLine("Skipping readonly file: {0}", m_sDestPath);
                                            continue;
                                        }
                                        if (m_info.m_iTruncateBytes > 0)
                                            TruncateFile(m_sDestPath, m_info.m_iTruncateBytes);
                                        iTargetSize = UnicodeAccess.GetFileSize(m_sDestPath);
                                        if (m_iSourceSize == iTargetSize)
                                            return;
                                        break;
                                    default:
                                        break;
                                }
                            }
                        }
                    }
                }
                else
                {
                    Console.WriteLine("{0}", StripBeeps(m_sSourcePath));
                }

                m_reader = null;

                bool bRetryReader = false;

                do
                {
                    bRetryReader = false;
                    try
                    {
                        m_reader = new BinaryReader(UnicodeAccess.GetReadStream(m_sSourcePath, m_info.bISOMode, m_info.bResume ? iTargetSize : 0));
                    }
                    catch (Exception ex)
                    {
                        if ((m_info.sMD5File != "") && (m_sSourcePath.EndsWith(Path.GetFileName(m_info.sMD5File))))
                            return; // ignore the md5 file

                        if ((m_info.sSHAFile != "") && (m_sSourcePath.EndsWith(Path.GetFileName(m_info.sSHAFile))))
                            return; // ignore the sha file

                        if ((m_info.sSHAFile256 != "") && (m_sSourcePath.EndsWith(Path.GetFileName(m_info.sSHAFile256))))
                            return; // ignore the sha256 file

                        string strError = "Error: Unable to access file \"" + m_sSourcePath + "\" for reading (" + chomp(ex.Message) + ")";
                        Console.WriteLine(strError);
                        if (m_info.bIgnoreReadErrors)
                        {
                            return;
                        }
                        string sInput = "y";
                        if (m_info.sErrorFile != "")
                        {
                            LogError(ErrorType.Read, strError);
                        }
                        else
                        {
                            Console.Write("Ignore error? (y)es (n)o (a)lways (r)etry -> ");
                            sInput = Console.ReadLine() + " ";
                        }

                        switch (Char.ToLower(sInput[0]))
                        {
                            case 'n':
                                Environment.Exit(0);
                                return;
                            case 'a':
                                m_info.bIgnoreReadErrors = true;
                                break;
                            case 'y':
                                break;
                            default:
                                bRetryReader = true;
                                break;
                        }
                        if (!bRetryReader)
                            return; // can't continue with the copy if "retry" is not selected
                    }
                }
                while (bRetryReader);

                bool bAnyWriters = false;

                if (m_info.bCopyFiles)
                {
                    int iWriterIndex = 0;
                    foreach (Destination dest in Destinations)
                    {
                        if (dest.IsDirectory)
                            m_sDestPath = Path.Combine(dest.Path, Path.GetFileName(m_sSourcePath));
                        else
                            m_sDestPath = dest.Path;

                        bool bRetryWrite = false;

                        do
                        {
                            bRetryWrite = false;
                            WriterInfo wi = (WriterInfo)m_writers[iWriterIndex];
                            try
                            {
                                wi.Path = m_sDestPath;
                                bool bExists = UnicodeAccess.FileExists(m_sDestPath);
                                if ((m_info.bResume) && (bExists))
                                {
                                    if (UnicodeAccess.IsReadOnly(m_sDestPath))
                                    {
                                        Console.WriteLine("Skipping readonly file: {0}", m_sDestPath);
                                        wi.Writer = null;
                                        continue;
                                    }

                                    wi.Writer = new BinaryWriter(UnicodeAccess.GetExistingWriteStream(m_sDestPath));
                                    m_lCurrent = wi.Writer.Seek(0, SeekOrigin.End);
                                    if ((m_lCurrent > 0) && (m_md5Writer != null || m_sha1Writer != null || m_sha256Writer != null))
                                    {
                                        RecalculateHashes(m_lCurrent);
                                    }
                                    if (!m_info.bIsURL)
                                        m_reader.BaseStream.Position = m_lCurrent;
                                    m_iBytesProcessed += m_lCurrent;
                                }
                                else
                                {
                                    if (bExists)
                                    {
                                        uint attrDest = UnicodeAccess.GetFileAttributes(m_sDestPath);
                                        if ((attrDest & UnicodeAccess.FILE_ATTRIBUTE_READONLY) > 0)
                                        {
                                            if (m_info.bOverwrite)
                                            {
                                                UnicodeAccess.SetAppropriateFileAttributes(m_sDestPath, attrDest & ~UnicodeAccess.FILE_ATTRIBUTE_READONLY);
                                            }
                                        }
                                        if ((attrDest & UnicodeAccess.FILE_ATTRIBUTE_SYSTEM) > 0)
                                        {
                                            if (m_info.bOverwrite)
                                            {
                                                UnicodeAccess.SetAppropriateFileAttributes(m_sDestPath, attrDest & ~UnicodeAccess.FILE_ATTRIBUTE_SYSTEM);
                                            }
                                        }
                                    }

                                    if (m_info.bReverify)
                                        wi.Writer = new BinaryWriter(UnicodeAccess.GetReadWriteStream(m_sDestPath, true));
                                    else
                                        wi.Writer = new BinaryWriter(UnicodeAccess.GetWriteStream(m_sDestPath));
                                }
                                bAnyWriters = true;
                            }
                            catch (Exception ex)
                            {
                                string strError = "\nError: Unable to access file \"" + m_sDestPath + "\" for writing (" + chomp(ex.Message) + ")";
                                Console.WriteLine(strError);
                                if (m_info.bIgnoreWriteErrors)
                                {
                                    m_reader.Close();
                                    if (m_reader.BaseStream != null)
                                        m_reader.BaseStream.Dispose();
                                    m_reader = null;
                                    return;
                                }
                                string sInput = "y";
                                if (m_info.sErrorFile != "")
                                {
                                    LogError(ErrorType.Read, strError);
                                }
                                else
                                {
                                    Console.Write("Ignore error? (y)es (n)o (a)lways (r)etry -> ");
                                    sInput = Console.ReadLine() + " ";
                                }
                                switch (Char.ToLower(sInput[0]))
                                {
                                    case 'n':
                                        if (m_md5Writer != null)
                                            m_md5Writer.Close();
                                        if (m_sha1Writer != null)
                                            m_sha1Writer.Close();
                                        if (m_sha256Writer != null)
                                            m_sha256Writer.Close();
                                        m_bAbortCopy = true;
                                        return;
                                    case 'a':
                                        m_info.bIgnoreWriteErrors = true;
                                        break;
                                    case 'y':
                                        break;
                                    default:
                                        bRetryWrite = true;
                                        break;
                                }
                                if (!bRetryWrite)
                                {
                                    // can't continue with the copy unless "retry" is specified
                                    m_reader.Close();
                                    if (m_reader.BaseStream != null)
                                        m_reader.BaseStream.Dispose();
                                    m_reader = null;
                                    return;
                                }
                                wi.Writer.Close();
                            }
                        } while (bRetryWrite);

                        iWriterIndex++;
                    }
                }

                if (m_info.bCopyFiles && !bAnyWriters)
                {
                    return;
                }

                long iSize;
                if (m_info.bISOMode)
                    iSize = m_iSourceDirectorySize;
                else if (m_info.bIsURL)
                    iSize = m_iSourceDirectorySize;
                else
                    iSize = m_reader.BaseStream.Length;

                DateTime dtStart = DateTime.Now;
                DateTime dtPrev = dtStart;

                TimeSpan ts;

                m_bReadFinished = false;
                m_bWriteFinished = false;

                Thread threadReader = null;
                Thread threadWriter = null;

                if (m_info.bThreaded)
                {
                    threadReader = new Thread(ThreadReader);
                    threadReader.Start();

                    threadWriter = new Thread(ThreadWriter);
                    threadWriter.Start();
                }

                m_iRead = 0;

                do
                {
                    if (!m_info.bThreaded)
                    {
                        bool bRetryRead = false;
                        do
                        {
                            bRetryRead = false;
                            ReadBuffer(0);

                            if (m_exRead != null)
                            {
                                bRetryRead = HandleReadException(0);
                                m_exRead = null;
                            }

                        } while (bRetryRead);

                        ProcessReadBuffer(0);
                    }

                    if (m_info.bCopyFiles)
                    {
                        foreach (WriterInfo wi in m_writers)
                        {
                            if (!m_info.bThreaded)
                            {
                                bool bRetryWrite = false;
                                do
                                {
                                    bRetryWrite = false;
                                    WriteBuffer(wi.Writer, 0);

                                    if (m_exWrite != null)
                                    {
                                        bRetryWrite = HandleWriteException();
                                        m_exWrite = null;
                                        if (bRetryWrite == false)
                                        {
                                            foreach (WriterInfo wiClose in m_writers)
                                            {
                                                wiClose.Writer.Close();
                                            }
                                            m_reader.Close();
                                            if (m_reader.BaseStream != null)
                                                m_reader.BaseStream.Dispose();
                                            m_reader = null;
                                            return;
                                        }
                                    }
                                } while (bRetryWrite);
                            }
                        }
                    }

                    ts = DateTime.Now - m_dtLastBPSStart;
                    if (ts.TotalMilliseconds > 5000)
                    {
                        double bps = m_iLastBytes / ts.TotalSeconds;
                        if (bps > 1000)
                        {
                            DateTime dtETA = DateTime.Now.AddSeconds((m_iSourceDirectorySize - m_iBytesProcessed) / bps);
                            m_tsETR = dtETA - DateTime.Now;
                            m_fLastBPS = bps;
                        }
                        m_iLastBytes = 0;
                        m_dtLastBPSStart = DateTime.Now;
                    }

                    // Don't display status while we're dealing with an exception
                    while ((m_exRead != null) || (m_exWrite != null))
                        Thread.Sleep(500);

                    if ((DateTime.Now - dtPrev).TotalMilliseconds > 500)
                    {
                        String sPrint = GetProgressString(iSize);

                        string strConsoleFull = String.Format("{{0,-{0}}}", Console.WindowWidth);
                        Console.Write(strConsoleFull, sPrint);
                        dtPrev = DateTime.Now;
                    }

                    if (m_info.bThreaded)
                    {
                        Thread.Sleep(50);  // Don't go nuts in the UI loop
                    }
                    else
                    {
                        m_bReadFinished = (m_iRead == 0);
                        m_bWriteFinished = m_bReadFinished;

                        if (Console.KeyAvailable)
                        {
                            ConsoleKeyInfo keyInfo = Console.ReadKey(true);
                            if ((keyInfo.Key == ConsoleKey.P) && ((keyInfo.Modifiers & ConsoleModifiers.Control) > 0))
                            {
                                string sComplete = GetProgressString(iSize);
                                Console.Write("\n{0}\nPaused.  Press ENTER to continue or Ctrl+P again to close handles.", sComplete);
                                keyInfo = Console.ReadKey(true);

                                if ((keyInfo.Key == ConsoleKey.P) && ((keyInfo.Modifiers & ConsoleModifiers.Control) > 0))
                                {
                                    Console.WriteLine();
                                    long lWrite;
                                    long lRead;

                                    SaveHandles(out lRead, out lWrite);

                                    Console.Write("Paused: handles closed.  Press ENTER to continue ");
                                    Console.ReadLine();

                                    if (!ReopenHandles(lRead, lWrite))
                                        return;
                                }
                                else
                                    Console.WriteLine();
                            }
                        }
                    }
                } while (!m_bWriteFinished || !m_bReadFinished);

                DateTime dtEnd = DateTime.Now;
                ts = (dtEnd - dtStart);

                String sOut;
                if (ts.TotalSeconds > 0.2)
                    sOut = String.Format("{0}: 100% ({1} in {2}:{3:D2}.{4:D2}, {5}/sec)", OperationString(), HRBytes(iSize), (int)ts.TotalMinutes, ts.Seconds, ts.Milliseconds / 100, HRBytes((long)(iSize / ts.TotalSeconds), 3));
                else
                    sOut = OperationString() + ": 100%";

                if ((m_tsETR.TotalSeconds > 1) && (m_info.bCalculateSizes))
                {
                    sOut += String.Format(", ETR {0}:{1:D2} ({2:F2}% total)", m_tsETR.TotalMinutes > 9999 ? 9999 : (int)m_tsETR.TotalMinutes, m_tsETR.Seconds,
                        m_iBytesProcessed <= m_iSourceDirectorySize ? m_iBytesProcessed / (double)m_iSourceDirectorySize * 100 : 100);
                }

                string strConsole = String.Format("{{0,-{0}}}", Console.WindowWidth - 1);
                Console.WriteLine("\r" + strConsole, sOut);

                if (!m_info.bThreaded)
                {
                    if (m_reader != null)
                    {
                        m_reader.Close();
                        if (m_reader.BaseStream != null)
                            m_reader.BaseStream.Dispose();
                        m_reader = null;
                    }

                    FinalizeHashes(m_copyBuffers[0]);

                    if (m_info.bCopyFiles)
                    {
                        foreach (WriterInfo wi in m_writers)
                        {
                            wi.Writer.Close();
                        }
                    }
                }

                foreach (string strDestFile in destinationFiles)
                {
                    if (!m_info.bIgnoreSizeMismatches)
                    {
                        long iTestSize = UnicodeAccess.GetFileSize(strDestFile);
                        if (m_iSourceSize != iTestSize && (!m_info.bIsURL || m_iSourceSize != 0))
                        {
                            string strError = String.Format("ERROR:  Source file \"{0}\" is {1} byte{2} but destination file \"{3}\" is {4} byte{5}!",
                                StripBeeps(m_sSourcePath),
                                m_iSourceSize, m_iSourceSize == 1 ? "" : "s",
                                StripBeeps(strDestFile),
                                iTestSize, iTestSize == 1 ? "" : "s");
                            string sInput = "y";
                            if (m_info.sErrorFile != "")
                            {
                                LogError(ErrorType.Size, strError);
                            }
                            else
                            {
                                Console.WriteLine(strError);
                                Console.Write("Ignore error? (y)es (n)o (a)lways (r)etry -> ");
                                sInput = Console.ReadLine() + " ";
                            }
                            switch (Char.ToLower(sInput[0]))
                            {
                                case 'n':
                                    Environment.Exit(0);
                                    return;
                                case 'a':
                                    m_info.bIgnoreSizeMismatches = true;
                                    break;
                                case 'y':
                                    break;
                                default:
                                    File.Delete(strDestFile);
                                    bRetryEntireCopy = true;
                                    m_iBytesProcessed -= m_lCurrent;
                                    m_iBytesTransferred -= m_lCurrent;
                                    break;
                            }
                        }
                    }
                    if (bRetryEntireCopy)
                        break;
                }
            } while (bRetryEntireCopy);

            bool bRetry = false;
            do
            {
                bRetry = false;
                try
                {
                    if (m_info.bCopyACLs && !m_info.bIsURL)
                        UnicodeAccess.CopyACLs(m_sSourcePath, m_sDestPath);
                }
                catch (Exception ex)
                {
                    if (m_info.sErrorFile != "")
                    {
                        string strError = string.Format("Error setting ACLs for {0} : {1}", m_sDestPath, ex.Message);
                        Console.WriteLine(strError);
                        if (m_info.sErrorFile != "")
                            LogError(ErrorType.Other, strError);
                        bRetry = false;
                    }
                    else
                    {
                        Console.Write("\nError setting ACLs/Ownership for {0}: {1}\nRetry? (y)es/(n)o -> ", StripBeeps(m_sDestPath), chomp(ex.Message));
                        string sRetry = Console.ReadLine();
                        bRetry = sRetry.ToLower().StartsWith("y");
                    }
                }
            } while (bRetry);

            do
            {
                bRetry = false;
                try
                {
                    if (m_info.bCopyOwners && !m_info.bIsURL)
                        UnicodeAccess.CopyOwner(m_sSourcePath, m_sDestPath);
                }
                catch (Exception ex)
                {
                    if (m_info.sErrorFile != "")
                    {
                        string strError = string.Format("Error setting owner for {0} : {1}", m_sDestPath, ex.Message);
                        Console.WriteLine(strError);
                        if (m_info.sErrorFile != "")
                            LogError(ErrorType.Other, strError);
                        bRetry = false;
                    }
                    else
                    {
                        Console.Write("\nError setting owner for {0}: {1}\nRetry? (y)es/(n)o -> ", StripBeeps(m_sDestPath), chomp(ex.Message));
                        string sRetry = Console.ReadLine();
                        bRetry = sRetry.ToLower().StartsWith("y");
                    }
                }
            } while (bRetry);

            if (m_info.bCopyAttributes && !m_info.bIsURL)
                UnicodeAccess.SetAppropriateFileAttributes(m_sDestPath, attributes);
        }

        string OperationString()
        {
            if (m_info.bCopyFiles)
            {
                if (m_info.bReverify)
                    return "Verifying";
                else
                    return "Copying";
            }
            return "Processing";
        }

        string PrintBytes(byte[] bytes)
        {
            StringBuilder sResult = new StringBuilder();
            foreach (byte b in bytes)
                sResult.AppendFormat("{0:x2}", b);

            return sResult.ToString();
        }

        public long GetDirectorySize()
        {
            return m_iSourceDirectorySize;
        }

        public long TotalBytesCopied
        {
            get
            {
                return m_iSourceDirectorySize;
            }
        }

        public void LogError(ErrorType type, string str)
        {
            // Close after each write so that we don't end up with a partial log in the case of a system failure
            try
            {
                StreamWriter writer = new StreamWriter(m_info.sErrorFile, true);
                string strLog = "";
                switch (type)
                {
                    case ErrorType.Read:
                        strLog += "READ:      ";
                        break;
                    case ErrorType.Write:
                        strLog += "WRITE:     ";
                        break;
                    case ErrorType.Overwrite:
                        strLog += "OVERWRITE: ";
                        break;
                    case ErrorType.Size:
                        strLog += "SIZE:      ";
                        break;
                    case ErrorType.Other:
                        strLog += "ERROR:     ";
                        break;
                }
                writer.WriteLine(strLog + str);
                writer.Close();
            }
            catch (IOException ex)
            {
                Console.WriteLine("Could not log error: " + chomp(ex.Message));
            }
        }

        public string HRBytes(long bytes)
        {
            return HRBytes(bytes, 2);
        }

        public string HRBytes(long bytes, int iFigures)
        {
            string strFigures = string.Format("{{0:F{0}}}", iFigures);
            if (bytes < 1024)
                return string.Format("{0} b", bytes);
            if (bytes < 1024 * 1024)
                return string.Format(strFigures + " kB", bytes / 1024.0);
            if (bytes < 1024L * 1024L * 1024L)
                return string.Format(strFigures + " MB", bytes / (1024.0 * 1024.0));
            if (bytes < 1024L * 1024L * 1024L * 1024L)
                return string.Format(strFigures + " GB", bytes / (1024.0 * 1024.0 * 1024.0));
            if (bytes < 1024L * 1024L * 1024L * 1024L * 1024L)
                return string.Format(strFigures + " TB", bytes / (1024.0 * 1024.0 * 1024.0 * 1024.0));
            return string.Format(strFigures + " PB", bytes / (1024.0 * 1024.0 * 1024.0 * 1024.0 * 1024.0));
        }

        public string GetProgressString(long iSize)
        {
            string sPrint;
            if (m_info.bCalculateSizes)
            {
                if (m_tsETR.TotalMilliseconds > 100)
                {
                    sPrint = String.Format("\r{0:F2}% ({1}/{2}) - {3} of {4} ({5:F2}%), ETR {6}:{7:D2}",
                        m_lCurrent * 100 / (double)iSize,
                        HRBytes(m_lCurrent),
                        HRBytes(m_iSourceSize),
                        HRBytes(m_iBytesProcessed),
                        HRBytes(m_iSourceDirectorySize),
                        m_iBytesProcessed / (double)m_iSourceDirectorySize * 100,
                        m_tsETR.TotalMinutes > 9999 ? 9999 : (int)m_tsETR.TotalMinutes, m_tsETR.Seconds
                        );
                }
                else
                {
                    sPrint = String.Format("\r{0:F2}% ({1}/{2}) - {3} of {4} ({5:F2}%) total",
                        m_lCurrent * 100 / (double)iSize,
                        HRBytes(m_lCurrent),
                        HRBytes(m_iSourceSize),
                        HRBytes(m_iBytesProcessed),
                        HRBytes(m_iSourceDirectorySize),
                        m_iBytesProcessed / (double)m_iSourceDirectorySize * 100
                        );
                }
            }
            else
            {
                sPrint = String.Format("\r{0}: {1:F2}% ({2}/{3})",
                    m_info.bCopyFiles ? "Copying" : "Processing",
                    m_lCurrent * 100 / (double)iSize,
                    HRBytes(m_lCurrent),
                    HRBytes(m_iSourceSize)
                    );
            }

            if (GetAsyncKeyState(0x11 /* VK_CONTROL */) < 0)
                sPrint += String.Format(" [{0:F2}]", m_fLastBPS / (1024.0 * 1024.0));

            return sPrint;
        }

        public long TruncateFile(string strFile, long iBytes)
        {
            FileStream stream = UnicodeAccess.GetReadWriteStream(strFile, false);
            long length = stream.Length;
            if (length > iBytes)
                stream.SetLength(length - iBytes);
            else
                stream.SetLength(0);
            length = stream.Length;
            stream.Close();
            return length;
        }
    }
}
