/*
 * Copyright (C) 2008, Shawn O. Pearce <spearce@spearce.org>
 * Copyright (C) 2009, Henon <meinrad.recheis@gmail.com>
 *
 * All rights reserved.
 *
 * Redistribution and use in source and binary forms, with or
 * without modification, are permitted provided that the following
 * conditions are met:
 *
 * - Redistributions of source code must retain the above copyright
 *   notice, this list of conditions and the following disclaimer.
 *
 * - Redistributions in binary form must reproduce the above
 *   copyright notice, this list of conditions and the following
 *   disclaimer in the documentation and/or other materials provided
 *   with the distribution.
 *
 * - Neither the name of the Git Development Community nor the
 *   names of its contributors may be used to endorse or promote
 *   products derived from this software without specific prior
 *   written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND
 * CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES,
 * INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES
 * OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
 * ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR
 * CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
 * SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT
 * NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
 * CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT,
 * STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
 * ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF
 * ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System.IO;
using GitSharp.Util;
using System.Text;
using System;
namespace GitSharp.TreeWalk
{


    /**
     * Walks a working directory tree as part of a {@link TreeWalk}.
     * <p>
     * Most applications will want to use the standard implementation of this
     * iterator, {@link FileTreeIterator}, as that does all IO through the standard
     * <code>java.io</code> package. Plugins for a Java based IDE may however wish
     * to create their own implementations of this class to allow traversal of the
     * IDE's project space, as well as benefit from any caching the IDE may have.
     * 
     * @see FileTreeIterator
     */
    public abstract class WorkingTreeIterator : AbstractTreeIterator
    {
        /** An empty entry array, suitable for {@link #init(Entry[])}. */
        internal static Entry[] EOF = { };

        /** Size we perform file IO in if we have to read and hash a file. */
        private static int BUFFER_SIZE = 2048;

        /** The {@link #idBuffer()} for the current entry. */
        private byte[] contentId;

        /** Index within {@link #entries} that {@link #contentId} came from. */
        private int contentIdFromPtr;

        /** Buffer used to perform {@link #contentId} computations. */
        private byte[] contentReadBuffer;

        /** Digest computer for {@link #contentId} computations. */
        private MessageDigest contentDigest;

        /** File name character encoder. */
        private Encoding nameEncoder;

        /** List of entries obtained from the subclass. */
        private Entry[] entries;

        /** Total number of entries in {@link #entries} that are valid. */
        private int entryCnt;

        /** Current position within {@link #entries}. */
        private int ptr;

        /** Create a new iterator with no parent. */
        internal WorkingTreeIterator()
            : base()
        {

            nameEncoder = Constants.CHARSET;
        }

        /**
         * Create a new iterator with no parent and a prefix.
         * <p>
         * The prefix path supplied is inserted in front of all paths generated by
         * this iterator. It is intended to be used when an iterator is being
         * created for a subsection of an overall repository and needs to be
         * combined with other iterators that are created to run over the entire
         * repository namespace.
         *
         * @param prefix
         *            position of this iterator in the repository tree. The value
         *            may be null or the empty string to indicate the prefix is the
         *            root of the repository. A trailing slash ('/') is
         *            automatically appended if the prefix does not end in '/'.
         */
        internal WorkingTreeIterator(string prefix)
            : base(prefix)
        {

            nameEncoder = Constants.CHARSET;
        }

        /**
         * Create an iterator for a subtree of an existing iterator.
         * 
         * @param p
         *            parent tree iterator.
         */
        internal WorkingTreeIterator(WorkingTreeIterator p) :
            base(p)
        {
            nameEncoder = p.nameEncoder;
        }

        public override byte[] idBuffer()
        {
            if (contentIdFromPtr == ptr)
                return contentId;
            switch (mode & 61440)
            {
                case 32768: /* normal files */
                    contentIdFromPtr = ptr;
                    return contentId = idBufferBlob(entries[ptr]);
                case 40960: /* symbolic links */
                    // Windows does not support symbolic links, so we should not
                    // have reached this particular part of the walk code.
                    //
                    return zeroid;
                case 57344: /* gitlink */
                    // TODO: Support obtaining current HEAD SHA-1 from nested repository
                    //
                    return zeroid;
            }
            return zeroid;
        }

        private void initializeDigest()
        {
            if (contentDigest != null)
                return;

            if (parent == null)
            {
                contentReadBuffer = new byte[BUFFER_SIZE];
                contentDigest = Constants.newMessageDigest();
            }
            else
            {
                WorkingTreeIterator p = (WorkingTreeIterator)parent;
                p.initializeDigest();
                contentReadBuffer = p.contentReadBuffer;
                contentDigest = p.contentDigest;
            }
        }

        private static byte[] digits = { (byte)'0', (byte)'1', (byte)'2', (byte)'3', (byte)'4', (byte)'5', (byte)'6', (byte)'7', (byte)'8', (byte)'9' };

        private static byte[] hblob = Constants.encodedTypeString(Constants.OBJ_BLOB);

        private byte[] idBufferBlob(Entry e)
        {
            try
            {
                FileStream @is = e.openInputStream();
                if (@is == null)
                    return zeroid;
                try
                {
                    initializeDigest();

                    contentDigest.Reset();
                    contentDigest.Update(hblob);
                    contentDigest.Update((byte)' ');

                    long blobLength = e.getLength();
                    long sz = blobLength;
                    if (sz == 0)
                    {
                        contentDigest.Update((byte)'0');
                    }
                    else
                    {
                        int bufn = contentReadBuffer.Length;
                        int p = bufn;
                        do
                        {
                            contentReadBuffer[--p] = digits[(int)(sz % 10)];
                            sz /= 10;
                        } while (sz > 0);
                        contentDigest.Update(contentReadBuffer, p, bufn - p);
                    }
                    contentDigest.Update((byte)0);

                    for (; ; )
                    {
                        int r = @is.Read(contentReadBuffer, 0, contentReadBuffer.Length); // was: read(contentReadBuffer) in java
                        if (r <= 0)
                            break;
                        contentDigest.Update(contentReadBuffer, 0, r);
                        sz += r;
                    }
                    if (sz != blobLength)
                        return zeroid;
                    return contentDigest.Digest();
                }
                finally
                {
                    try
                    {
                        @is.Close();
                    }
                    catch (IOException)
                    {
                        // Suppress any error related to closing an input
                        // stream. We don't care, we should not have any
                        // outstanding data to flush or anything like that.
                    }
                }
            }
            catch (IOException)
            {
                // Can't read the file? Don't report the failure either.
                //
                return zeroid;
            }
        }

        public override int idOffset()
        {
            return 0;
        }

        public override bool first()
        {
            return ptr == 0;
        }

        public override bool eof()
        {
            return ptr == entryCnt;
        }

        public override void next(int delta)
        {
            ptr += delta;
            if (!eof())
                parseEntry();
        }

        public override void back(int delta)
        {
            ptr -= delta;
            parseEntry();
        }

        private void parseEntry()
        {
            Entry e = entries[ptr];
            mode = e.getMode().Bits;

            int nameLen = e.encodedNameLen;
            while (pathOffset + nameLen > path.Length)
                growPath(pathOffset);
            Array.Copy(e.encodedName, 0, path, pathOffset, nameLen);
            pathLen = pathOffset + nameLen;
        }

        /**
         * Get the byte Length of this entry.
         *
         * @return size of this file, in bytes.
         */
        public long getEntryLength()
        {
            return current().getLength();
        }

        /**
         * Get the last modified time of this entry.
         *
         * @return last modified time of this file, in milliseconds since the epoch
         *         (Jan 1, 1970 UTC).
         */
        public long getEntryLastModified()
        {
            return current().getLastModified();
        }

        private static Comparison<Entry> ENTRY_CMP = new Comparison<Entry>((o1, o2) =>
        {
            byte[] a = o1.encodedName;
            byte[] b = o2.encodedName;
            int aLen = o1.encodedNameLen;
            int bLen = o2.encodedNameLen;
            int cPos;

            for (cPos = 0; cPos < aLen && cPos < bLen; cPos++)
            {
                int cmp = (a[cPos] & 0xff) - (b[cPos] & 0xff);
                if (cmp != 0)
                    return cmp;
            }

            if (cPos < aLen)
                return (a[cPos] & 0xff) - lastPathChar(o2);
            if (cPos < bLen)
                return lastPathChar(o1) - (b[cPos] & 0xff);
            return lastPathChar(o1) - lastPathChar(o2);
        }
        );

        static int lastPathChar(Entry e)
        {
            return e.getMode() == FileMode.Tree ? (byte)'/' : (byte)'\0';
        }

        /**
         * Constructor helper.
         *
         * @param list
         *            files in the subtree of the work tree this iterator operates
         *            on
         */
        internal void init(Entry[] list)
        {
            // Filter out nulls, . and .. as these are not valid tree entries,
            // also cache the encoded forms of the path names for efficient use
            // later on during sorting and iteration.
            //
            entries = list;
            int i, o;

            for (i = 0, o = 0; i < entries.Length; i++)
            {
                Entry e = entries[i];
                if (e == null)
                    continue;
                string name = e.getName();
                if (".".Equals(name) || "..".Equals(name))
                    continue;
                if (".git".Equals(name))
                    continue;
                if (i != o)
                    entries[o] = e;
                e.encodeName(nameEncoder);
                o++;
            }
            entryCnt = o;
            Array.Sort(entries, ENTRY_CMP); // was Arrays.sort(entries, 0, entryCnt, ENTRY_CMP) in java

            contentIdFromPtr = -1;
            ptr = 0;
            if (!eof())
                parseEntry();
        }

        /**
         * Obtain the current entry from this iterator.
         * 
         * @return the currently selected entry.
         */
        internal Entry current()
        {
            return entries[ptr];
        }

        /** A single entry within a working directory tree. */
        public abstract class Entry
        {
            public byte[] encodedName;

            public int encodedNameLen
            {
                get
                {
                    return encodedName.Length;
                }
            }

            public void encodeName(Encoding enc)
            {
                byte[] b;
                try
                {
                    encodedName = enc.GetBytes(getName());
                }
                catch (EncoderFallbackException e)
                {
                    // This should so never happen.
                    throw new Exception("Unencodeable file: " + getName());
                }

            }

            public override string ToString()
            {
                return getMode().ToString() + " " + getName();
            }

            /**
             * Get the type of this entry.
             * <p>
             * <b>Note: Efficient implementation required.</b>
             * <p>
             * The implementation of this method must be efficient. If a subclass
             * needs to compute the value they should cache the reference within an
             * instance member instead.
             * 
             * @return a file mode constant from {@link FileMode}.
             */
            public abstract FileMode getMode();

            /**
             * Get the byte Length of this entry.
             * <p>
             * <b>Note: Efficient implementation required.</b>
             * <p>
             * The implementation of this method must be efficient. If a subclass
             * needs to compute the value they should cache the reference within an
             * instance member instead.
             * 
             * @return size of this file, in bytes.
             */
            public abstract long getLength();

            /**
             * Get the last modified time of this entry.
             * <p>
             * <b>Note: Efficient implementation required.</b>
             * <p>
             * The implementation of this method must be efficient. If a subclass
             * needs to compute the value they should cache the reference within an
             * instance member instead.
             *
             * @return time since the epoch (in ms) of the last change.
             */
            public abstract long getLastModified();

            /**
             * Get the name of this entry within its directory.
             * <p>
             * Efficient implementations are not required. The caller will obtain
             * the name only once and cache it once obtained.
             * 
             * @return name of the entry.
             */
            public abstract string getName();

            /**
             * Obtain an input stream to read the file content.
             * <p>
             * Efficient implementations are not required. The caller will usually
             * obtain the stream only once per entry, if at all.
             * <p>
             * The input stream should not use buffering if the implementation can
             * avoid it. The caller will buffer as necessary to perform efficient
             * block IO operations.
             * <p>
             * The caller will close the stream once complete.
             * 
             * @return a stream to read from the file.
             * @throws IOException
             *             the file could not be opened for reading.
             */
            public abstract FileStream openInputStream();
        }
    }
}
