﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Lucene.Net.Analysis;
using Lucene.Net.Store;
using Lucene.Net.Analysis.Standard;
using HundredMilesSoftware.UltraID3Lib;
using System.IO;
using Lastfm.Services;
using Lastfm.Scrobbling;

namespace kyokuFind
{
    public partial class frmMain : Form
    {
        //operators for special query
        string OP_ARTIST = "!songsby";
        string OP_ALBUM = "!songsin";
        string OP_GENRE = "!songs";

        //max number of recommended artists to display
        int N_RECOMMEND = 10;

        //temporary playlist location
        string PLAYLIST_PATH = Path.GetTempPath() + "tmp_playlist.m3u";
        Console console;
        LuceneService lucene;
        private Session session;

        public frmMain()
        {
            InitializeComponent();
            SetUp();
        }

        //all initialization goes here
        private void SetUp()
        {
            console = new Console(this.txtConsole);
            lucene = new LuceneService(console);
            session = new Session(CONFIG.LASTFM_API_KEY, CONFIG.LASTFM_API_SECRET);
        }

        //to process hotkeys
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            //ctrl+l = clear console
            if(keyData == (Keys.Control | Keys.L))
            {
                console.Clear();
                return true;
            }
            //ctrl+r = build index
            if (keyData == (Keys.Control | Keys.R))
            {
                console.Log("(Re)building index...");
                lucene.RebuildIndex();
                console.Log("Index successfully created");
                return true;
            }
            //ctrl+p = create playlists from displayed songs
            if (keyData == (Keys.Control | Keys.P))
            {
                console.Log("creating playlist...");
                createPlaylist((IEnumerable<Result>) lstSongs.DataSource);
                console.Log("playlist created.");
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        //Main search method, does preprocessing of special and recommendation queries
        //in the case of special and regular queries, they're sent to the Lucene Service for search and retrieval
        //in the case of recommendation queries, they're sent to the method that handles the Last.FM API
        private void Search(string query)
        {
            IEnumerable<Result> songs, artists, albums, genres;
            bool recommendationQuery = false;
            songs = artists = albums = genres = null;
            query = query.Trim();
            //empty query
            if (query == "")
            {
                DisplayResults(songs, artists, albums, genres);
                return;
            }

            //special query
            if (query.Contains("!songs"))
            {
                query = processSpecialQuery(query);
            }
            //only works for artists
            else if (query.Contains("!similarto"))
            {
                artists = processRecommendationQuery(query);
                recommendationQuery = true;
            }

            if(!recommendationQuery)
                lucene.Search(query, ref songs, ref artists, ref albums, ref genres);
            DisplayResults(songs, artists, albums, genres);
        }

        //last.fm API
        private IEnumerable<Result> processRecommendationQuery(string query)
        {
            List<Result> results = new List<Result>(N_RECOMMEND);
            var name = query.Replace("!similarto","").Trim();
            var artist = new Lastfm.Services.Artist(name, session);
            var similarArtists = artist.GetSimilar(N_RECOMMEND);
            foreach (var a in similarArtists)
            {
                results.Add(new Result { Artist = a.Name, URL = a.URL });
            }
            return results;
        }

        //processes special queries: of type !songs, !songsby, !songsin
        private string processSpecialQuery(string query)
        {
            string[] tokens;
            string field = "";
            //artist query
            if (query.Contains(OP_ARTIST))
            {
                query = query.Replace(OP_ARTIST, "");
                field = LuceneService.F_ARTIST;
            }
            else if (query.Contains(OP_ALBUM))
            {
                query = query.Replace(OP_ALBUM, "");
                field = LuceneService.F_ALBUM;
            }
            else if (query.Contains(OP_GENRE))
            {
                query = query.Replace(OP_GENRE, "");
                field = LuceneService.F_GENRE;
            }
            tokens = query.Trim().Split(',');
            string finalQuery = "";
            for (int i = 0; i < tokens.Length; i++)
            {
                finalQuery += field + ":\"" + tokens[i].Trim() + "\"";
                //if it's not the last
                if (i != tokens.Length - 1)
                    finalQuery += " OR ";
            }
            return finalQuery;
        }

        //updates listboxes with the results retrieved
        private void DisplayResults(IEnumerable<Result> songs, IEnumerable<Result> artists,
            IEnumerable<Result> albums, IEnumerable<Result> genres)
        {
            lstSongs.DataSource = songs;
            lstSongs.DisplayMember = "DisplaySong";
            lstGenres.ValueMember = "Filename";

            lstArtists.DataSource = artists;
            lstArtists.DisplayMember = "DisplayArtist";

            lstAlbums.DataSource = albums;
            lstAlbums.DisplayMember = "DisplayAlbum";

            lstGenres.DataSource = genres;
            lstGenres.DisplayMember = "DisplayGenre";
        }

        //performs search when Enter is pressed
        private void txtSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.Handled = e.SuppressKeyPress = true;
                //avoid beeping sound!
                console.Log("performing search");
                Search(txtSearch.Text);
                
            }
        }

        //creates playlists froom a set of songs
        private void createPlaylist(IEnumerable<Result> songs)
        {
            using (var sw = new System.IO.StreamWriter(PLAYLIST_PATH, false))
            {
                foreach (var s in songs)
                {
                    sw.WriteLine(s.Filename);
                }
            }
            System.Diagnostics.Process.Start(PLAYLIST_PATH);
        }

        private void txtSearch_TextChanged(object sender, EventArgs e)
        {
            //used for instant search, disabled because of conflicts with special and recommendation queries
            //IF UNCOMMENTED, PROCEED WITH CAUTION
            //Search(txtSearch.Text);
        }
        
        private void lstArtists_DoubleClick(object sender, EventArgs e)
        {
            goToArtistsURL();
        }

        //goes to recommended artist LastFM page using default browser
        private void goToArtistsURL()
        {
            var i = lstArtists.SelectedIndex;
            if (i == -1)
                return;
            var URL = ((IEnumerable<Result>) lstArtists.DataSource).ElementAt(i).URL;
            if (URL == null)
                return;
            System.Diagnostics.Process.Start(URL);
        }

        private void btnSelectMusicFolder_Click(object sender, EventArgs e)
        {
            var dlgResult = folderBrowserDialog.ShowDialog();
            if(dlgResult == DialogResult.Cancel)
            {
                return;
            }
            Mp3Tags.musicPath = lblMusicPath.Text = folderBrowserDialog.SelectedPath;
            lucene.RebuildIndex();
        }
    }
}