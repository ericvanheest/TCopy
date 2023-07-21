using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using System.IO;

namespace TCopy
{
    public class Source
    {
        public string Path = null;
        private string m_sWildcard = null;
        private string m_sSourceDir = null;

        public Source(string str)
        {
            Path = str;
        }

        public string SourceDir
        {
            get
            {
                if (m_sSourceDir == null)
                    CreateValues();

                return m_sSourceDir;
            }

            set
            {
                m_sSourceDir = value;
            }
        }

        private void CreateValues()
        {
            if (Directory.Exists(Path))
            {
                m_sSourceDir = Path;
                m_sWildcard = "*.*";
            }
            else
            {
                m_sSourceDir = UnicodeAccess.GetDirectoryName(Path);
                m_sWildcard = System.IO.Path.GetFileName(Path);
            }
        }

        public string Wildcard
        {
            get
            {
                if (m_sWildcard == null)
                    CreateValues();

                return m_sWildcard;
            }

            set
            {
                m_sWildcard = value;
            }
        }
    }

    public class ClipboardAccess
    {
        public ClipboardAccess()
        {
        }

        public static Source[] GetClipboardFiles()
        {
            if (!Clipboard.ContainsFileDropList())
                return new Source[0];

            System.Collections.Specialized.StringCollection collection = Clipboard.GetFileDropList();
            List<Source> result = new List<Source>(collection.Count);
            foreach (string str in collection)
                result.Add(new Source(str));

            return result.ToArray();
        }
    }
}
