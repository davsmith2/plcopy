using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Threading;
using System.Web;


public class TrackReference
{
    public string SourcePath { get; set; }
    public long Size { get; set; }

    public string Name { get; set; }
    public string Artist { get; set; }
    public string Album { get; set; }
    public bool Compilation { get; set; }

    public long DiscNumber { get; set; }
    public long TrackNumber { get; set; }
    public long Duration { get; set; }

    string _Ext { get { return Path.GetExtension(SourcePath); } }

    string _Filename
    {
        get
        {
            string prefix = Compilation ? Album : Artist;
            return String.Format("{0}; {1}", prefix.Substring(0, Math.Min(15, prefix.Length)), Name);
        }
    }

    public string PlaylistName
    {
        get
        {
            if (Compilation)
            {
                return string.Format("{0}", Album);
            }
            else
            {
                return string.Format("{0} - {1}", Album, Artist);
            }
        }
    }

    public string TrimmedFilename
    {
        get
        {
            string strExt = _Ext;
            return Helpers.CleanFilename(_Filename, 31 - strExt.Length) + strExt;  // 32 - 1 for the M folder
        }
    }

    public string FullFilename
    {
        get
        {
            return Helpers.CleanFilename(_Filename) + _Ext;
        }
    }
}


public class TrackCollection
{
    public class CopyException : Exception
    {
        public enum Failure
        {
            FailedToCreatePlaylist,
            FailedToCreateDestinationFile,
            FailedToOpenSourceTrack,
            NoDestination,
        }

        public Failure reason;
        public CopyException(Failure f, string msg) : base(msg)
        {
           reason = f;
        }
    }

    public string Name { get; set; }
    public string PersistentKey { get; set; }

    List<TrackReference> _tracks = new List<TrackReference>();
    public List<TrackReference> Tracks 
    { 
        get { return _tracks; } 
    }

    public delegate void CopyStagesEventHandler(TrackReference tr);
    public event CopyStagesEventHandler CopyStageEvents;                        // this delegate fires to notify callers of CopyTo of our progress

    public void CopyTo(string strDest, bool fLimitNames, bool fAlbumPlaylists, string strCarType)
    {
        try
        {
            Helpers.EnsureDirectory(strDest);
            Helpers.EnsureDirectory(Helpers.Combine(strDest, "M"));

            if (fAlbumPlaylists)
            {
                _SaveAlbumStructure(strDest, fLimitNames);
            }

            _SaveTracksAsPlaylist(_tracks, strDest, Name, fLimitNames, !fAlbumPlaylists);
        }
        catch (IOException)
        {
            throw new CopyException(CopyException.Failure.NoDestination, "Destination location doesn't exist");
        }
    }

    // construct a collection of playlists for each album referneced in the playlist

    private void _SaveAlbumStructure(string strDest, bool fLimitNames)
    {
        Dictionary<string, List<TrackReference>> albums = new Dictionary<string,List<TrackReference>>();;
        foreach (TrackReference tr in _tracks)
        {
            List<TrackReference> tracks;
            if (!albums.ContainsKey(tr.PlaylistName))
            {
                tracks = new List<TrackReference>();
                albums.Add(tr.PlaylistName, tracks);
            }
            else
            {
                tracks = albums[tr.PlaylistName];
            }

// TODO: duplicate tracks could exist in the playlist, so here would be a great place to check for them.

            tracks.Add(tr);
        }

        foreach(string strAlbum in albums.Keys)
        {
            List<TrackReference> tracks = albums[strAlbum];

            tracks.Sort(
                delegate(TrackReference tr1, TrackReference tr2)
                {
                    Int32 iResult = (Int32)(tr1.DiscNumber - tr2.DiscNumber);
                    if (iResult == 0)
                    {
                        iResult = (Int32)(tr1.TrackNumber - tr2.TrackNumber);
                    }
                    return iResult;
                });

            _SaveTracksAsPlaylist(tracks, strDest, strAlbum, fLimitNames, true);
        }
    }

    // copy a collection of tracks and create a supporting playlist to reference them

    private void _SaveTracksAsPlaylist(List<TrackReference> tracks, string strDest, string strName, bool fLimitNames, bool fCopyFile)
    {
        string strPlaylist = (fLimitNames ? Helpers.CleanFilename(strName, 28) : Helpers.CleanFilename(strName)) + ".M3U";
        try
        {
            StreamWriter writer = new StreamWriter(File.Create(Helpers.Combine(strDest, strPlaylist)), System.Text.Encoding.ASCII);
            writer.WriteLine("#EXTM3U");

            foreach (TrackReference tr in tracks)
            {
                string strRelName = Helpers.Combine("M", fLimitNames ? tr.TrimmedFilename : tr.FullFilename);

                if (fCopyFile)
                {
                    CopyStageEvents(tr);
                    _CopyFile(tr, Helpers.Combine(strDest, strRelName));
                }

                writer.WriteLine();
                writer.WriteLine("#EXTINF:{0},{1} - {2}", tr.Duration, tr.Artist, tr.Name);
                writer.WriteLine("{0}", strRelName);
            }

            writer.Close();
        } 
        catch (IOException)
        {
            throw new CopyException(CopyException.Failure.FailedToCreatePlaylist, "Failed to create playlist file for: " + strName);
        }
    }

    // "block" copy a track from the source to destination allowing for progress callbacks.

    private void _CopyFile(TrackReference tr, string strDest)
    {
        try
        {
            using (FileStream streamIn = File.OpenRead(tr.SourcePath))
            {
                try
                {
                    using (FileStream streamOut = File.Create(strDest))
                    {
                        int cbOffset = 0;
                        while (streamIn.Length - cbOffset > 0)
                        {
                            int cbBlockSize = Math.Min((int)streamIn.Length - cbOffset, 2 * 1024 * 1024);       // copy in 2MB blocks
                            byte[] buffer = new byte[cbBlockSize];
                            streamOut.Write(buffer, 0, streamIn.Read(buffer, 0, cbBlockSize));

                            cbOffset += cbBlockSize;
                        }
                        streamOut.Close();
                    }
                    streamIn.Close();
                }
                catch (IOException)
                {
                    throw new CopyException(CopyException.Failure.FailedToCreateDestinationFile, "Failed to create: " + strDest);
                }
            }
        }
        catch (IOException)
        {
            throw new CopyException(CopyException.Failure.FailedToOpenSourceTrack, "Failed to open: " + tr.SourcePath);
        }
    }
}


public class iTunesData
{
    // strip file://localhost from a URL so we can use it for the path name to an track we want to copy

    static string _UriToFilePath(string strUri)
    {
        Uri uri = new Uri(strUri);
        if (uri.Scheme != "file" && !uri.IsLoopback)
        {
            throw new FormatException("Non file: URL scheme specified");
        }
        return uri.LocalPath.Remove(0, 12);
    }


    // parse the iTunes library and read the track data for a given list, returning a reference to the TrackCollection

    public bool LoadiTunesDataFile(string strDataFile, string strPlaylist, out TrackCollection tcOut)
    {
        tcOut = null;       // don't assume this was initialized

        PropertyList plistRoot = new PropertyList();
        plistRoot.ParseFile(strDataFile);

        foreach (PropertyList plistPlaylist in plistRoot.Value("Playlists"))
        {
            if (plistPlaylist.Value("Name") == strPlaylist)
            {
                tcOut = new TrackCollection() 
                { 
                    Name = plistPlaylist.Value("Name"),
                    PersistentKey = plistPlaylist.Value("Playlist Persistent ID"),
                };
                
                if (plistPlaylist.Contains("Playlist Items"))
                {
                    // itterate over all the items in the playlist and dereference the track information to get metadata on the song
                    // note - iTunes XML only contains those fields in the original file so we must handle the "optional" data...

                    foreach (PropertyList plistItem in plistPlaylist.Value("Playlist Items"))
                    {
                        PropertyList plistTrack = plistRoot.Value("Tracks").Value(string.Format("{0}", plistItem.Value("Track ID")));
                        if (plistTrack.Value("Track Type") == "File")
                        {
                            string strFile = _UriToFilePath(plistTrack.Value("Location"));
                            TrackReference tr = new TrackReference()
                            {
                                SourcePath = strFile,
                                Name = plistTrack.Value("Name"),
                                Size = plistTrack.Value("Size"),
                                Artist = plistTrack.Value("Artist", "Unknown Artist"),
                                Album = plistTrack.Value("Album", "Unknown Album"),
                                Compilation = plistTrack.Value("Compilation", false),
                                DiscNumber = plistTrack.Value("Disc Number", 1),  
                                TrackNumber = plistTrack.Value("Track Number", 1),
                                Duration = plistTrack.Value("Total Time", 0),
                            };

                            tcOut.Tracks.Add(tr);
                        }
                    }
                }

                break;
            }
        }

        return (tcOut != null);
    }
}
