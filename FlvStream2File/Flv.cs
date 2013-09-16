using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.IO;

namespace FlvStream2File
{

    enum TagType { NONE =0x0, AUDIO = 0x8, VIDEO = 0x9, META = 0x12 }

    public class Tag
    {
        public byte[] PrevSize = new byte[sizeof(int)];
        public byte TagType = 0;
        public int TagSize = 0;
        public int TimeStamp;
        public byte TimeExtra = 0;
        public byte[] StreamId = new byte[3];
        public byte[] Data = null;

        public byte[] GetBytes()
        {
            byte[] tag = new byte[15];
            byte[] psize = new byte[4];
            Buffer.BlockCopy(PrevSize, 0, psize, 0, PrevSize.Length); // copy prev size (4 bytes)
            Array.Reverse(psize);
            Buffer.BlockCopy(psize, 0, tag, 0, psize.Length); // copy prev size (4 bytes)
            tag[4] = TagType;                                       // type (1 byte) 5
            
            byte[] tagLenBigEn = new byte[3];
            Buffer.BlockCopy(BitConverter.GetBytes(this.TagSize),0,tagLenBigEn, 0, tagLenBigEn.Length);
            Array.Reverse(tagLenBigEn);
            Buffer.BlockCopy(tagLenBigEn, 0, tag, 5, 3); // size (3 bytes) = 8
            
            byte[] timMsBigEn = new byte[3];
            Buffer.BlockCopy(BitConverter.GetBytes(this.TimeStamp), 0, timMsBigEn, 0, timMsBigEn.Length);
            Array.Reverse(timMsBigEn);
            Buffer.BlockCopy(timMsBigEn, 0, tag, 8, 3); // size (3 bytes) = 8
            
            tag[11] = TimeExtra; // tm extra (1 bytes) = 12
            Buffer.BlockCopy(StreamId, 0, tag, 12, 3); // tm (3 bytes) = 15    
            return tag;
        }
    };

    public class FlvHeader
    {
        public const int HEADER_SIZE    = 9;
        public byte[] Header            = new byte[HEADER_SIZE];
        public Tag Tag = new Tag();
        public IDictionary<string, object> Meta = new Dictionary<string,object>();
        internal IList<string> DebugOrder = null;
        
        

        public byte[] GetBytes()
        {
            using (MemoryStream ms = new MemoryStream())
            {
                ms.Write(Header, 0, Header.Length);

                if (!this.Meta.ContainsKey("duration"))
                {   // assure that we add space for the duration key
                    this.Meta["duration"] = 0.0;    // place holder..  
                }
                if (!this.Meta.ContainsKey("lasttimestamp"))
                {   // assure that we add space for the lasttimestamp
                    this.Meta["lasttimestamp"] = 0.0;    // place holder..  
                }       

                byte[] meta = new AmfEncoderDecoder().EncodeMetaData(this.Meta, this.DebugOrder);
                Tag.TagSize = meta.Length;
                
                byte[] metahead = Tag.GetBytes();
                ms.Write(metahead, 0, metahead.Length);
                
                //ms.Write(Tag.Data, 0, Tag.Data.Length);
                ms.Write(meta, 0, meta.Length);

                return ms.GetBuffer().Take((int)ms.Position).ToArray();
            }
        }
    }


}
