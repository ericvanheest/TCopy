using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;
using System.Collections;
using System.IO;
using System.Net;
using System.Security.AccessControl;

namespace TCopy
{
    class UnicodeAccess
    {
        public const uint FILE_FLAG_WRITE_THROUGH         = 0x80000000;
        public const uint FILE_FLAG_OVERLAPPED            = 0x40000000;
        public const uint FILE_FLAG_NO_BUFFERING          = 0x20000000;
        public const uint FILE_FLAG_RANDOM_ACCESS         = 0x10000000;
        public const uint FILE_FLAG_SEQUENTIAL_SCAN       = 0x08000000;
        public const uint FILE_FLAG_DELETE_ON_CLOSE       = 0x04000000;
        public const uint FILE_FLAG_BACKUP_SEMANTICS      = 0x02000000;
        public const uint FILE_FLAG_POSIX_SEMANTICS       = 0x01000000;
        public const uint FILE_FLAG_OPEN_REPARSE_POINT    = 0x00200000;
        public const uint FILE_FLAG_OPEN_NO_RECALL        = 0x00100000;
        public const uint FILE_FLAG_FIRST_PIPE_INSTANCE   = 0x00080000;

        public const uint GENERIC_READ = 0x80000000;
        public const uint GENERIC_WRITE = 0x40000000;
        public const uint GENERIC_EXECUTE = 0x20000000;
        public const uint GENERIC_ALL = 0x10000000;

        public const uint FILE_SHARE_READ = 0x00000001;
        public const uint FILE_SHARE_WRITE = 0x00000002;
        public const uint FILE_SHARE_DELETE = 0x00000004;
        public const uint FILE_ATTRIBUTE_READONLY = 0x00000001;
        public const uint FILE_ATTRIBUTE_HIDDEN = 0x00000002;
        public const uint FILE_ATTRIBUTE_SYSTEM = 0x00000004;
        public const uint FILE_ATTRIBUTE_DIRECTORY = 0x00000010;
        public const uint FILE_ATTRIBUTE_ARCHIVE = 0x00000020;
        public const uint FILE_ATTRIBUTE_DEVICE = 0x00000040;
        public const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;
        public const uint FILE_ATTRIBUTE_TEMPORARY = 0x00000100;
        public const uint FILE_ATTRIBUTE_SPARSE_FILE = 0x00000200;
        public const uint FILE_ATTRIBUTE_REPARSE_POINT = 0x00000400;
        public const uint FILE_ATTRIBUTE_COMPRESSED = 0x00000800;
        public const uint FILE_ATTRIBUTE_OFFLINE = 0x00001000;
        public const uint FILE_ATTRIBUTE_NOT_CONTENT_INDEXED = 0x00002000;
        public const uint FILE_ATTRIBUTE_ENCRYPTED = 0x00004000;
        public const uint FILE_ATTRIBUTE_VIRTUAL = 0x00010000;
        public const uint FILE_NOTIFY_CHANGE_FILE_NAME = 0x00000001;
        public const uint FILE_NOTIFY_CHANGE_DIR_NAME = 0x00000002;
        public const uint FILE_NOTIFY_CHANGE_ATTRIBUTES = 0x00000004;
        public const uint FILE_NOTIFY_CHANGE_SIZE = 0x00000008;
        public const uint FILE_NOTIFY_CHANGE_LAST_WRITE = 0x00000010;
        public const uint FILE_NOTIFY_CHANGE_LAST_ACCESS = 0x00000020;
        public const uint FILE_NOTIFY_CHANGE_CREATION = 0x00000040;
        public const uint FILE_NOTIFY_CHANGE_SECURITY = 0x00000100;
        public const uint FILE_ACTION_ADDED = 0x00000001;
        public const uint FILE_ACTION_REMOVED = 0x00000002;
        public const uint FILE_ACTION_MODIFIED = 0x00000003;
        public const uint FILE_ACTION_RENAMED_OLD_NAME = 0x00000004;
        public const uint FILE_ACTION_RENAMED_NEW_NAME = 0x00000005;
        public const uint MAILSLOT_NO_MESSAGE = 0xffffffff;
        public const uint MAILSLOT_WAIT_FOREVER = 0xffffffff;
        public const uint FILE_CASE_SENSITIVE_SEARCH = 0x00000001;
        public const uint FILE_CASE_PRESERVED_NAMES = 0x00000002;
        public const uint FILE_UNICODE_ON_DISK = 0x00000004;
        public const uint FILE_PERSISTENT_ACLS = 0x00000008;
        public const uint FILE_FILE_COMPRESSION = 0x00000010;
        public const uint FILE_VOLUME_QUOTAS = 0x00000020;
        public const uint FILE_SUPPORTS_SPARSE_FILES = 0x00000040;
        public const uint FILE_SUPPORTS_REPARSE_POINTS = 0x00000080;
        public const uint FILE_SUPPORTS_REMOTE_STORAGE = 0x00000100;
        public const uint FILE_VOLUME_IS_COMPRESSED = 0x00008000;
        public const uint FILE_SUPPORTS_OBJECT_IDS = 0x00010000;
        public const uint FILE_SUPPORTS_ENCRYPTION = 0x00020000;
        public const uint FILE_NAMED_STREAMS = 0x00040000;
        public const uint FILE_READ_ONLY_VOLUME = 0x00080000;

        public const uint CREATE_NEW = 1;
        public const uint CREATE_ALWAYS = 2;
        public const uint OPEN_EXISTING = 3;
        public const uint OPEN_ALWAYS = 4;
        public const uint TRUNCATE_EXISTING = 5;

        public const uint ERROR_ALREADY_EXISTS = 183;
        public const uint FIND_FIRST_EX_CASE_SENSITIVE = 0x00000001;
        public const int INVALID_HANDLE_VALUE = -1;
        public const int ERROR_NO_MORE_FILES = 18;
        public const int ERROR_NO_TOKEN = 1008;

        public static uint GetFileTypeFlags(string s)
        {
            uint flags = 0;

            foreach (char c in s)
            {
                switch (Char.ToLower(c))
                {
                    case 'j':
                        flags |= FILE_ATTRIBUTE_REPARSE_POINT;
                        break;
                    case 'd':
                        flags |= FILE_ATTRIBUTE_DIRECTORY;
                        break;
                    case 'r':
                        flags |= FILE_ATTRIBUTE_READONLY;
                        break;
                    case 's':
                        flags |= FILE_ATTRIBUTE_SYSTEM;
                        break;
                    case 'h':
                        flags |= FILE_ATTRIBUTE_HIDDEN;
                        break;
                    case 'c':
                        flags |= FILE_ATTRIBUTE_COMPRESSED;
                        break;
                    case 'e':
                        flags |= FILE_ATTRIBUTE_ENCRYPTED;
                        break;
                    case 'o':
                        flags |= FILE_ATTRIBUTE_OFFLINE;
                        break;
                    case 'p':
                        flags |= FILE_ATTRIBUTE_SPARSE_FILE;
                        break;
                    case 't':
                        flags |= FILE_ATTRIBUTE_TEMPORARY;
                        break;
                }
            }
            return flags;
        }

        [DllImport("Kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern SafeFileHandle CreateFile(
            string fileName,
            [MarshalAs(UnmanagedType.U4)] FileAccess fileAccess,
            [MarshalAs(UnmanagedType.U4)] FileShare fileShare,
            IntPtr securityAttributes,
            [MarshalAs(UnmanagedType.U4)] FileMode creationDisposition,
            [MarshalAs(UnmanagedType.U4)] EFileAttributes flags,
            IntPtr template);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool CloseHandle(SafeFileHandle handle);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool ReadFile(SafeFileHandle hFile, [Out] byte[] lpBuffer, uint nNumberOfBytesToRead,
           out uint lpNumberOfBytesRead, IntPtr lpOverlapped);

        enum FINDEX_INFO_LEVELS
        {
            FindExInfoStandard,
            FindExInfoMaxInfoLevel
        };

        enum FINDEX_SEARCH_OPS
        {
            FindExSearchNameMatch,
            FindExSearchLimitToDirectories,
            FindExSearchLimitToDevices,
            FindExSearchMaxSearchOp
        };

        [Flags]
        public enum EFileAttributes : uint
        {
            Readonly = 0x00000001,
            Hidden = 0x00000002,
            System = 0x00000004,
            Directory = 0x00000010,
            Archive = 0x00000020,
            Device = 0x00000040,
            Normal = 0x00000080,
            Temporary = 0x00000100,
            SparseFile = 0x00000200,
            ReparsePoint = 0x00000400,
            Compressed = 0x00000800,
            Offline = 0x00001000,
            NotContentIndexed = 0x00002000,
            Encrypted = 0x00004000,
            Write_Through = 0x80000000,
            Overlapped = 0x40000000,
            NoBuffering = 0x20000000,
            RandomAccess = 0x10000000,
            SequentialScan = 0x08000000,
            DeleteOnClose = 0x04000000,
            BackupSemantics = 0x02000000,
            PosixSemantics = 0x01000000,
            OpenReparsePoint = 0x00200000,
            OpenNoRecall = 0x00100000,
            FirstPipeInstance = 0x00080000
        }

        [StructLayoutAttribute(LayoutKind.Sequential, CharSet=CharSet.Unicode)]
        public struct WIN32_FIND_DATA
        {
            public uint dwFileAttributes;
            public System.Runtime.InteropServices.ComTypes.FILETIME ftCreationTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME ftLastAccessTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME ftLastWriteTime;
            public uint nFileSizeHigh;
            public uint nFileSizeLow;
            public uint dwReserved0;
            public uint dwReserved1;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string cFileName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
            public string cAlternate;

/*
            WIN32_FIND_DATA()
            {
                dwFileAttributes = 0;
                ftCreationTime.dwHighDateTime = 0;
                ftCreationTime.dwLowDateTime = 0;
                ftLastAccessTime.dwHighDateTime = 0;
                ftLastAccessTime.dwLowDateTime = 0;
                ftLastWriteTime.dwHighDateTime = 0;
                ftLastWriteTime.dwLowDateTime = 0;
                nFileSizeHigh = 0;
                nFileSizeLow = 0;
                dwReserved0 = 0;
                dwReserved1 = 0;
                cFileName = null;
                cAlternate = null;
            }
*/
        };

        [DllImport("kernel32.dll", CharSet=CharSet.Unicode)]
        static extern IntPtr FindFirstFileEx(string lpFileName, FINDEX_INFO_LEVELS
           fInfoLevelId, IntPtr lpFindFileData, FINDEX_SEARCH_OPS fSearchOp,
           IntPtr lpSearchFilter, uint dwAdditionalFlags);

        [DllImport("kernel32.dll", SetLastError=true, CharSet=CharSet.Unicode)]
        static extern IntPtr FindFirstFile(string lpFileName, out WIN32_FIND_DATA ffd);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern bool DeleteFile(string lpFileName);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern bool RemoveDirectory(string lpFileName);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern bool MoveFile(string lpExistingFileName, string lpNewFileName);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern bool FindNextFile(IntPtr hFindFile, out WIN32_FIND_DATA
           lpFindFileData);

        [DllImport("kernel32.dll", SetLastError=false)]
        static extern bool FindClose(IntPtr hFindFile);

        [DllImport("kernel32.dll", SetLastError = false)]
        public static extern uint GetFileAttributes(string strFile);

        [DllImport("kernel32.dll", SetLastError = false)]
        static extern bool SetFileAttributes(string strFile, uint attributes);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetDiskFreeSpaceEx(string lpDirectoryName,
        out ulong lpFreeBytesAvailable,
        out ulong lpTotalNumberOfBytes,
        out ulong lpTotalNumberOfFreeBytes);
        public enum FileTypes { Files, Directories, All };

        public static ulong GetDirectoryFreeSpace(string strDir)
        {
            ulong iFree = 0;
            ulong iTotal = 0;
            ulong iTotalFree = 0;

            // strDir might be a filename instead of a directory, in which case we need to fix it
            while (!Directory.Exists(strDir))
            {
                string strNewDir = UnicodeAccess.GetDirectoryName(strDir);
                if (strNewDir == strDir)
                    break;

                strDir = strNewDir;
            }

            GetDiskFreeSpaceEx(strDir, out iFree, out iTotal, out iTotalFree);

            return iFree;
        }

        public static bool SetAppropriateFileAttributes(string strFile, uint attributes)
        {
            attributes &= ~(FILE_ATTRIBUTE_COMPRESSED | FILE_ATTRIBUTE_DEVICE | FILE_ATTRIBUTE_DIRECTORY | FILE_ATTRIBUTE_ENCRYPTED | FILE_ATTRIBUTE_REPARSE_POINT | FILE_ATTRIBUTE_SPARSE_FILE);
            return SetFileAttributes(strFile, attributes);
        }

        public static bool CopyACLs(string strSource, string strDest)
        {
            DirectorySecurity dsDest = Directory.GetAccessControl(strDest);
            DirectorySecurity dsSource = new DirectorySecurity(strSource, AccessControlSections.All);

            string sddl = dsSource.GetSecurityDescriptorSddlForm(AccessControlSections.Access);

            // TOTALLY REPLACE The existing access rights with the new ones.
            dsDest.SetSecurityDescriptorSddlForm(sddl, AccessControlSections.Access);

            // Disable inheritance for this directory.
            dsDest.SetAccessRuleProtection(true, true);

            // Apply these changes.
            Directory.SetAccessControl(strDest, dsDest);

            return true;
        }

        public static bool CopyOwner(string strSource, string strDest)
        {
            DirectorySecurity dsDest = Directory.GetAccessControl(strDest);
            DirectorySecurity dsSource = new DirectorySecurity(strSource, AccessControlSections.All);

            string sddl = dsSource.GetSecurityDescriptorSddlForm(AccessControlSections.Owner);

            // TOTALLY REPLACE The existing access rights with the new ones.
            dsDest.SetSecurityDescriptorSddlForm(sddl, AccessControlSections.Owner);

            // Disable inheritance for this directory.
            dsDest.SetAccessRuleProtection(true, true);

            // Apply these changes.
            Directory.SetAccessControl(strDest, dsDest);

            return true;
        }

        public static bool IsURL(string str)
        {
            if (str.Length < 8)
                return false;
            return (str.Substring(0, 8).ToLower().Contains("://"));
        }

        public static long GetFileSize(string sFile, bool bNoHead = false)
        {
            // Note that sFile may be a wildcard
            if (IsURL(sFile))
            {
                if (bNoHead)
                    return 0;
                try
                {
                    WebRequest req = HttpWebRequest.Create(sFile);
                    req.Method = "HEAD";
                    WebResponse resp = req.GetResponse();
                    long iContentLength = 0;
                    if (long.TryParse(resp.Headers.Get("Content-Length"), out iContentLength))
                        return iContentLength;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error retrieving size of file via HTTP: {0}", ex.Message);
                }
                return 0;
            }
            WIN32_FIND_DATA ffd;
            long iTotalSize = 0;
            IntPtr hFindFile = FindFirstFile(UnicodePrefix(FullPathName(sFile)), out ffd);
            if (hFindFile == (IntPtr)INVALID_HANDLE_VALUE)
                return 0;
            iTotalSize = (((long)ffd.nFileSizeHigh << 32) | (long)ffd.nFileSizeLow);
            while (FindNextFile(hFindFile, out ffd)) {
                iTotalSize += (((long)ffd.nFileSizeHigh << 32) | (long)ffd.nFileSizeLow);
            }
            FindClose(hFindFile);
            return iTotalSize;
        }

        public static string[] DirectoryGetFiles(string sDir, string sWildcard)
        {
            return GetFiles(sDir, sWildcard, FileTypes.Files);
        }

        public static string[] GetFiles(string sDir, string sWildcard, FileTypes types)
        {
            WIN32_FIND_DATA ffd;
            string sPath = Path.Combine(FullPathName(sDir), sWildcard);
            IntPtr hFindFile = FindFirstFile(UnicodePrefix(sPath), out ffd);
            if (hFindFile == (IntPtr)INVALID_HANDLE_VALUE)
            {
                //if (Marshal.GetLastWin32Error() == ERROR_NO_MORE_FILES)
                    return new string[0];
                //throw new IOException("Could not locate file \"" + sPath + "\"");
            }

            ArrayList files = new ArrayList();

            do
            {
                if (ffd.cFileName != "." && ffd.cFileName != "..")
                {
                    switch (types)
                    {
                        case FileTypes.All:
                            files.Add(FullPathName(Path.Combine(sDir, ffd.cFileName)));
                            break;
                        case FileTypes.Directories:
                            if ((ffd.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) > 0)
                                files.Add(FullPathName(Path.Combine(sDir, ffd.cFileName)));
                            break;
                        case FileTypes.Files:
                            if ((ffd.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) == 0)
                                files.Add(FullPathName(Path.Combine(sDir, ffd.cFileName)));
                            break;
                    }
                }

                if (!FindNextFile(hFindFile, out ffd))
                {
                    int iResult = Marshal.GetLastWin32Error();
                    FindClose(hFindFile);
                    if (iResult == ERROR_NO_MORE_FILES || iResult == 0 || iResult == ERROR_NO_TOKEN)
                        break;
                    else
                        Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
                }
            } while (true);

            return (string[]) files.ToArray(typeof(string));

            /*
            SafeFileHandle handle = CreateFile(UnicodePrefix(sDir), GENERIC_READ | GENERIC_WRITE, FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE, 0, OPEN_EXISTING, FILE_FLAG_BACKUP_SEMANTICS, 0);
            if (handle.IsInvalid)
                return null;

            StreamReader reader = new StreamReader(new FileStream(handle, FileAccess.Read));


            string sFile = null;


            reader.Close();

            CloseHandle(handle);
            */

        }

        public static string[] DirectoryGetDirectories(string sDir)
        {
            return GetFiles(sDir, "*.*", FileTypes.Directories);
        }

        public static Stream GetReadStream(string sFile, bool bISO, long iResume)
        {
            if (IsURL(sFile))
            {
                HttpWebRequest webRequest = (HttpWebRequest) HttpWebRequest.Create(sFile);
                webRequest.Method = "GET";
                if (iResume > 0)
                    webRequest.AddRange((int) iResume);
                HttpWebResponse response = (HttpWebResponse) webRequest.GetResponse();
                return response.GetResponseStream();
            }
            SafeFileHandle handle = CreateFile(bISO ? UnicodeISOPrefix(FullPathName(sFile)) : UnicodePrefix(FullPathName(sFile)), FileAccess.Read, FileShare.Read, (IntPtr)0, FileMode.Open, 0, (IntPtr)0);
            if (handle.IsInvalid)
                Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());

            FileStream reader = new FileStream(handle, FileAccess.Read);
            if (iResume > 0)
                reader.Position = iResume;

            return reader;
        }

        public static bool IsReadOnly(string sFile)
        {
            return ((GetFileAttributes(sFile) & FILE_ATTRIBUTE_READONLY) > 0);
        }

        public static FileStream GetReadWriteStream(string sFile, bool bWriteThrough)
        {
            SafeFileHandle handle = CreateFile(UnicodePrefix(FullPathName(sFile)), FileAccess.ReadWrite, FileShare.Read, (IntPtr)0, FileMode.OpenOrCreate, bWriteThrough ? EFileAttributes.Write_Through : 0, (IntPtr)0);
            if (handle.IsInvalid)
                Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());

            return new FileStream(handle, FileAccess.ReadWrite);
        }

        public static FileStream GetWriteStream(string sFile)
        {
            SafeFileHandle handle = CreateFile(UnicodePrefix(FullPathName(sFile)), FileAccess.Write, FileShare.Read, (IntPtr)0, FileMode.Create, 0, (IntPtr)0);
            if (handle.IsInvalid)
                Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());

            return new FileStream(handle, FileAccess.Write);
        }

        public static FileStream GetExistingWriteStream(string sFile)
        {
            SafeFileHandle handle = CreateFile(UnicodePrefix(FullPathName(sFile)), FileAccess.ReadWrite, FileShare.Read, (IntPtr)0, FileMode.Open, 0, (IntPtr)0);
            if (handle.IsInvalid)
                Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());

            return new FileStream(handle, FileAccess.Write);
        }

        public static FileStream GetAppendStream(string sFile)
        {
            SafeFileHandle handle = CreateFile(UnicodePrefix(FullPathName(sFile)), FileAccess.ReadWrite, FileShare.Read, (IntPtr)0, FileMode.OpenOrCreate, 0, (IntPtr)0);
            if (handle.IsInvalid)
                Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());

            FileStream fs = new FileStream(handle, FileAccess.Write);
            fs.Seek(0, SeekOrigin.End);
            return fs;
        }

        [DllImport("kernel32.dll")]
        static extern uint GetFullPathName(string lpFileName, uint nBufferLength,
           [Out] StringBuilder lpBuffer, out IntPtr FilePart);

        [DllImport("Kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern uint GetFullPathNameW(
            string lpFileName,
            uint nBufferLength,
            StringBuilder lpBuffer,
            IntPtr FilePart);

        public static string FullPathName(string sFile)
        {
            StringBuilder sb = new StringBuilder(260);
            IntPtr part = (IntPtr) 0;
            uint iSize = GetFullPathNameW(sFile, 260, sb, part);
            if (iSize > 260)
            {
                sb.EnsureCapacity(32767);
                iSize = GetFullPathNameW(sFile, 32767, sb, part);
            }
            if (iSize == 0)
            {
                int iError = Marshal.GetLastWin32Error();
                if (iError != -2147024896)  // Operation completed successfully!
                    Marshal.ThrowExceptionForHR(iError);
            }

            return sb.ToString();
        }

        public static string UnicodePrefix(string sPath)
        {
            if (sPath.StartsWith(@"\\"))
                return sPath;
            return @"\\?\" + sPath;
        }

        public static string UnicodeISOPrefix(string sPath)
        {
            if (sPath.StartsWith(@"\\"))
                return sPath;
            return @"\\.\" + sPath.Substring(0,2);
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool CreateDirectoryW(string lpPathName, IntPtr lpSecurityAttributes);

        public static bool ContainsWildcards(string sTest)
        {
            return (sTest.Contains("*") || sTest.Contains("?"));
        }

        public static string RemoveWildcards(string str)
        {
			int iSlash = str.IndexOf('\\');
			if (iSlash != -1)
			{
				string sEnd = str.Substring(iSlash+1);
				if (ContainsWildcards(sEnd))
				{
					str = str.Substring(0, iSlash);
				}
			}
			
			return str;
        }

        public static string DirectoryCreateDirectory(string sDir, string sSourceACL, string sSourceOwner)
        {
            if (sSourceACL == null && sSourceOwner == null)
                return DirectoryCreateDirectory(sDir);

            string strResult = DirectoryCreateDirectory(sDir);
            if (sSourceACL != null)
                CopyACLs(sSourceACL, sDir);
            if (sSourceOwner != null)
                CopyOwner(sSourceOwner, sDir);
            return strResult;
        }
        
        public static string DirectoryCreateDirectory(string sDir)
        {
			sDir = RemoveWildcards(sDir);
			
            string sFull = FullPathName(sDir).Replace("\r\n", "");
            bool bResult = CreateDirectoryW(UnicodePrefix(sFull), (IntPtr)0);
            if (!bResult)
            {
				if (sFull.Length == 3 && sFull[1] == ':')   // e.g. "C:\"
					return sFull;
					
                else if (Marshal.GetLastWin32Error() == ERROR_ALREADY_EXISTS)
                    return null;

                string sParent = UnicodeAccess.GetDirectoryName(sFull);
                if (sParent != null)  // Can be null if the full path is something like C:\
                {
					if (sParent.Length > 3)
						bResult = (DirectoryCreateDirectory(sParent) != null);
				}

                if (bResult)
                    bResult = CreateDirectoryW(UnicodePrefix(sFull), (IntPtr)0);

                if (!bResult)
                {
                    if (Marshal.GetLastWin32Error() == ERROR_ALREADY_EXISTS)
                        return sFull;
                    Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
                }
            }

			if (bResult)
				return sFull;
			
			return null;
        }

        public static string EnsureBackslash(string s)
        {
            if (s.EndsWith(@"\"))
                return s;
            return s + @"\";
        }

        public static int CountOf(string str, char c)
        {
            return CountOf(str, c, str.Length);
        }

        public static int CountOf(string str, char c, int iMaximum)
        {
            int iResult = 0;
            for (int i = 0; i < iMaximum; i++)
            {
                if (str[i] == c)
                    iResult++;
            }

            return iResult;
        }

        public static void DeleteDirectoryWithOutput(string sDir)
        {
            Console.WriteLine("Deleting directory: {0}", sDir);
            string strFull = FullPathName(sDir);
            DeleteDirectory(strFull, true, CountOf(strFull, '\\'));
            Console.WriteLine("Completed.                    ");
        }

        public static void DeleteDirectory(string sDir)
        {
            string strFull = FullPathName(sDir);
            DeleteDirectory(strFull, false, CountOf(strFull, '\\'));
        }

        public static bool DeleteDirectory(string sDir, bool bOutput, int iMinimumMoveDepth)
        {
            WIN32_FIND_DATA ffd;
            bool bRetry = false;
            do
            {
                bRetry = false;
                IntPtr hFind = FindFirstFile(UnicodePrefix(sDir + "\\*.*"), out ffd);
                while (hFind != (IntPtr)INVALID_HANDLE_VALUE)
                {
                    if (ffd.cFileName != "." && ffd.cFileName != "..")
                    {
                        if ((ffd.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) > 0)
                            bRetry = DeleteDirectory(Path.Combine(sDir, ffd.cFileName), bOutput, iMinimumMoveDepth);
                        else
                            DeleteFile(Path.Combine(sDir, ffd.cFileName));
                    }

                    if (!FindNextFile(hFind, out ffd))
                        break;
                }
                FindClose(hFind);
            } while (bRetry);

            if (bOutput)
            {
                Console.Write("\rDirectory depth: {0}    ", CountOf(sDir, '\\'));
            }

            bool bResult = RemoveDirectory(UnicodePrefix(sDir));
            if (!bResult)
            {
                int iError = Marshal.GetLastWin32Error();
                if (iError == 145) // not empty
                {
                    int iSlash = sDir.LastIndexOf('\\');
                    if (iSlash > 0)
                    {
                        iSlash = sDir.LastIndexOf('\\', iSlash - 1);
                        if (iSlash > 0)
                        {
                            if (CountOf(sDir, '\\', iSlash) >= iMinimumMoveDepth)
                            {
                                string strNewPath = UnicodePrefix(sDir.Substring(0, iSlash + 1) + Guid.NewGuid().ToString());
                                if (MoveFile(UnicodePrefix(sDir), strNewPath))
                                    return true;
                            }
                        }
                    }
                }
            }

            return false;
        }

        public static bool DirectoryExists(string sDir)
        {
            if (ContainsWildcards(sDir))
                return false;
            sDir = FullPathName(sDir);
			if (sDir.EndsWith(":") || sDir.EndsWith(":\\"))
			{
				return Directory.Exists(sDir);
			}
            WIN32_FIND_DATA ffd;
            IntPtr hFindFile = FindFirstFile(UnicodePrefix(sDir), out ffd);
            if (hFindFile == (IntPtr)INVALID_HANDLE_VALUE)
                return false;
            FindClose(hFindFile);
            if ((ffd.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) > 0)
                return true;
            return false;
        }

        public static bool FileExists(string sFile)
        {
            WIN32_FIND_DATA ffd;
            IntPtr hFindFile = FindFirstFile(UnicodePrefix(FullPathName(sFile)), out ffd);
            if (hFindFile == (IntPtr)INVALID_HANDLE_VALUE)
                return false;
            FindClose(hFindFile);
            if ((ffd.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) > 0)
                return false;
            return true;
        }

        public static string GetDirectoryName(string sPath)
        {
            if (sPath.Length < 248)
                return Path.GetDirectoryName(sPath);

            while(sPath.Contains("\\\\"))
                sPath = sPath.Replace("\\\\", "\\");

            int iColon = sPath.IndexOf(':');
            int iSlash = sPath.LastIndexOf('\\');

            if (iSlash != -1)
            {
                if (iColon == 1 && iSlash == 2)
                {
                    if (sPath.Length > 3)
                        return sPath.Substring(0, 3);
                    return null;
                }
                return sPath.Substring(0, iSlash);
            }

            if (iColon != -1)
            {
                if (sPath.Length > 2)
                    return sPath.Substring(0, iColon + 1);
                return null;
            }

            return null;
        }

        public static string GetFileName(string sPath)
        {
            if (sPath.Length < 248)
                return Path.GetFileName(sPath);

            int iSlash = sPath.LastIndexOfAny(new char[] {'\\', ':'});

            if (iSlash != -1)
                return sPath.Substring(iSlash + 1);

            return "";
        }
    }
}
