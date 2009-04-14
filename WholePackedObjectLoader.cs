﻿/*
 * Copyright (C) 2008, Shawn O. Pearce <spearce@spearce.org>
 * Copyright (C) 2008, Kevin Thompson <kevin.thompson@theautomaters.com>
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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Gitty.Core.Exceptions;

namespace Gitty.Core
{
    [Complete]
    public class WholePackedObjectLoader : PackedObjectLoader
    {
        public WholePackedObjectLoader(WindowCursor curs, PackFile pr,
                long dataOffset, long objectOffset, ObjectType type, int size)
            : base(curs, pr, dataOffset, objectOffset)
        {

            this._objectType = type;
            this._objectSize = size;
        }

        public override ObjectId GetDeltaBase()
        {
            return null;
        }

        public override byte[] CachedBytes
        {
            get
            {
                if (this.ObjectType != ObjectType.Commit)
                {
                    UnpackedObjectCache.Entry cache = pack.ReadCache(this.DataOffset);
                    if (cache != null)
                    {
                        curs.Release();
                        return cache.Data;
                    }
                }

                try
                {
                    // might not should be down converting this.Size
                    byte[] data = pack.Decompress(this.DataOffset, (int)this.Size, curs);
                    curs.Release();
                    if (this.ObjectType != ObjectType.Commit)
                        pack.SaveCache(this.DataOffset, data, this.ObjectType);
                    return data;
                }
                catch (FormatException fe)
                {
                    throw new CorruptObjectException(this.Id, "bad stream", fe);
                }

            }
        }

        public override ObjectType RawType
        {
            get
            {
                return this.ObjectType;
            }
        }

        public override long RawSize
        {
            get
            {
                return this.Size;
            }
        }
    }
}