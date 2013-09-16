using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

using FlvStream2File;

namespace ConsoleTest
{
    class Program
    {
        
        static void Main(string[] args)
        {
            // We just test from a file source 
            FlvStream2FileWriter stream2File = new FlvStream2FileWriter("C:\\tmp\\out.flv");
            try
            {
                FileStream fs = new FileStream(args[0], FileMode.Open);
                int BUF_LEN = 4096;
                byte[] buffer = new byte[BUF_LEN];
                AsyncCallback callback = null;
                callback = ar =>
                {
                    try
                    {
                        // Call EndRead.
                        int bytesRead = fs.EndRead(ar);

                        // Process the bytes here.                    
                        stream2File.Write(buffer);
                        if (bytesRead < BUF_LEN)
                        {
                            Console.Write("Last read??");
                        }
                        fs.BeginRead(buffer, 0, BUF_LEN, callback, null);
                    }
                    catch (Exception e)
                    {   // just close and return for now on error...
                        fs.Dispose();
                        fs = null;
                        return;
                    }
                };
                fs.BeginRead(buffer, 0, BUF_LEN, callback, null);
                while (fs != null)
                {   // this could be better ;)
                    System.Threading.Thread.Sleep(250);
                }
            }
            catch (Exception ex)
            {   // Set a break point here so you can debug from VS
                Console.Write(ex.Message);
            }
            finally
            {   // 
                stream2File.FinallizeFile();
            }
        }
    }
}
