//
// plcopy.exe
//
//      -list <playlist name>                   - source playlist to transfer, case sensitive
//      -destination <destination folder name>  - destination folder
//      [-library <path to iTunes XML>]         - configure which iTunes library to copy
//      [-rnse]                                 - Audi RNSE name limits respected
//      [-noalbums]                             - Supress album Playlists
//

// TODO:
//  - Make iTunes data file relateive to the Music known folder.
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
            bool fLimitNames = false;
            bool fNoAlbums = false;

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
                        strDest = qArgs.Dequeue();
                        break;

                    case "-list":
                        strList = qArgs.Dequeue();
                        break;

                    case "-library":
                        strLibrary = qArgs.Dequeue();
                        break;

                    default:
                       break;
                }
            }

            Console.WriteLine("Loading library: " + strLibrary);
            Console.WriteLine("Copying list: " + strList);
            Console.WriteLine("Destination: " + strDest);

            try
            {
                TrackCollection tc;
                new iTunesData().LoadiTunesDataFile(strLibrary, strList, out tc);
        
                if (tc != null)
                {
                    try
                    {
                        tc.CopyStageEvents += new TrackCollection.CopyStagesEventHandler(_CopyProgressHandler);
                        tc.CopyTo(strDest, fLimitNames, !fNoAlbums);
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
