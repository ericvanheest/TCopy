using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Management;
using System.Diagnostics;

namespace TCopy
{
    class Program
    {
        public static int m_iDelay = 0;
        public static int m_iBlockSize = 512;
        public static int m_iBuffers = 4;
        public static int m_iDefaultTruncate = 4096;

        enum NextOption { None, Delay, BlockSize, MD5File, SHAFile, NumBuffers, VerifyFile, AddDestination, SHAFile256, ExcludeFile, ErrorFile, SpeedLimit, Checksum, ResumeTruncate, ExecuteBeforeVerify, ExcludeTypes };
        enum NextArgs { None, Source, Destination, DeleteDir };

        static string Version()
        {
            return System.Diagnostics.Process.GetCurrentProcess().MainModule.FileVersionInfo.ProductVersion;
        }

        static void Usage()
        {
            Console.WriteLine(String.Format(
"TCopy, version {0}\n" +
"\n" +
"Usage:    TCopy [options] {{source}} [destination]\n" +
"\n" +
"Options:  -A file    Save SHA1 checksums to 'file' only (do not copy)\n" +
"          -a file    Save SHA1 checksums to 'file'\n" +
"          -A256 file Save SHA256 checksums to 'file' only (do not copy)\n" +
"          -a256 file Save SHA256 checksums to 'file'\n" +
"          -b size    Use 'size' kB as the block size (default: {2})\n" +
"          -c         Do not copy file attributes (read-only, etc.)\n" +
"          -C         Copy difficult files (like \"System Volume Information\")\n" +
"          -d dir     Add another destination directory\n" +
"          -e file    Write errors to 'file' and continue copying\n" +
"          -f t,cs    Copy and check a single file against type t, checksum cs\n" +
"          -g         Ignore read errors while copying\n" +
"          -G         Ignore write errors while copying\n" +
"          -H file    Save SHA256 checksums to 'file' only (do not copy)\n" +
"          -h file    Save SHA256 checksums to 'file'\n" +
"          -i         ISO mode.  Example:  tcopy -i J: DVDOutput.iso\n" +
"          -j prog    Execute \"prog\" just before verification step\n" +
"          -k         Do not calculate directory sizes\n" +
"          -l         Copy ACLs for files/directories\n" +
"          -L         Copy owner information for files/directories\n" +
"          -M file    Save MD5 checksums to 'file' only (do not copy)\n" +
"          -m file    Save MD5 checksums to 'file'\n" +
"          -n num     Use 'num' buffers in multithreaded mode (default: {3})\n" +
//"          -N         Do not write the TCopy_Recover.txt file\n" +
"          -o         Normalize the MD5/SHA files for easy comparison\n" +
"          -p delay   Pause for 'delay' ms after every block (default: {1})\n" +
"          -P speed   Pause after every block for a maximum of 'speed' kB/s\n" +
"          -r         Re-read the written data and verify against the source\n" +
"          -s         Copy files recursively\n" +
"          -S         Copy files recursively and create target directory\n" +
"          -t         Use separate reading and writing threads\n" +
"          -u         Resume a partial copy (including hash files, if any)\n" +
"          -U len     Resume a copy after trimming 'len' kB (default: {4})\n" +
"          -v file    Write a verification MD5/SHA1 file after copying\n" +
"          -V file    Write a verification file, ignoring -p/-P settings\n" +
"          -w         Wait for media to become available\n" +
"          -x files   Exclude the selected files/directories\n" +
"          -X types   Exclude files of these types.  Ex: jdhsrceopt\n" +
"          -y         Overwrite destination files without asking\n" +
"          -z         Replace unreadable blocks with zeros\n" +
"\n" +
"          --nohead   Do not use the HTTP HEAD command to get file sizes\n" +
"          --paste    Use files copied from explorer as the source (implies -s)\n" +
"          --md5      Shortcut: -So -e errors.txt -m from.md5 -v to.md5\n" +
"          --delete d Use long-paths to completely remove a directory \"d\"" +
"", Version(), m_iDelay, m_iBlockSize, m_iBuffers, m_iDefaultTruncate / 1024));
        }

        [STAThread]
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Usage();
                return;
            }

            NextOption nextOption = NextOption.None;
            NextArgs nextArg = NextArgs.Source;
            string sDest = ".";

            ThrottleCopier tc = new ThrottleCopier();

            tc.m_info.iDelay = m_iDelay;
            tc.m_info.iBlockSize = m_iBlockSize;
            tc.m_info.iNumBuffers = m_iBuffers;
            tc.m_info.iSpeedLimit = 0;
            bool bMD5Shortcut = false;

            foreach (string s in args)
            {
                switch (nextOption)
                {
                    case NextOption.None:
                        if (s[0] == '-' || s[0] == '/')
                        {
                            for (int i = 1; i < s.Length; i++)
                            {
                                switch (s[i])
                                {
                                    case 'm':
                                        nextOption = NextOption.MD5File;
                                        break;
                                    case 'M':
                                        nextOption = NextOption.MD5File;
                                        tc.m_info.bCopyFiles = false;
                                        break;
                                    case 'x':
                                        nextOption = NextOption.ExcludeFile;
                                        break;
                                    case 'X':
                                        nextOption = NextOption.ExcludeTypes;
                                        break;
                                    case 'a':
                                        if (s.Substring(i+1) == "256")
                                            nextOption = NextOption.SHAFile256;
                                        else
                                            nextOption = NextOption.SHAFile;
                                        break;
                                    case 'A':
                                        if (s.Substring(i+1) == "256")
                                            nextOption = NextOption.SHAFile256;
                                        else
                                            nextOption = NextOption.SHAFile;
                                        tc.m_info.bCopyFiles = false;
                                        break;
                                    case 'p':
                                        nextOption = NextOption.Delay;
                                        break;
                                    case 'P':
                                        nextOption = NextOption.SpeedLimit;
                                        break;
                                    case 'b':
                                    case 'B':
                                        nextOption = NextOption.BlockSize;
                                        break;
                                    case 'n':
                                        nextOption = NextOption.NumBuffers;
                                        break;
                                    case 'j':
                                    case 'J':
                                        nextOption = NextOption.ExecuteBeforeVerify;
                                        break;
                                    case 's':
                                        tc.m_info.bRecursive = true;
                                        break;
                                    case 'S':
                                        tc.m_info.bRecursive = true;
                                        tc.m_info.bCreateTarget = true;
                                        break;
                                    case 't':
                                    case 'T':
                                        tc.m_info.bThreaded = true;
                                        break;
                                    case 'k':
                                    case 'K':
                                        tc.m_info.bCalculateSizes = false;
                                        break;
                                    case 'y':
                                    case 'Y':
                                        tc.m_info.bOverwrite = true;
                                        break;
                                    case 'w':
                                    case 'W':
                                        tc.m_info.bWaitForMedia = true;
                                        break;
                                    case 'v':
                                        nextOption = NextOption.VerifyFile;
                                        break;
                                    case 'V':
                                        tc.m_info.bIgnoreDelayForVerify = true;
                                        nextOption = NextOption.VerifyFile;
                                        break;
                                    case 'r':
                                    case 'R':
                                        tc.m_info.bReverify = true;
                                        break;
                                    case 'u':
                                        tc.m_info.bResume = true;
                                        break;
                                    case 'U':
                                        tc.m_info.bResume = true;
                                        nextOption = NextOption.ResumeTruncate;
                                        break;
                                    case 'd':
                                    case 'D':
                                        nextOption = NextOption.AddDestination;
                                        break;
                                    case 'e':
                                    case 'E':
                                        nextOption = NextOption.ErrorFile;
                                        break;
                                    case 'g':
                                        tc.m_info.bIgnoreReadErrors = true;
                                        break;
                                    case 'G':
                                        tc.m_info.bIgnoreWriteErrors = true;
                                        break;
                                    case 'o':
                                    case 'O':
                                        tc.m_info.bNormalizeHashFile = true;
                                        break;
                                    case 'c':
                                        tc.m_info.bCopyAttributes = false;
                                        break;
                                    case 'C':
                                        tc.m_info.bCopyAnnoyingFiles = true;
                                        break;
                                    case 'z':
                                    case 'Z':
                                        tc.m_info.bZeroUnreadableSectors = true;
                                        break;
                                    case 'i':
                                    case 'I':
                                        tc.m_info.bISOMode = true;
                                        break;
                                    case 'l':
                                        tc.m_info.bCopyACLs = true;
                                        break;
                                    case 'L':
                                        tc.m_info.bCopyOwners = true;
                                        break;
                                    case 'f':
                                    case 'F':
                                        nextOption = NextOption.Checksum;
                                        break;
                                    case '-':
                                        switch (s.Substring(i + 1))
                                        {
                                            case "nohead":
                                                tc.m_info.bNoHead = true;
                                                break;
                                            case "paste":
                                                tc.m_info.bRecursive = true;
                                                tc.m_info.bCreateTarget = true;
                                                tc.m_info.sSources = ClipboardAccess.GetClipboardFiles();
                                                nextArg = NextArgs.Destination;
                                                break;
                                            case "md5":
                                                bMD5Shortcut = true;
                                                tc.m_info.bRecursive = true;
                                                tc.m_info.bCreateTarget = true;
                                                tc.m_info.bNormalizeHashFile = true;
                                                break;
                                            case "delete":
                                                nextArg = NextArgs.DeleteDir;
                                                break;
                                            default:
                                                Console.WriteLine("Warning:  Ignoring unknown option: --{0}", s.Substring(i+1));
                                                break;
                                        }
                                        i = s.Length;
                                        break;
                                }
                            }
                        }
                        else switch(nextArg)
                        {
                            case NextArgs.Source:
                                tc.m_info.sSources = new Source[1];
                                tc.m_info.sSources[0] = new Source(s);
                                nextArg = NextArgs.Destination;
                                break;
                            case NextArgs.Destination:
                                sDest = s;
                                nextArg = NextArgs.None;
                                break;
                            case NextArgs.DeleteDir:
                                tc.m_info.sDeleteDir = s;
                                nextArg = NextArgs.None;
                                break;
                            default:
                                Console.WriteLine("Warning: Ignoring extra command line parameters");
                                break;
                        }
                        break;
                    case NextOption.Delay:
                        tc.m_info.iDelay = Convert.ToInt32(s);
                        nextOption = NextOption.None;
                        break;
                    case NextOption.SpeedLimit:
                        tc.m_info.iSpeedLimit = Convert.ToInt32(s);
                        nextOption = NextOption.None;
                        break;
                    case NextOption.NumBuffers:
                        tc.m_info.iNumBuffers = Convert.ToInt32(s);
                        nextOption = NextOption.None;
                        break;
                    case NextOption.BlockSize:
                        tc.m_info.iBlockSize = Convert.ToInt32(s);
                        nextOption = NextOption.None;
                        break;
                    case NextOption.ResumeTruncate:
                        tc.m_info.m_iTruncateBytes = Convert.ToInt32(s);
                        nextOption = NextOption.None;
                        break;
                    case NextOption.ExecuteBeforeVerify:
                        tc.m_info.sExecuteBeforeVerify = s;
                        nextOption = NextOption.None;
                        break;
                    case NextOption.MD5File:
                        tc.m_info.sMD5File = s;
                        nextOption = NextOption.None;
                        break;
                    case NextOption.SHAFile:
                        tc.m_info.sSHAFile = s;
                        nextOption = NextOption.None;
                        break;
                    case NextOption.SHAFile256:
                        tc.m_info.sSHAFile256 = s;
                        nextOption = NextOption.None;
                        break;
                    case NextOption.VerifyFile:
                        tc.m_info.sVerifyFile = s;
                        nextOption = NextOption.None;
                        break;
                    case NextOption.AddDestination:
                        tc.m_info.Destinations.Add(new Destination(s));
                        nextOption = NextOption.None;
                        break;
                    case NextOption.ExcludeFile:
                        tc.m_info.AddExclusions(s);
                        nextOption = NextOption.None;
                        break;
                    case NextOption.ErrorFile:
                        tc.m_info.sErrorFile = s;
                        if (File.Exists(s))
                            File.Delete(s);
                        nextOption = NextOption.None;
                        break;
                    case NextOption.ExcludeTypes:
                        tc.m_info.fExcludeAttributes = UnicodeAccess.GetFileTypeFlags(s);
                        nextOption = NextOption.None;
                        break;
                    case NextOption.Checksum:
                        string[] cs = s.Split(new char[] { ',' });
                        if (cs.Length < 2)
                        {
                            Console.WriteLine("Error: -f option must specify type,checksum");
                            return;
                        }
                        else
                        {
                            switch(cs[0].ToLower())
                            {
                                case "md5":
                                    tc.m_info.csType = ChecksumType.MD5;
                                    break;
                                case "sha1":
                                    tc.m_info.csType = ChecksumType.SHA1;
                                    break;
                                case "sha256":
                                    tc.m_info.csType = ChecksumType.SHA256;
                                    break;
                                default:
                                    Console.WriteLine("Error: -f checksum type must be md5, sha1 or sha256");
                                    tc.m_info.csType = ChecksumType.None;
                                    return;
                            }
                            tc.m_info.csString = cs[1];
                        }
                        nextOption = NextOption.None;
                        break;
                }
            }

            if (tc.m_info.sDeleteDir.Length > 0)
            {
                UnicodeAccess.DeleteDirectoryWithOutput(tc.m_info.sDeleteDir);
                return;
            }

            if (tc.m_info.sSources == null)
            {
                Console.WriteLine("Error:  Source directory not specified");
                return;
            }

            if (tc.m_info.sSources.Length == 0)
            {
                Console.WriteLine("Error:  No files in source list");
                return;
            }

            if (nextOption != NextOption.None)
            {
                Console.WriteLine("Error:  Incomplete command line");
                return;
            }

            if (!tc.m_info.bThreaded)
                tc.m_info.iNumBuffers = 1;

			sDest = UnicodeAccess.RemoveWildcards(sDest);
			
            tc.m_info.Destinations.Insert(0, new Destination(sDest));

            if (tc.m_info.sSHAFile != "")
	            tc.m_info.IgnoreForVerify.Add(tc.m_info.sSHAFile);
            if (tc.m_info.sSHAFile256 != "")
                tc.m_info.IgnoreForVerify.Add(tc.m_info.sSHAFile256);
            if (tc.m_info.sMD5File != "")
	            tc.m_info.IgnoreForVerify.Add(tc.m_info.sMD5File);

            if (bMD5Shortcut)
            {
                tc.m_info.sErrorFile = Global.PathCombineNearestDir(sDest, "errors.txt");
                tc.m_info.sMD5File = Global.PathCombineNearestDir(sDest, "from.md5");
                tc.m_info.sVerifyFile = Global.PathCombineNearestDir(sDest, "to.md5");
                if (File.Exists(tc.m_info.sErrorFile))
                    File.Delete(tc.m_info.sErrorFile);
            }

            tc.m_info.GenerateNormalizationConstant();

			DateTime dtStartCopy = DateTime.Now;

            tc.StartCopy(false);
            
            DateTime dtEndCopy = DateTime.Now;
            
            TimeSpan tsTotalTime = dtEndCopy.Subtract(dtStartCopy);
            
            long iSourceSize = tc.GetDirectorySize();

            long[] destSizes = new long[tc.m_info.Destinations.Count];

            if (tc.m_info.sVerifyFile != "")
            {
                tc.ResetETR();
				tc.m_info.bISOMode = false;
                int iIndex = 1;

                if (!string.IsNullOrEmpty(tc.m_info.sExecuteBeforeVerify))
                {
                    Process proc = null;
                    try
                    {
                        Console.WriteLine("Executing {0}", tc.m_info.sExecuteBeforeVerify);
                        ProcessStartInfo pi = new ProcessStartInfo(tc.m_info.sExecuteBeforeVerify);
                        pi.CreateNoWindow = true;
                        pi.UseShellExecute = false;
                        pi.RedirectStandardError = true;
                        pi.RedirectStandardOutput = true;
                        proc = Process.Start(pi);
                        string strLine = null;
                        do
                        {
                            strLine = proc.StandardOutput.ReadLine();
                            if (strLine != null)
                                Console.WriteLine(strLine);
                        } while (strLine != null);
                        Console.Error.Write(proc.StandardError.ReadToEnd());
                        proc.WaitForExit();
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine("Error starting process \"{0}\": {1}", tc.m_info.sExecuteBeforeVerify, ex.Message);
                    }
                }

                if (tc.m_info.sSources.Length > 1)
                {
                    foreach (Destination dest in tc.m_info.Destinations)
                        dest.Path = dest.OrigPath;
                }

                foreach (Destination dest in tc.m_info.Destinations)
                {
                    string sVerifyDest = dest.Path;
                    string sFile = tc.m_info.sVerifyFile;
                    if (iIndex > 1)
                        sFile = Path.Combine(UnicodeAccess.GetDirectoryName(sFile), (Path.GetFileNameWithoutExtension(sFile) + String.Format(" ({0})", iIndex) + Path.GetExtension(sFile)));

                    tc.m_info.IgnoreForVerify.Add(sFile);
                    
                    if (tc.m_info.sSHAFile != "")
                    {
                        tc.m_info.sHashFile = tc.m_info.sSHAFile;
                        tc.m_info.sSHAFile = sFile;
                        tc.m_info.sSHAFile256 = "";
                        tc.m_info.sMD5File = "";
                    }
                    else if (tc.m_info.sSHAFile256 != "")
                    {
                        tc.m_info.sHashFile = tc.m_info.sSHAFile256;
                        tc.m_info.sSHAFile256 = sFile;
                        tc.m_info.sSHAFile = "";
                        tc.m_info.sMD5File = "";
                    }
                    else
                    {
                        tc.m_info.sHashFile = tc.m_info.sMD5File;
                        tc.m_info.sMD5File = sFile;
                        tc.m_info.sSHAFile = "";
                        tc.m_info.sSHAFile256 = "";
                    }

                    tc.m_info.bCopyFiles = false;

                    if (tc.m_info.sSources.Length > 1 || UnicodeAccess.FileExists(tc.m_info.sSources[0].Path))
                    {
                        foreach (Source source in tc.m_info.sSources)
                        {
                            source.Path = Global.PathCombineNearestDir(sVerifyDest, Path.GetFileName(source.Path));
                        }
                    }
                    else
                    {
                        tc.m_info.sSources = new Source[1];
                        tc.m_info.sSources[0] = new Source(sVerifyDest);
                    }

                    tc.m_info.GenerateNormalizationConstant();

                    tc.StartCopy(true);

                    destSizes[iIndex-1] = tc.GetDirectorySize();

                    iIndex++;
                }
            }

            if ((tc.m_info.iExcludedSource > 0) || (tc.m_info.iExcludedTarget > 0))
                Console.WriteLine("\nSource total: {0} bytes ({1} excluded)", iSourceSize - tc.m_info.iExcludedSource, tc.m_info.iExcludedSource);
            else
                Console.WriteLine("\nSource total: {0} bytes", iSourceSize);
            
            if (tc.m_info.sVerifyFile != "")
            {
				int iSizeIndex = 0;
				foreach (long size in destSizes)
				{
					Console.WriteLine("Target {0}total: {1} bytes{2}", destSizes.Length > 1 ? iSizeIndex.ToString() + " " : "", size, size == iSourceSize ? " [match]" : " [DOES NOT MATCH!]");
					iSizeIndex++;
				}
			}
            if (!string.IsNullOrEmpty(tc.m_info.sHashFile) && !string.IsNullOrEmpty(tc.m_info.sVerifyFile) && File.Exists(tc.m_info.sHashFile) && File.Exists(tc.m_info.sVerifyFile))
            {
                // Compare the MD5 files to see if the copy worked properly.
                FileComparer comparer = new FileComparer(tc.m_info.sHashFile, tc.m_info.sVerifyFile);
                Console.WriteLine("Comparing hash files:  {0}", comparer.AreFilesIdentical() ? "[match]" : (comparer.DoHashValuesMatch() ? "[hashes match, paths differ]" : "[DIFFERENCES IN HASH FILES!]"));

            }

            Console.WriteLine("\nCopy Finished: {0} bytes in {1} seconds ({2:F3} MB/sec)", tc.TotalBytesCopied, (int) tsTotalTime.TotalSeconds, tc.TotalBytesCopied / tsTotalTime.TotalSeconds / (1024 * 1024));

            if (File.Exists(tc.m_info.sErrorFile))
            {
                Console.WriteLine("\nWARNING!  Errors were logged to \"" + tc.m_info.sErrorFile + "\"!");
                Console.WriteLine("Press 'Enter' to view this file or any other key to exit.");
                if (Console.ReadKey().Key == ConsoleKey.Enter)
                {
                    using (System.Diagnostics.Process prc = new System.Diagnostics.Process())
                    {
                        prc.StartInfo.FileName = tc.m_info.sErrorFile;
                        prc.Start();
                    }
                }
            }

            tc.m_info.monitor.Stop();

			if (tc.m_info.monitor.m_events != null)
			{
				if (tc.m_info.monitor.m_events.Count > 0)
				{
					Console.WriteLine("\nWARNING!  Files in the source path were modified during the copy!");
					Console.WriteLine("Press 'Enter' to view the list of changes or any other key to exit.");
					if (Console.ReadKey().Key == ConsoleKey.Enter)
					{
						string sTempFile = Path.GetTempFileName() + ".txt";
						StreamWriter writer = new StreamWriter(sTempFile);
						foreach (string str in tc.m_info.monitor.m_events)
							writer.WriteLine(str);
						writer.Close();
						using (System.Diagnostics.Process prc = new System.Diagnostics.Process())
						{
							prc.StartInfo.FileName = sTempFile;
							prc.Start();
							prc.WaitForExit();
						}
						File.Delete(sTempFile);
					}
				}
            }
        }
    }
}
