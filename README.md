flv-streamer-2-file
===================

Project takes and FLV stream coming in from a "raw" source, and could be at any point in the "live" stream.  
It then saves the FLV to disk and corrects Metadata to allow for seek operations on stream close.

Is this an FLV Metadata injector?  Yes.  But this one has been written to address some specific needs.  Also I think it could be more useful than a lot of the other ones that are out there.  Here are some of the major differences:
* Written entirely in C# (.net)
* Avoids usage of “unsafe” code.
* Handles arbitrary file sizes and streams.
* Memory efficient.
* Small code base
* And of course is open source 

## Overview
This code was developed to accomplish a specific task, but can be adapted to do a number of things to an FLV file.

My goal was to take an FLV stream … and save that stream to disk while still being able to seek inside the file.  This means that you can drop out of the stream at any time and the file will contain the portion of content up to that point.  The file on disk will contain the total duration and thus be seekable (tested with VLC).

Here is the general idea.  I store the FLV header and meta data.  I make sure that there are “placeholder” values for the meta data I want.  I then write this out to the head of the file we are saving.  I then stream the tag data to disk.  I also keep a count of audio and video packets… you could also choose to alter timestamps ect…

When the stream errors our or has reached the end.  I close the file and then go back in to the head to alter placeholder values.  In my case the “duration” or total time of the file in seconds will allow players to now seek in the file.

## Usage
The solution consists of 2 projects:
* FlvStream2File (class library)
* ConsoleTest (example app)

Compile the FlvStream2File.  This is the only project required to use in your application.
To use the test app.. simply compile and in a console window you can run:

`>ConsoleTest.exe “path to infile.flv” “path to outfile.flv”`

This will read in the source FLV and produce an output FLV with the correct duration in the metadata (thus it will allow seek operations)

If you run in the debugger you will also get a lot of useful output via the “Debug.Write” (System.Diagnostics)

Here is an example of how to use the lib.
```C
using(FlvStream2FileWriter stream2File = new FlvStream2FileWriter("out.flv"));
{
  // keep adding bytes to the file (choose a block size)
  while(bytedata){
    stream2File.Write(buffer);
  }

  // finish write and fix flv header.
  stream2File.FinallizeFile();
}
```

## Refrences
Here are some of the features of the more full featured and closed source version of FLV Metadata injector [http://www.buraks.com/flvmdi/](http://www.buraks.com/flvmdi/)

Here is some good information around FLV file format
* [http://en.wikipedia.org/wiki/Flash_Video](http://en.wikipedia.org/wiki/Flash_Video)
* [http://www.adobe.com/devnet/f4v.html](http://www.adobe.com/devnet/f4v.html)

Also you will need to know a little bit about “Action Message Format” (AMF).
* [http://en.wikipedia.org/wiki/Action_Message_Format](http://en.wikipedia.org/wiki/Action_Message_Format)

## Expanding on this
My needs were specific, but the code can be adapted very easily to handle any of the other functions that you see in closed source projects such as [FLVMDI](http://www.buraks.com/flvmdi/)

## Ideas, Questions, Comments?
Just message me and I will try to help.




[![Bitdeli Badge](https://d2weczhvl823v0.cloudfront.net/coreyauger/flv-streamer-2-file/trend.png)](https://bitdeli.com/free "Bitdeli Badge")

