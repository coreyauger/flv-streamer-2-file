using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace FlvStream2File
{

    enum AMFTypes
    {
        Number = 0x00, // (Encoded as IEEE 64-bit double-precision floating point number)
        Boolean = 0x01, // (Encoded as a single byte of value 0x00 or 0x01)
        String = 0x02, //(ASCII encoded)
        Object = 0x03, // (Set of key/value pairs)
        Null = 0x05,
        Array = 0x08,
        End = 0x09
    }

    public class AmfEncoderDecoder
    {
        private int _readHead = 0;

        private IList<IDictionary<string, object>> _obj = new List<IDictionary<string, object>>();
        public IList<string> DebugOrder { get; private set; }


        public AmfEncoderDecoder()
        {
            DebugOrder = new List<string>();
        }

        private string DecodeKey(byte[] buff)
        {
            // get length of string name
            byte[] flip = new byte[sizeof(short)];
            flip[0] = buff[_readHead++];
            flip[1] = buff[_readHead++];
            ushort klen = BitConverter.ToUInt16(flip.Reverse().ToArray(), 0);
            string name = System.Text.Encoding.Default.GetString(buff, _readHead, klen);
            _readHead += klen;
            return name;
        }

        private object DecodeVal(byte[] buff)
        {
            
            byte type = buff[_readHead++];
            AMFTypes amfType = (AMFTypes)Enum.ToObject(typeof(AMFTypes), (int)type);
            switch (amfType)
            {
                                                  
                case AMFTypes.String:
                    // read string size..
                    return DecodeKey(buff);
                case AMFTypes.Number:
                    byte[] flip = new byte[sizeof(double)];
                    Buffer.BlockCopy(buff, _readHead, flip, 0, flip.Length);
                    double num = BitConverter.ToDouble(flip.Reverse().ToArray(), 0);
                    _readHead += sizeof(double);
                    return num;
                case AMFTypes.Boolean:
                    byte b = buff[_readHead++];
                    return b;
                case AMFTypes.End:
                    return null;
                default:
                    // could make this a real Amf Enc/Dec but it is not required for my needs..
                    throw new MissingMethodException();  
            }           
        }

        private byte[] EncodeKey(string key)
        {
            byte[] ret = new byte[2 + key.Length];      // 2 for the size at the front
            UInt16 strSize = (UInt16)key.Length;
            byte[] strSizeb = BitConverter.GetBytes(strSize);
            Array.Reverse(strSizeb);
            Buffer.BlockCopy(strSizeb, 0, ret, 0, strSizeb.Length);
            Buffer.BlockCopy(System.Text.Encoding.ASCII.GetBytes(key), 0, ret, 2, key.Length);
            return ret;
        }
        private byte[] EncodeVal(object val)
        {
            // we only deal with a small subset of object types...
            if (val is double)
            {
                double num = (double)val;
                byte[] ret = new byte[1 + sizeof(double)];  // type + double
                ret[0] = (byte)AMFTypes.Number;
                byte[] numbits = BitConverter.GetBytes(num);
                Array.Reverse(numbits);
                Buffer.BlockCopy(numbits, 0, ret, 1, numbits.Length);
                return ret;
            }
            else if (val is string)
            {
                string str = val as string;
                byte[] ret = new byte[3 + str.Length];      // 2 for the size at the front
                ret[0] = (byte)AMFTypes.String;
                UInt16 strSize = (UInt16)str.Length;
                byte[] strSizeb = BitConverter.GetBytes(strSize);
                Array.Reverse(strSizeb);
                Buffer.BlockCopy(strSizeb, 0, ret, 1, strSizeb.Length);
                Buffer.BlockCopy(System.Text.Encoding.ASCII.GetBytes(str), 0, ret, 3, str.Length);
                return ret;
            }
            else if (val is byte)
            {
                byte bit = (byte)val;
                byte[] ret = new byte[2];
                ret[0] = (byte)AMFTypes.Boolean;
                ret[1] = bit;
                return ret;
            }
            else
            {
                Debug.Write(string.Format("Unknown Value type: {0}\n", val.GetType().Name));
            }
            return null;

        }

        public byte[] EncodeMetaData(IDictionary<string, object> meta, IList<string> order = null)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                string onMetaData = "onMetaData";
                ms.WriteByte((byte)AMFTypes.String);
                UInt16 strSize = (UInt16)onMetaData.Length;
                byte[] strSizeb = BitConverter.GetBytes(strSize);
                Array.Reverse(strSizeb);
                ms.Write(strSizeb, 0, strSizeb.Length);
                ms.Write(System.Text.Encoding.ASCII.GetBytes(onMetaData), 0, onMetaData.Length);    // add str "onMetaData"
                ms.WriteByte((byte)AMFTypes.Array);     // array type..
                byte[] asize = BitConverter.GetBytes(meta.Keys.Count);
                Array.Reverse(asize);
                ms.Write(asize, 0, asize.Length);    // add the array length
                if (order != null)
                {
                    foreach (string key in order)
                    {
                        object val = meta[key];
                        if (!string.IsNullOrWhiteSpace(key) && val != null)
                        {
                            byte[] keyBytes = EncodeKey(key);
                            ms.Write(keyBytes, 0, keyBytes.Length);
                            byte[] valBytes = EncodeVal(val);
                            ms.Write(valBytes, 0, valBytes.Length);
                        }
                    }
                }
                else
                {
                    foreach (string key in meta.Keys)
                    {   // encode all the key val pairs...
                        object val = meta[key];
                        if (!string.IsNullOrWhiteSpace(key) && val != null)
                        {                            
                            byte[] keyBytes = EncodeKey(key);
                            ms.Write(keyBytes, 0, keyBytes.Length);
                            byte[] valBytes = EncodeVal(val);
                            ms.Write(valBytes, 0, valBytes.Length);
                        }
                    }
                }
                ms.WriteByte((byte)0x0);    // Not totally sure why i need this...
                ms.WriteByte((byte)0x0);    // Not totally sure why i need this...
                ms.WriteByte((byte)AMFTypes.End);    // End of array..
                return ms.GetBuffer().Take((int)ms.Length).ToArray();
            }
        }

        public IDictionary<string, object> DecodeMetaData(byte[] buff)
        {
            IDictionary<string, object> keyval = new Dictionary<string, object>();
            _readHead = 0;
            // get the onMetadata 
            string onMeta = DecodeVal(buff) as string;
            // read array type
            byte type = buff[_readHead++];
            Debug.Assert(type == (byte)AMFTypes.Array || type == (byte)AMFTypes.Object);
            if(type == (byte)AMFTypes.Array)
            {
                byte[] alen = new byte[sizeof(int)];
                Buffer.BlockCopy(buff, _readHead, alen, 0, alen.Length);
                _readHead += alen.Length;
                int arrayLen = BitConverter.ToInt32(alen.Reverse().ToArray(), 0);
                Debug.Write(string.Format("onMetaData Array Len: {0}\n", arrayLen));
            }
            else if(type == (byte)AMFTypes.Object)
            {
                // Do nothing. Just fine.
                Debug.Write("onMetaData isn't an Array but Object!\n");
            }
            else
            {
                //TODO: Maybe throw a exception
            }
            while(_readHead <= buff.Length - 1)
            {
                string key = DecodeKey(buff);
                DebugOrder.Add(key);
                object val = DecodeVal(buff);
                Debug.Write(string.Format("Encode key {0} => {1}\n", key,val));                
                //keyval.Add(new KeyValuePair<string, object>(key, val));  // Have some files with duplicate keys in Meta..
                keyval[key] = val;                
            }

            return keyval;
        }
    }
}
