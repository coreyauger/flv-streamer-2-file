using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace FlvStream2File
{
    public class FlvStream2FileWriter : IDisposable
    {
        private FileStream _fs = null;
        public string FileName { get; private set; }

        private int _readPos = 0;
        private int _restOfTag = 0;

        private bool hasHeader = false;
        const int BUF_LEN = 2048;        // NOTE: this MUST be large enough to get header + meta data...
        private MemoryStream _ms = new MemoryStream(BUF_LEN);

        public int NumVideo { get; private set; }
        public int NumAudio { get; private set; }
        public int MaxTimeStamp { get; private set; }

        private FlvHeader _header = new FlvHeader();
        private Tag _curTag = new Tag();

        internal byte[] _prevSize = new byte[sizeof(int)];

        

        public FlvStream2FileWriter(string filename)
        {
            this.FileName = filename;
            this.MaxTimeStamp = int.MinValue;
        }

        #region IDisposeable
        // Dispose() calls Dispose(true)
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        ~FlvStream2FileWriter() 
        {
            // Finalizer calls Dispose(false)
            Dispose(false);
        }
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                // free managed resources
                if (_fs != null)
                {
                    _fs.Dispose();
                    _fs = null;
                }
                if (_ms != null)
                {
                    _ms.Dispose();
                    _ms = null;
                }
            }
            // free native resources if there are any.
            //if (nativeResource != IntPtr.Zero)
            //{
            //    Marshal.FreeHGlobal(nativeResource);
            //    nativeResource = IntPtr.Zero;
            //}
        }

        #endregion


        public int Write(byte[] buffer)
        {
            
            if (_fs == null)
            {
                _fs = new FileStream(this.FileName, FileMode.Create);
            }
            if (!hasHeader)
            {   // header is 9 bytes                
                GrabHeader(buffer);
            }
            else if (_curTag.TagSize > 0)
            {
                ReadTagData(buffer);
            }
            else
            {   // lets start crushing tags here..
                ParseTag(buffer);
            }
      
            return 0;
        }

        private void ReadTagData(byte[] buffer)
        {
            int toRead = Math.Min(buffer.Length, (_curTag.TagSize - _readPos));
            _fs.Write(buffer, 0, toRead);
            _readPos += toRead;
            if (_readPos == _curTag.TagSize)
            {   // we have finished with the tag... so lets reset
                _curTag.TagType = 0;
                _curTag.TagSize = -1;
                _readPos = 0;
                Write(buffer.Skip(toRead).ToArray());
            }
        }

        private void ParseTag(byte[] buffer)
        {
            _ms.Write(buffer, 0, buffer.Length);
            if (_ms.Position >= BUF_LEN) // have we filled enough?
            {
                long dataLen = _ms.Position;
                _ms.Position = 0;
                _ms.Read(_curTag.PrevSize, 0, sizeof(int));  
                Array.Reverse(_curTag.PrevSize);
                Debug.Write(string.Format("Prev Tag Len: {0}\n", BitConverter.ToInt32( _curTag.PrevSize, 0)-11 ));
                _curTag.TagType = (byte)_ms.ReadByte();  
                Debug.Write(string.Format("Tag Type: {0}\n", _curTag.TagType));
                if (_curTag.TagType == (byte)TagType.AUDIO)
                {
                    this.NumAudio++;
                }
                else if (_curTag.TagType == (byte)TagType.VIDEO)
                {
                    this.NumVideo++;
                }
                else
                {
                    Debug.Write("Unknown Tag Type (something is wrong)");
                    throw new Exception("Unknown Tag");     // TODO: throw discontinuity exception...
                }
                
                byte[] tagSize = new byte[sizeof(int)];
                _ms.Read(tagSize, 1, 3);     // read 24 bit body length.  Note: Body length + 11 is the entire TAG size	
                tagSize[0] = 0;
                Array.Reverse(tagSize);
                _curTag.TagSize = BitConverter.ToInt32(tagSize, 0);
                Debug.Write(string.Format("Tag Size: {0}\n", _curTag.TagSize));
                
                byte[] tagMs = new byte[sizeof(int)];
                _ms.Read(tagMs, 1, 3);     // read 24 bit body length.  Note: Body length + 11 is the entire TAG size	
                tagMs[0] = 0;
                Array.Reverse(tagMs);
                _curTag.TimeStamp = BitConverter.ToInt32(tagMs, 0);
                Debug.Write(string.Format("{0} ms\n", _curTag.TimeStamp));
                this.MaxTimeStamp = Math.Max(this.MaxTimeStamp, _curTag.TimeStamp);

                _curTag.TimeExtra = (byte)_ms.ReadByte();

                _ms.Read(_curTag.StreamId, 0, _curTag.StreamId.Length);
                byte[] rest = _ms.GetBuffer().Skip((int)_ms.Position).Take((int)(dataLen - _ms.Position)).ToArray();
                _ms.Position = 0;

                byte[] tagHead = _curTag.GetBytes();
                _fs.Write(tagHead, 0, tagHead.Length);

                Write(rest);
            }
        }        

        private void GrabHeader(byte[] buffer)
        {
            _ms.Write(buffer, 0, buffer.Length);
            if (_ms.Position >= BUF_LEN)
            {// have we filled enough?
                _ms.Position = 0;
                _ms.Read(_header.Header, 0, _header.Header.Length);  // read the header.                
                _ms.Read(_header.Tag.PrevSize, 0, sizeof(int));  // read prev tag len (should be 0).
                _header.Tag.TagType = (byte)_ms.ReadByte();  // Tag type (0x12 for Meta)
                Debug.Write(string.Format("Tag Type: {0}\n", _header.Tag.TagType));
                Debug.Assert(_header.Tag.TagType == (byte)TagType.META);

                byte[] tagSize = new byte[sizeof(int)];
                _ms.Read(tagSize, 1, 3);     // read 24 bit body length.  Note: Body length + 11 is the entire TAG size	
                tagSize[0] = 0;
                Array.Reverse(tagSize);
                _header.Tag.TagSize = BitConverter.ToInt32(tagSize, 0);
                Debug.Write(string.Format("Tag Size: {0}\n", _header.Tag.TagSize));
               
                byte[] tagMs = new byte[sizeof(int)];
                _ms.Read(tagMs, 1, 3);     // read 24 bit body length.  Note: Body length + 11 is the entire TAG size	
                tagMs[0] = 0;
                Array.Reverse(tagMs);
                _header.Tag.TimeStamp = BitConverter.ToInt32(tagMs, 0);
                Debug.Write(string.Format("Tag TimeStamp: {0}\n", _header.Tag.TimeStamp));

                _header.Tag.TimeExtra = (byte)_ms.ReadByte();
                _ms.Read(_header.Tag.StreamId, 0, _header.Tag.StreamId.Length);
                _header.Tag.Data = new byte[_header.Tag.TagSize];
                _ms.Read(_header.Tag.Data, 0, _header.Tag.Data.Length);

                _header.Meta = new AmfEncoderDecoder().DecodeMetaData(_header.Tag.Data);

                byte[] header =_header.GetBytes();
                _fs.Write(header, 0, header.Length);

                // Now that we have what we came for.. lets start writing the file out Tag by Tag...
                hasHeader = true;
                _readPos = 0;
                byte[] rest = _ms.GetBuffer().Skip((int)_ms.Position).Take((int)(_ms.Length - _ms.Position)).ToArray();
                _ms.Position = 0;
                Write(rest);
            }
        }


        public void FinallizeFile()
        {
            _fs.Dispose();
            _ms.Dispose();

            // do meta data correction and overwrite ...
            using (FileStream fs = new FileStream(this.FileName, FileMode.Open))
            {
                _header.Meta["duration"] = this.MaxTimeStamp / 1000.0; // duration is in seconds..
                byte[] header = _header.GetBytes();
                fs.Write(header, 0, header.Length);
            }
        }
       

    }
}
