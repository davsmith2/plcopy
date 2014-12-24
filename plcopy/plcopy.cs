// History:
//  12/21/2014: Added cartype argument for specifying the make of car for the destination playlist. [DAVSMITH]
//

// TODO:
//  - Make iTunes data file relative to the Music known folder.
//  - Improve progress reporting to show the % of the track copied.
//  - Support down sampling and transcoding.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace PlaylistCopy
{
    class plcopy
    {
        static void _CopyProgressHandler(TrackReference tr)
        {
            Console.WriteLine(String.Format("Copying: {0}", tr.Name));
        }

        static void Main(string[] args)
        {
            String strLibrary = Environment.ExpandEnvironmentVariables("%userprofile%\\Music\\iTunes\\iTunes Music Library.xml");
            String strDest = null;
            String strList = null;
            String strCarType = "audi";
            bool fLimitNames = false;
            bool fNoAlbums = false;
            bool fBadArgs = false;

            Queue<String> qArgs = new Queue<String>(args);
            while (qArgs.Count > 0)
            {
                switch (qArgs.Dequeue().ToLower())
                {
                    case "-rnse":
                        fLimitNames = true;
                        break;

                    case "-noalbums":
                        fNoAlbums = true;
                        break;

                    case "-dest":
                        strDest = (qArgs.Count >=1) ? qArgs.Dequeue() : null;
                        break;

                    case "-list":
                        strList = (qArgs.Count >=1) ? qArgs.Dequeue() : null; 
                        break;

                    case "-library":
                        strLibrary = (qArgs.Count >=1) ? qArgs.Dequeue() : null;
                        break;

                    case "-cartype":
                        strCarType = (qArgs.Count >= 1) ? qArgs.Dequeue().ToLower() : null;
                        break;

                    default:
                        fBadArgs = true;
                        break;
                }
            }

            if (strList == null || strDest == null || fBadArgs)
            {
                Console.WriteLine("");
                Console.WriteLine("Usage: plcopy.exe");
                Console.WriteLine("");
                Console.WriteLine("             -list <playlist> : sets the iTunes playlist to copy");
                Console.WriteLine("             -dest <destination folder> : sets the destination folder to copy to");
                Console.WriteLine("             [-rnse] : respects Audi RNSE naming limitations");
                Console.WriteLine("             [-noalbums] : turns off album playlist creation");
                Console.WriteLine("             [-library <path to iTunes data file>] : sets iTunes library to read from");
                Console.WriteLine("             [-cartype <audi | nissan>] : make of car on which the playlist will be used.");
            }
            else
            {
                Console.WriteLine("Loading library: " + strLibrary);
                Console.WriteLine("Copying list: " + strList);
                Console.WriteLine("Destination: " + strDest);
                Console.WriteLine("Car type: " + strCarType);

                try
                {
                    TrackCollection tc;
                    new iTunesData().LoadiTunesDataFile(strLibrary, strList, out tc);
        
                    if (tc != null)
                    {
                        try
                        {
                            tc.CopyStageEvents += new TrackCollection.CopyStagesEventHandler(_CopyProgressHandler);
                            tc.CopyTo(strDest, fLimitNames, !fNoAlbums, strCarType);
                        }
                        catch (TrackCollection.CopyException e)
                        {
                            Console.WriteLine(e.Message);
                        }
                    }
                    else
                    {
                        Console.WriteLine("No playlist called: " + strList);
                    }
                }
                catch (IOException)
                {
                    Console.WriteLine("Failed to read iTunes data file");
                }
                catch (FormatException e)
                {
                    Console.WriteLine("Failed to parse iTunes data file: " + e.Message);
                }
                catch (Exception e)
                {
                    Console.WriteLine("Unexepected failure: " + e.Message);
                }
            }
        }
    }
}
