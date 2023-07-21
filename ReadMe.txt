TCopy - Throttle-capable file copying with md5/sha1 verification

TCopy's primary function was originally to allow a configurable pause
in between copying blocks of data from a CD/DVD drive (to prevent I/O
overload on a system).  It has other features that help this kind of
file transfer, such as MD5/SHA hash calculations, the ability to retry
failed sectors and options to resume copies of interrupted large file
transfers.

======================================================================

TCopy, version 1.0.72

TCopy was originally designed for copying data from optical media on
older systems without rendering the machine useless during the copy.
It accomplishes this by allowing a configurable pause between copies of
blocks of data.  I've found that a 2 ms pause between 64 kB blocks keeps
my particular test machine workable while not incurring too much of a
penalty on the copy process.

TCopy has expanded to include the ability to pause and resume a copy in
progress (press Control+P to force it, or select "open again" during a
read or write error) that closes and re-opens the file handles.  This
is useful for taking out a DVD and cleaning it before continuing the
copy process.

TCopy allows you to re-try sectors that have had CRC failures without
having to re-read the entire file.  You can also reduce the buffer size by
50% in an attempt to read every sector up to the failed one (sometimes this
helps, sometimes not; it's different for every weak disc), with an option
for continuous automatic retrying of a particular bad area.

TCopy can calculate MD5 and/or SHA1 hashes while copying.  This takes very
little extra time as compared to doing the calculation before or after the
copy, since the data is already in memory.

TCopy can copy from a URL in the form of http://example.com/file.dat and
will use the HTTP Range header to attempt to resume an aborted copy, but
some features (such as re-opening the file handles) will not work
properly.

======================================================================

Usage:    TCopy [options] {source} [destination]

Options:  -A file    Save SHA1 checksums to 'file' only (do not copy)
          -a file    Save SHA1 checksums to 'file'
          -A256 file Save SHA256 checksums to 'file' only (do not copy)
          -a256 file Save SHA256 checksums to 'file'
          -b size    Use 'size' kB as the block size (default: 512)
          -c         Do not copy file attributes (read-only, etc.)
          -C         Copy difficult files (like "System Volume Information")
          -d dir     Add another destination directory
          -e file    Write errors to 'file' and continue copying
          -f t,cs    Copy and check a single file against type t, checksum cs
          -g         Ignore read errors while copying
          -G         Ignore write errors while copying
          -H file    Save SHA256 checksums to 'file' only (do not copy)
          -h file    Save SHA256 checksums to 'file'
          -i         ISO mode.  Example:  tcopy -i J: DVDOutput.iso
          -j prog    Execute "prog" just before verification step
          -k         Do not calculate directory sizes
          -l         Copy ACLs for files/directories
          -L         Copy owner information for files/directories
          -M file    Save MD5 checksums to 'file' only (do not copy)
          -m file    Save MD5 checksums to 'file'
          -n num     Use 'num' buffers in multithreaded mode (default: 4)
          -o         Normalize the MD5/SHA files for easy comparison
          -p delay   Pause for 'delay' ms after every block (default: 0)
          -P speed   Pause after every block for a maximum of 'speed' kB/s
          -r         Re-read the written data and verify against the source
          -s         Copy files recursively
          -S         Copy files recursively and create target directory
          -t         Use separate reading and writing threads
          -u         Resume a partial copy (including hash files, if any)
          -U len     Resume a copy after trimming 'len' kB (default: 4)
          -v file    Write a verification MD5/SHA1 file after copying
          -V file    Write a verification file, ignoring -p/-P settings
          -w         Wait for media to become available
          -x files   Exclude the selected files/directories
          -X types   Exclude files of these types.  Ex: jdhsrceopt
          -y         Overwrite destination files without asking
          -z         Replace unreadable blocks with zeros

          --paste    Use files copied from explorer as the source (implies -s)
          --md5      Shortcut: -So -e errors.txt -m from.md5 -v to.md5
          --delete d Use long-paths to completely remove a directory "d"
          
    -A  Save the SHA1 checksums only without actually copying anything.

    -a  Save the SHA1 checksums to the specified file while copying.

    -A256  Save the SHA256 checksums only without actually copying anything.

    -a256  Save the SHA256 checksums to the specified file while copying.

    -b  Sets the number of kB to read at once.  Particularly large or small
        values don't really work very well.  I recommend sticking to
        the 16-256 range except for special circumstances.
        
    -c  Copies all of a file's attributes (read-only, system, hidden, etc)
        as well as the contents of the file.
        
    -C  Attempts to copy directories that are known to be annoying, such
        as "System Volume Information" and "lost+found".  The default
        behavior skips these directories to save the trouble of manually
        ignoring the errors they can cause.
        
    -d  Allows you to specify an additional directory in which copies of
        the source file should be placed.  Since this avoids a second read
        of the source media, it can save some time if, for example, you want
        to copy files from a DVD to several local directories.
        
    -e  This will write all errors encountered during execution to the log
        file specified instead of interrupting to ask the user what to do.
        A notice will be given after the copy process is complete if the
        error log contains any information.
        
    -f  This will copy one file and verify that its checksum is correct.
        checksum types are md5, sha1, sha256.  For example:
        tcopy -f md5,5c8eeebaa287a3be1f738a23ef1adb60 http://example.com/x.iso
        
    -g  Read errors (such as "access denied") will be ignored during the
        copy operation.  Files with read errors will be skipped.
        
    -G  Write errors (such as "out of space") will be ignored during the
        copy operation.  This may be useful in rare circumstances, but is
        not generally appropriate.

    -i  Process the source drive as a block device, creating an ISO file.
        For example:  TCopy.exe -i H: C:\DVD_Image.iso

    -j  Specifies a program to execute after the copy step but before the
        verification step.  I have only ever used it to run "clearcache"
        so that the read cache will not affect the verification step.
        Command line arguments are not currently supported.
    
    -k  Skip the initial directory size calculation.  This can also be done
        by pressing Control+K.  If you forego the size calculation, total
        ETR and percentages will be unavailable, but the copy will begin
        more quickly.

    -l  Copy the ACLs for the files.

    -L  Copy the owner information for the files.

    -M  Save the MD5 checksums only without actually copying anything.

    -m  Save the MD5 checksums to the specified file while copying.

    -n  This changes the number of buffers to use when using the "-t"
        option for multithreading purposes.  The total memory usage will
        be this number times the number of kilobytes specified with "-b"
        The more buffers, the more space is available for the reading
        thread to read while the writing thread is busy, or vice-versa.
        
    -o  Normalizes the paths in the MD5 or SHA1 output file, meaning
        that any part of the beginning of the path that matches the source
        (for copy) or target (for verify) path will be stripped.  This makes
        comparing the resulting files easier.

    -p  Use this option to pause for a number of milliseconds after each
        block of data is written.  This can be useful when continuous access
        to a device (such as a DVD drive) causes a machine to perform very
        slowly.  Experiment for the best results on a particular setup.

    -P  This is similar to the -p option except that the amount of time
        will be calculated to maintain a maximum transfer rate of the
        number of kB/sec given.

    -r  This will re-open the written data and check it against the source.
        This is *extremely* slow compared to using the "-v" option and is
        not usually recommended.

    -s  Perform a recursive copy (i.e. including all subdirectories).
    
    -S  Perform a recursive copy, but create the directory specified as 
        the source directory first, and copy into that directory.
    
    -t  Use multithreaded mode.  This will attempt to read and write
        simultaneously using a number of buffers (at least two).  Sometimes
        this can increase performance and sometimes not.  Multithreaded
        mode is not particularly well-tested and should be regarded as
        experimental.

    -u  This will automatically resume a copy that was previously
        interrupted.  It is slightly different from simply selecting
        "resume" during the copy in that it will also resume the MD5/SHA1
        file, if specified and present.  Normal resume without using "-u"
        will not recalculate a hash that was interrupted mid-transfer.

    -v  Using this option will, after the copy is complete, calculate an
        MD5 or SHA1 (depending on whether you used -m or -a) of the
        destination directory.  These files can be compared to determine
        whether the copy is perfect or if corruption occurred.  (Note that
        this will include all files in the target directory, so there may
        be some spurious extra lines in the new hash file if you were
        not copying to an empty directory).
        
    -V  This is identical to the -v option except that any artificial
        read delays set with -p or -P will be ignored during this pass.

    -w  This will automatically ignore any IOException errors while trying
        to open the source files and keep attempting to read them.  This
        allows you to run the TCopy command before putting the DVD in the
        drive.
        
    -x  Exclude this file/directory (or colon-separated list of them) from
        the copy/verify process.

    -X  Exclude any files or directories with attributes specified by
        characters in the following string.  See the Notes section for more
        information;

    -y  Automatically overwrite any target files that already exist.  If
        you do not use this option, you may specify overwrite for
        individual files or resume for the entire fileset.
        
    -z  Skips blocks of data that include unreadable areas, replacing them
        with null bytes.  This is probably best used with a block size
        (-b option) of a single block (or sector) of source media.

======================================================================

Examples:

To copy everything from the C: drive to the D: drive, with normalized MD5
verification using files "from.md5" and "to.md5", continuing even if there
are errors, and writing the error log to "errors.txt":

    tcopy -so -e D:\errors.txt -m D:\from.md5 -v D:\to.md5 C:\* D:\

To copy a DVD in G: with a 1 ms pause in between blocks, waiting for the user
to put the DVD in the drive before continuing:

    tcopy -w -p 1 -s G:\*.* C:\TargetDir

To copy one directory to another, creating comparison MD5 files:

    tcopy -s -m from.md5 -v to.md5 C:\SourceDir D:\TargetDir

To resume the transfer in the previous example after it was interrupted:

    tcopy -u -s -m from.md5 -v to.md5 C:\SourceDir D:\TargetDir

To attempt a large (2 MB) buffer multithreaded transfer with 10 buffers:

    tcopy -s -t -n 10 -b 2048 C:\SourceDir D:\TargetDir

======================================================================
Notes:

Holding "Control" while watching the progress of a copy will display the
current speed in MB/sec at the right.

Pressing "Control-P" while copying will close all open file handles and
pause any file transfer in progress.  Press "Enter" to re-open the handles
and resume the copy.  Pressing Control-C when paused in this way will
guarantee a safe closing of the files, which can be useful for moving a
CD/DVD from a problematic drive to another drive and then resuming the
transfer.  Running the same TCopy command will give you the option of
resuming files (use -u if you are also calculating hash files).

The following characters are valid for a string following the -X option:

    j - Reparse Point (junction, symlink)
    d - Directory
    r - Read Only
    s - System
    h - Hidden
    c - Compressed
    e - Encrypted
    o - Offline
    p - Sparse
    t - Temporary

======================================================================

"TCopy" is copyright 2012 by Eric VanHeest (edv_ws@vanheest.org). 
Feel free to modify this program to suit your needs.

