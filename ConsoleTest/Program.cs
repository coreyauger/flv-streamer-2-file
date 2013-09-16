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
            if (args.Length != 2)
            {
                Console.WriteLine("Usage:\nConsoleTest.exe \"infile.flv\" \"outfile.flv\"");
                return;
            }
            // We just test from a file source 
            FlvStream2FileWriter stream2File = new FlvStream2FileWriter(args[1]);
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
                        if (bytesRead != buffer.Length)
                        {
                            if (bytesRead == 0)
                            {
                                fs.Dispose();
                                fs = null;
                                return;
                            }
                            stream2File.Write(buffer.Take(bytesRead).ToArray());
                        }
                        else
                        {
                            stream2File.Write(buffer);
                        }
                        fs.BeginRead(buffer, 0, BUF_LEN, callback, null);
                        Console.Write(".");
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
                    System.Threading.Thread.Sleep(125);
                }
            }
            catch (Exception ex)
            {   // Set a break point here so you can debug from VS
                Console.Write(ex.Message);
            }
            finally
            {   // 
                stream2File.FinallizeFile();

                Console.WriteLine("");
                Console.WriteLine(string.Format( "Duraction: {0}s", stream2File.MaxTimeStamp/1000.0));
                Console.WriteLine(string.Format("Num Audio: {0}", stream2File.NumAudio));
                Console.WriteLine(string.Format("Num Video: {0}", stream2File.NumVideo));
            }
        }
    }
}
