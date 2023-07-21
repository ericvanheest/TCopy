using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Collections;

namespace TCopy
{
    public class DirectoryMonitor
    {
        private List<FileSystemWatcher> m_watchers = null;

        public ArrayList m_events;
        public ArrayList m_eventsVerify;

        private bool m_bVerifyPass = false;

        public DirectoryMonitor()
        {
            m_watchers = new List<FileSystemWatcher>();
        }
        
        public void Start(string strDirectory, bool bVerifyPass)
        {
            if (UnicodeAccess.ContainsWildcards(Path.GetFileName(strDirectory)))
                strDirectory = UnicodeAccess.GetDirectoryName(strDirectory);
            m_events = new ArrayList();
            m_eventsVerify = new ArrayList();
            FileSystemWatcher watcher = new FileSystemWatcher();
            watcher.Filter = "*.*";
            m_watchers.Add(watcher);
            m_bVerifyPass = bVerifyPass;

            watcher.Created += new FileSystemEventHandler(watcher_FileCreated);
            watcher.Deleted += new FileSystemEventHandler(watcher_FileDeleted);
            watcher.Renamed += new RenamedEventHandler(watcher_FileRenamed);
            watcher.Changed += new FileSystemEventHandler(watcher_FileChanged);
            watcher.Path = strDirectory;
            watcher.EnableRaisingEvents = true;
        }

        public void Stop()
        {
            foreach (FileSystemWatcher watcher in m_watchers)
            {
                if (watcher != null)
                {
                    watcher.Created -= watcher_FileCreated;
                    watcher.Deleted -= watcher_FileDeleted;
                    watcher.Renamed -= watcher_FileRenamed;
                    watcher.Changed -= watcher_FileChanged;
                    watcher.EnableRaisingEvents = false;
                    watcher.Dispose();
                }
            }
            m_watchers.Clear();
        }

        void AddEvent(string str)
        {
            if (m_bVerifyPass)
                m_eventsVerify.Add(str);
            else
                m_events.Add(str);
        }

        void watcher_FileCreated(object sender, FileSystemEventArgs e)
        {
            AddEvent(string.Format("File Created: {0}", e.FullPath));
        }

        void watcher_FileDeleted(object sender, FileSystemEventArgs e)
        {
            AddEvent(string.Format("File Deleted: {0}", e.FullPath));
        }

        void watcher_FileRenamed(object sender, RenamedEventArgs e)
        {
            AddEvent(string.Format("File Renamed: {0} => {1}", e.OldFullPath, e.FullPath));
        }

        void watcher_FileChanged(object sender, FileSystemEventArgs e)
        {
            AddEvent(string.Format("File Changed: {0}", e.FullPath));
        }
    }
}
